using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using Neo.BlockchainToolkit.Persistence;
using Neo.BlockchainToolkit.SmartContract;
using Neo.Network.P2P.Payloads;
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

                var signer = UInt160.Zero;
                if (!string.IsNullOrEmpty(Signer))
                {
                    if (UInt160.TryParse(Signer, out var _signer))
                    {
                        signer = _signer;
                    }

                    if (chain != null && chain.TryGetDefaultAccount(Signer, out var account))
                    {
                        signer = Neo.Wallets.Helper.ToScriptHash(account.ScriptHash, chain.AddressVersion);
                    }

                    throw new ArgumentException($"couldn't parse \"{Signer}\" as {nameof(Signer)}", nameof(Signer));
                }

                using ICheckpoint checkpoint = string.IsNullOrEmpty(CheckpointFile)
                    ? Checkpoint.NullCheckpoint
                    : new Checkpoint(CheckpointFile, chain);

                using var store = new MemoryTrackingStore(checkpoint.Store);
                EnsureLedgerInitialized(store, checkpoint.Settings);
                var parser = chain != null
                    ? CreateContractParameterParser(chain, store, fileSystem)
                    : CreateContractParameterParser(checkpoint.Settings, store, fileSystem);

                var script = await parser.LoadInvocationScriptAsync(NeoInvokeFile); 

                using var snapshot = new SnapshotCache(store);
                using var engine = new TestApplicationEngine(snapshot, checkpoint.Settings, signer);

                List<LogEventArgs> logEvents = new();
                engine.Log += (_, args) => logEvents.Add(args);
                List<NotifyEventArgs> notifyEvents = new();
                engine.Notify += (_, args) => notifyEvents.Add(args);

                engine.LoadScript(script);
                var state = engine.Execute();

                using var writer = new JsonTextWriter(app.Out)
                {
                    Formatting = Formatting.Indented
                };

                await writer.WriteStartObjectAsync();

                await writer.WritePropertyNameAsync("state");
                await writer.WriteValueAsync(engine.State);;
                await writer.WritePropertyNameAsync("exception");
                await ((engine.FaultException == null)
                    ? writer.WriteNullAsync() 
                    : writer.WriteValueAsync(engine.FaultException.GetBaseException().Message));
                await writer.WriteValueAsync($"{engine.GasConsumed}");
                await writer.WritePropertyNameAsync("gasconsumed");

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

                return 0;
            }
            catch (Exception ex)
            {
                await app.Error.WriteLineAsync(ex.Message);
                return 1;
            }
        }

        class InitializeAppEngine : ApplicationEngine
        {
            public InitializeAppEngine(TriggerType trigger, DataCache snapshot, Block persistingBlock, ProtocolSettings settings, long gas = 2000000000L)
                : base(trigger, null, snapshot, persistingBlock, settings, gas)
            {
            }

            public new void NativeOnPersist() => base.NativeOnPersist();
            public new void NativePostPersist() => base.NativePostPersist();

            public static bool IsInitialized(DataCache snapshot)
            {
                const byte Prefix_BlockHash = 9;
                var key = new KeyBuilder(NativeContract.Ledger.Id, Prefix_BlockHash).ToArray();
                return snapshot.Find(key).Any();
            }
        }

        static void EnsureLedgerInitialized(IStore store, ProtocolSettings settings)
        {
            using var snapshot = new SnapshotCache(store.GetSnapshot());
            if (InitializeAppEngine.IsInitialized(snapshot)) return;

            var block = NeoSystem.CreateGenesisBlock(settings);
            if (block.Transactions.Length != 0) throw new Exception("Unexpected Transactions in genesis block");

            using (var engine = new InitializeAppEngine(TriggerType.OnPersist, snapshot, block, settings))
            {
                engine.NativeOnPersist();
                if (engine.State != VMState.HALT) throw new InvalidOperationException("NativeOnPersist operation failed", engine.FaultException);
            }

            using (var engine = new InitializeAppEngine(TriggerType.PostPersist, snapshot, block, settings))
            {
                engine.NativePostPersist();
                if (engine.State != VMState.HALT) throw new InvalidOperationException("NativePostPersist operation failed", engine.FaultException);
            }

            snapshot.Commit();
        }

        static ContractParameterParser CreateContractParameterParser(ExpressChain chain, IReadOnlyStore store, IFileSystem? fileSystem = null)
        {
            var tryGetContract = BuildTryGetContract(store);
            ContractParameterParser.TryGetUInt160 tryGetAccount = (string name, out UInt160 scriptHash) =>
                {
                    if (chain.TryGetDefaultAccount(name, out var account))
                    {
                        scriptHash = Neo.Wallets.Helper.ToScriptHash(account.ScriptHash, chain.AddressVersion);
                        return true;
                    }

                    scriptHash = null!;
                    return false;
                };

            return new ContractParameterParser(chain.AddressVersion,
                                               tryGetAccount: tryGetAccount,
                                               tryGetContract: tryGetContract,
                                               fileSystem: fileSystem);
        }
            
        static ContractParameterParser CreateContractParameterParser(ProtocolSettings settings, IReadOnlyStore store, IFileSystem? fileSystem = null)
        {
            var tryGetContract = BuildTryGetContract(store);
            return new ContractParameterParser(settings.AddressVersion,
                                               tryGetAccount: null,
                                               tryGetContract: tryGetContract,
                                               fileSystem: fileSystem);
        }

        static ContractParameterParser.TryGetUInt160 BuildTryGetContract(IReadOnlyStore store)
        {
            (string name, UInt160 hash)[] contracts;
            using (var snapshot = new SnapshotCache(store))
            {
                contracts = NativeContract.ContractManagement.ListContracts(snapshot)
                    .Select(c => (name: c.Manifest.Name, hash: c.Hash))
                    .ToArray();
            }

            return (string name, out UInt160 scriptHash) =>
                {
                    for (int i = 0; i < contracts.Length; i++)
                    {
                        if (string.Equals(contracts[i].name, name))
                        {
                            scriptHash = contracts[i].hash;
                            return true;
                        }
                    }

                    for (int i = 0; i < contracts.Length; i++)
                    {
                        if (string.Equals(contracts[i].name, name, StringComparison.OrdinalIgnoreCase))
                        {
                            scriptHash = contracts[i].hash;
                            return true;
                        }
                    }

                    scriptHash = null!;
                    return false;
                };
        }
    }
}
