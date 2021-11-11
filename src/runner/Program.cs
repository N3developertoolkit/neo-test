using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using Neo.BlockchainToolkit.Persistence;
using Neo.BlockchainToolkit.SmartContract;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Newtonsoft.Json;

namespace Neo.Test.Runner
{
    class Program
    {
        static Task<int> Main(string[] args)
        {
            var services = new ServiceCollection()
                .AddSingleton<IFileSystem, FileSystem>()
                .AddSingleton<IConsole>(PhysicalConsole.Singleton)
                .BuildServiceProvider();

            var app = new CommandLineApplication<Program>();
            app.Conventions
                .UseDefaultConventions()
                .UseConstructorInjection(services);

            return app.ExecuteAsync(args);
        }

        [Argument(0, Description = "Path to neo-invoke JSON file")]
        [Required]
        internal string NeoInvokeFile { get; set; } = string.Empty;

        [Option(Description = "Account that is invoking the contract")]
        internal string Account { get; set; } = string.Empty;

        [Option("-c|--checkpoint", Description = "Path to checkpoint file")]
        internal string CheckpointFile { get; set; } = string.Empty;

        [Option("-n|--nef-file")]
        internal string NefFile { get; set; } = string.Empty;

        [Option("-e|--express", Description = "Path to neo-express file")]
        internal string NeoExpressFile { get; set; } = string.Empty;

        [Option("-i|--iterator-count")]
        internal int MaxIteratorCount { get; set; } = 100;

        [Option(Description = "Contracts to include in storage results")]
        public string[] Storages { get; } = Array.Empty<string>();

        [Option("--version", Description = "Show version information.")]
        bool Version { get; }

        internal async Task<int> OnExecuteAsync(CommandLineApplication app, IFileSystem fileSystem)
        {
            try
            {
                if (Version)
                {
                    await app.Out.WriteLineAsync(ThisAssembly.AssemblyInformationalVersion);
                    return 0;
                }

                DebugInfo? debugInfo = string.IsNullOrEmpty(NefFile)
                    ? null 
                    : (await DebugInfo.LoadAsync(NefFile, fileSystem: fileSystem))
                        .Match<DebugInfo?>(di => di, _ => null);

                ExpressChain? chain = string.IsNullOrEmpty(NeoExpressFile)
                    ? null : fileSystem.LoadChain(NeoExpressFile);

                var signer = ParseSigner(chain);

                using ICheckpoint checkpoint = string.IsNullOrEmpty(CheckpointFile)
                    ? Checkpoint.NullCheckpoint
                    : new Checkpoint(CheckpointFile, chain);

                using var store = new MemoryTrackingStore(checkpoint.Store);
                store.EnsureLedgerInitialized(checkpoint.Settings);

                var tryGetContract = store.CreateTryGetContract();
                var storages = Storages
                    .Select(s => tryGetContract(s, out var hash) ? hash : null)
                    .Where(h => h != null)
                    .Cast<UInt160>()
                    .Distinct();

                var parser = chain != null
                    ? chain.CreateContractParameterParser(tryGetContract, fileSystem)
                    : checkpoint.Settings.CreateContractParameterParser(tryGetContract, fileSystem);

                var script = await parser.LoadInvocationScriptAsync(NeoInvokeFile);

                using var snapshot = new SnapshotCache(store);
                using var engine = new TestApplicationEngine(snapshot, checkpoint.Settings, signer);

                List<LogEventArgs> logEvents = new();
                engine.Log += (_, args) => logEvents.Add(args);
                List<NotifyEventArgs> notifyEvents = new();
                engine.Notify += (_, args) => notifyEvents.Add(args);

                engine.LoadScript(script);
                var state = engine.Execute();

                if (debugInfo != null)
                {
                    var contract = engine.ExecutedScripts.Values
                        .Where(s => s.IsT0).Select(s => s.AsT0)
                        .SingleOrDefault(c => c.Script.ToScriptHash() == debugInfo.ScriptHash);

                    if (contract != null)
                    {
                        var instructions = ((Script)contract.Script)
                            .EnumerateInstructions()
                            .ToArray();
                        var hitMap = engine.GetHitMap(contract.Hash);

                        var sequencePoints = debugInfo.Methods
                            .SelectMany(m => m.SequencePoints)
                            .Select(sp => sp.Address)
                            .ToArray();
                        var hitSequencePoints = hitMap
                            .Where(kvp => sequencePoints.Contains(kvp.Key))
                            .ToArray();

                        var branchInstructions = instructions
                            .Where(t => t.instruction.IsBranchInstruction())
                            .ToArray();
                        var branchMap = engine.GetBranchMap(contract.Hash);

                        var instructionCoverage = (decimal)hitMap.Count / (decimal)instructions.Length;
                        var sequencePointCoverage = (decimal)hitSequencePoints.Length / (decimal)sequencePoints.Length;
                        var branchCoverage = (decimal)branchMap.Count / (decimal)branchInstructions.Length

                        ;
                    }
                }

                await WriteResultsAsync(app.Out, engine, logEvents, notifyEvents, storages);

                return 0;
            }
            catch (Exception ex)
            {
                await app.Error.WriteLineAsync(ex.Message);
                return 1;
            }
        }

