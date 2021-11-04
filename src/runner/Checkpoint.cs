using System;
using System.IO;
using Neo.BlockchainToolkit.Models;
using Neo.BlockchainToolkit.Persistence;
using Neo.Persistence;

namespace Neo.Test.Runner
{
    class Checkpoint : ICheckpoint
    {
        class NullStoreCheckpoint : ICheckpoint
        {
            public IReadOnlyStore Store => NullStore.Instance;
            public ProtocolSettings Settings => ProtocolSettings.Default;

            public void Dispose() { }
        }

        public static readonly ICheckpoint NullCheckpoint = new NullStoreCheckpoint();

        readonly RocksDbStore store;
        readonly string checkpointTempPath;
        readonly ProtocolSettings settings;

        public IReadOnlyStore Store => store;
        public ProtocolSettings Settings => settings;

        public Checkpoint(string checkpointPath, ExpressChain? chain)
            : this(checkpointPath, chain?.Network, chain?.AddressVersion)
        {
        }

        public Checkpoint(string checkpointPath, uint? network = null, byte? addressVersion = null)
        {
            do
            {
                checkpointTempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            }
            while (System.IO.Directory.Exists(checkpointTempPath));

            var metadata = RocksDbUtility.RestoreCheckpoint(checkpointPath, checkpointTempPath);
            if (network.HasValue && network.Value != metadata.magic)
                throw new Exception($"checkpoint network ({metadata.magic}) doesn't match ({network.Value})");
            if (addressVersion.HasValue && addressVersion.Value != metadata.addressVersion)
                throw new Exception($"checkpoint address version ({metadata.addressVersion}) doesn't match ({addressVersion.Value})");

            this.settings = ProtocolSettings.Default with
            {
                Network = metadata.magic,
                AddressVersion = metadata.addressVersion,
            };

            var db = RocksDbUtility.OpenReadOnlyDb(checkpointTempPath);
            this.store = new RocksDbStore(db, readOnly: true);
        }

        public void Dispose()
        {
            store.Dispose();

            if (Directory.Exists(checkpointTempPath))
            {
                Directory.Delete(checkpointTempPath, true);
            }
        }
    }
}
