using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
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

        [Argument(0)]
        internal string NeoInvokeFile { get; set; } = string.Empty;

        [Option("-c|--checkpoint")]
        internal string CheckpointFile { get; set; } = string.Empty;

        [Option("-e|--express")]
        internal string NeoExpressFile { get; set; } = string.Empty;

        [Option]
        internal string Signer { get; set; } = string.Empty;

        [Option("--iterator-count")]
        internal int MaxIteratorCount { get; set; } = 100;

        [Option("--version")]
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

                ExpressChain? chain = string.IsNullOrEmpty(NeoExpressFile)
                    ? null : fileSystem.LoadChain(NeoExpressFile);

                var signer = ParseSigner(chain);

                using ICheckpoint checkpoint = string.IsNullOrEmpty(CheckpointFile)
                    ? Checkpoint.NullCheckpoint
                    : new Checkpoint(CheckpointFile, chain);

                using var store = new MemoryTrackingStore(checkpoint.Store);
                store.EnsureLedgerInitialized(checkpoint.Settings);

                var tryGetContract = store.CreateTryGetContract();

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

                await WriteResultsAsync(app.Out, engine, logEvents, notifyEvents);

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
            if (string.IsNullOrEmpty(Signer))
            {
                return UInt160.Zero;
            }

            if (UInt160.TryParse(Signer, out var signer))
            {
                return signer;
            }

            if (chain != null && chain.TryGetDefaultAccount(Signer, out var account))
            {
                return account.ToScriptHash(chain.AddressVersion);
            }

            throw new ArgumentException($"couldn't parse \"{Signer}\" as {nameof(Signer)}", nameof(Signer));
        }

        private async Task WriteResultsAsync(TextWriter textWriter, TestApplicationEngine engine,
            IReadOnlyList<LogEventArgs> logEvents, IReadOnlyList<NotifyEventArgs> notifyEvents)
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
            foreach (var log in logEvents)
            {
                await writer.WriteLogAsync(log);
            }
            await writer.WriteEndArrayAsync();

            await writer.WritePropertyNameAsync("notifications");
            await writer.WriteStartArrayAsync();
            foreach (var notification in notifyEvents)
            {
                await writer.WriteNotificationAsync(notification, MaxIteratorCount);
            }
            await writer.WriteEndArrayAsync();

            await writer.WritePropertyNameAsync("stack");
            await writer.WriteStartArrayAsync();
            foreach (var r in engine.ResultStack)
            {
                await writer.WriteStackItemAsync(r, MaxIteratorCount);
            }
            await writer.WriteEndArrayAsync();

            await writer.WriteEndObjectAsync();
        }
    }
}
