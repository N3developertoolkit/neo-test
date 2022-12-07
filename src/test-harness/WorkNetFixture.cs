using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Neo;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using Neo.BlockchainToolkit.Persistence;
using Neo.Network.RPC;
using Neo.Persistence;
using Xunit;

namespace NeoTestHarness
{
    public abstract class WorkNetFixture : IAsyncLifetime
    {
        private readonly static Lazy<IFileSystem> defaultFileSystem = new Lazy<IFileSystem>(() => new FileSystem());
        private StateServiceStore workNetStore;
        private RocksDbSharp.RocksDb db;
        private ProtocolSettings settings;
        private uint height;
        private RpcClient client;
        private WorkNetConfigAttribute workNetConfig;
        private string[] prefetchContracts;
        private Dictionary<UInt160, string> contractNameCache = new();

        public IReadOnlyStore WorkNetStore => workNetStore;
        public ProtocolSettings ProtocolSettings => workNetStore.Settings;

        public WorkNetFixture(
                string[] prefetchContracts,
                WorkNetConfigAttribute cfg)
        {
            this.workNetConfig = cfg;
            this.prefetchContracts = prefetchContracts;

            settings = workNetConfig.Settings ?? ProtocolSettings.Default with
            {
                Network = 7630401, // mainnet
                AddressVersion = 0x35,
                MillisecondsPerBlock = 15000,
            };

            client = new RpcClient(new Uri((workNetConfig.RpcUri)), null, null, settings);

            if (workNetConfig.Height == "latest")
            {
                height = client.GetBlockCountAsync().GetAwaiter().GetResult() - 1;
            }
            else
            {
                if (!UInt32.TryParse(workNetConfig.Height, out height))
                {
                    throw new Exception($"");
                }
            }
        }

        public async Task InitializeAsync()
        {
            var branchInfo = await StateServiceStore.GetBranchInfoAsync(client, height);

            db = RocksDbUtility.OpenDb(workNetConfig.DbPath);
            workNetStore = new StateServiceStore(client, branchInfo, db);

            var options = new ParallelOptions { MaxDegreeOfParallelism = 3 };
            var nativeContracts = branchInfo.Contracts.Where( x => x.Id < 0);
            await Parallel.ForEachAsync(nativeContracts, options, async (contract, token) =>
            {
                var contractHash = contract.Hash ?? throw new Exception("Null contract address in branch info");
                contractNameCache.Add(contractHash, contract.Name);
                await workNetStore.PrefetchAsync(contractHash, CancellationToken.None, 
                        ( foundStates ) =>
                        {
                            Console.WriteLine($"found states for (native) {GetContractName(contractHash)}");
                        });
            });

            await Parallel.ForEachAsync(prefetchContracts, options, async (contractName, token) =>
            {
                var contract = branchInfo.Contracts.Where( x => x.Name == contractName ).FirstOrDefault();
                contractNameCache.Add(contract.Hash, contract.Name);
                await workNetStore.PrefetchAsync(contract.Hash, CancellationToken.None, 
                        ( foundStates ) =>
                        {
                            Console.WriteLine($"found states for {GetContractName(contract.Hash)}");
                        });
            });
        }

        private string GetContractName(UInt160 hash)
        {
            if (contractNameCache.TryGetValue(hash, out string name))
            {
                return name;
            }

            return "unknown_" + hash;
        }

        public Task DisposeAsync()
        {
            workNetStore.Dispose();
            client.Dispose();

            return Task.CompletedTask;
        }

        public ExpressChain FindChain(string fileName = Constants.DEFAULT_EXPRESS_FILENAME, IFileSystem? fileSystem = null, string? searchFolder = null)
            => (fileSystem ?? defaultFileSystem.Value).FindChain(fileName, searchFolder);
    }
}

