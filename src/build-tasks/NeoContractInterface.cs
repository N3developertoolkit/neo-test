﻿using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Neo.BuildTasks
{
    public class NeoContractInterface : Task
    {
        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.High, OutputFile);
            Log.LogMessage(MessageImportance.High, ManifestFile);
            Log.LogMessage(MessageImportance.High, ContractNamespace);

            if (string.IsNullOrEmpty(ManifestFile))
            {
                Log.LogError("Invalid ManifestFile");
            }
            else
            {
                var manifest = NeoManifest.Load(ManifestFile);
                var source = manifest.GenerateContractInterface(ContractNamespace);
                if (!string.IsNullOrEmpty(source))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(this.OutputFile));
                    FileOperationWithRetry(() => File.WriteAllText(this.OutputFile, source));
                }
            }
            return !Log.HasLoggedErrors;
        }

        [Required]
        public string OutputFile { get; set; } = "";

        [Required]
        public string ManifestFile { get; set; } = "";

        public string ContractNamespace { get; set; } = "";

        static void FileOperationWithRetry(Action operation)
        {
            const int ProcessCannotAccessFileHR = unchecked((int)0x80070020);

            for (int retriesLeft = 6; retriesLeft > 0; retriesLeft--)
            {
                try
                {
                    operation();
                }
                catch (IOException ex) when (ex.HResult == ProcessCannotAccessFileHR && retriesLeft > 0)
                {
                    System.Threading.Tasks.Task.Delay(100).Wait();
                    continue;
                }
            }
        }
    }
}
