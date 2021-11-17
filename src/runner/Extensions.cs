using System;
using System.IO.Abstractions;
using System.Linq;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;

namespace Neo.Test.Runner
{
    static class Extensions
    {
        public static ContractParameterParser CreateContractParameterParser(this IReadOnlyStore store, ExpressChain chain, IFileSystem? fileSystem = null)
        {
            var tryGetContract = CreateTryGetContract(store);
            return CreateContractParameterParser(chain, tryGetContract, fileSystem);
        }

        public static ContractParameterParser CreateContractParameterParser(this ExpressChain chain, ContractParameterParser.TryGetUInt160 tryGetContract, IFileSystem? fileSystem = null)
        {
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

        public static ContractParameterParser CreateContractParameterParser(this IReadOnlyStore store, ProtocolSettings settings, IFileSystem? fileSystem = null)
        {
            var tryGetContract = CreateTryGetContract(store);
            return CreateContractParameterParser(settings, tryGetContract, fileSystem);
        }

        public static ContractParameterParser CreateContractParameterParser(this ProtocolSettings settings, ContractParameterParser.TryGetUInt160 tryGetContract, IFileSystem? fileSystem = null)
        {
            return new ContractParameterParser(settings.AddressVersion,
                                               tryGetAccount: null,
                                               tryGetContract: tryGetContract,
                                               fileSystem: fileSystem);
        }

        public static ContractParameterParser.TryGetUInt160 CreateTryGetContract(this IReadOnlyStore store)
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
        public static void EnsureLedgerInitialized(this IStore store, ProtocolSettings settings)
        {
            const byte Prefix_BlockHash = 9;
            var key = new KeyBuilder(NativeContract.Ledger.Id, Prefix_BlockHash).ToArray();

            using var snapshot = new SnapshotCache(store.GetSnapshot());
            if (snapshot.Find(key).Any()) return;

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

        class InitializeAppEngine : ApplicationEngine
        {
            public InitializeAppEngine(TriggerType trigger, DataCache snapshot, Block persistingBlock, ProtocolSettings settings, long gas = 2000000000L)
                : base(trigger, null, snapshot, persistingBlock, settings, gas)
            {
            }

            public new void NativeOnPersist() => base.NativeOnPersist();
            public new void NativePostPersist() => base.NativePostPersist();
        }
    }
}