        UInt160 ParseSigner(ExpressChain? chain)
        {
            if (string.IsNullOrEmpty(Account))
            {
                return UInt160.Zero;
            }

            if (UInt160.TryParse(Account, out var signer))
            {
                return signer;
            }

            if (chain != null && chain.TryGetDefaultAccount(Account, out var account))
            {
                return account.ToScriptHash(chain.AddressVersion);
            }

            throw new ArgumentException($"couldn't parse \"{Account}\" as {nameof(Account)}", nameof(Account));
        }

        private async Task WriteResultsAsync(TextWriter textWriter, TestApplicationEngine engine,
            IReadOnlyList<LogEventArgs> logEvents, IReadOnlyList<NotifyEventArgs> notifyEvents,
            IEnumerable<UInt160> storages)
        {
            using var writer = new JsonTextWriter(textWriter)
            {
                Formatting = Formatting.Indented
            };

            await writer.WriteStartObjectAsync();

            await writer.WritePropertyNameAsync("state");
            await writer.WriteValueAsync($"{engine.State}"); ;
            await writer.WritePropertyNameAsync("exception");
            await ((engine.FaultException == null)
                ? writer.WriteNullAsync()
                : writer.WriteValueAsync(engine.FaultException.GetBaseException().Message));
            await writer.WritePropertyNameAsync("gasconsumed");
            await writer.WriteValueAsync($"{new BigDecimal((BigInteger)engine.GasConsumed, NativeContract.GAS.Decimals)}");

            await writer.WritePropertyNameAsync("logs");
            await writer.WriteStartArrayAsync();
            for (int i = 0; i < logEvents.Count; i++)
            {
                await writer.WriteLogAsync(logEvents[i]);
            }
            await writer.WriteEndArrayAsync();

            await writer.WritePropertyNameAsync("notifications");
            await writer.WriteStartArrayAsync();
            for (int i = 0; i < notifyEvents.Count; i++)
            {
                await writer.WriteNotificationAsync(notifyEvents[i], MaxIteratorCount);
            }
            await writer.WriteEndArrayAsync();

            await writer.WritePropertyNameAsync("stack");
            await writer.WriteStartArrayAsync();
            IReadOnlyList<VM.Types.StackItem> list = engine.ResultStack;
            for (int i = 0; i < list.Count; i++)
            {
                await writer.WriteStackItemAsync(list[i], MaxIteratorCount);
            }
            await writer.WriteEndArrayAsync();

            await writer.WritePropertyNameAsync("storages");
            await writer.WriteStartArrayAsync();
            foreach (var contractHash in storages)
            {
                await writer.WriteStorageAsync(engine.Snapshot, contractHash);
            }
            await writer.WriteEndArrayAsync();
            await writer.WriteEndObjectAsync();
        }
    }
}
