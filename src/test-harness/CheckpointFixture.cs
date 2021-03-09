using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Neo;
using Neo.BlockchainToolkit.Persistence;

namespace NeoTestHarness
{
    public abstract class CheckpointFixture : IDisposable
    {
        string checkpointTempPath;
        RocksDbStore rocksDbStore;

        public CheckpointFixture(string checkpointPath)
        {
            if (Path.IsPathFullyQualified(checkpointPath))
            {
                if (!File.Exists(checkpointPath)) throw new FileNotFoundException("couldn't find checkpoint", checkpointPath);
            }
            else
            {
                var directory = Path.GetFullPath(".");
                var tempPath = Path.GetFullPath(checkpointPath, directory);
                while (!File.Exists(tempPath))
                {
                    directory = Path.GetDirectoryName(directory);
                    tempPath = Path.GetFullPath(checkpointPath, directory!);
                }
                checkpointPath = tempPath;
            }

            do
            {
                checkpointTempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            }
            while (Directory.Exists(checkpointTempPath));

            var magic = RocksDbStore.RestoreCheckpoint(checkpointPath, checkpointTempPath);
            rocksDbStore = RocksDbStore.OpenReadOnly(checkpointTempPath);
        }

        public void Dispose()
        {
            rocksDbStore.Dispose();
            if (Directory.Exists(checkpointTempPath)) Directory.Delete(checkpointTempPath, true);
        }

        public CheckpointStore GetCheckpointStore()
        {
            return new CheckpointStore(rocksDbStore, false);
        }
    }
}

