using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Neo.BuildTasks
{
    public class NeoContractInterface : Task
    {
        public override bool Execute()
        {
            if (ManifestFiles.Length != OutputFiles.Length)
            {
                Log.LogError("Mismatched Manifest ({0}) and Output ({1}) file arrays", ManifestFiles.Length, OutputFiles.Length);
                return false;
            }

            for (int i = 0; i < ManifestFiles.Length; i++)
            {
                var manifestFile = ManifestFiles[i];
                var outputFile = OutputFiles[i];

                if (string.IsNullOrEmpty(manifestFile.ItemSpec))
                {
                    Log.LogError("Invalid ManifestFile {0}", manifestFile);
                    continue;
                }

                var manifest = NeoManifest.Load(manifestFile.ItemSpec);
                var generatedSource = ContractGenerator.GenerateContractInterface(manifest, RootNamespace);
                if (!string.IsNullOrEmpty(generatedSource))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(outputFile.ItemSpec));
                    FileOperationWithRetry(() => File.WriteAllText(outputFile.ItemSpec, generatedSource));
                }
            }

            return !Log.HasLoggedErrors;
        }

        [Required]
        public ITaskItem[] OutputFiles { get; set; } = Array.Empty<ITaskItem>();

        [Required]
        public ITaskItem[] ManifestFiles { get; set; } = Array.Empty<ITaskItem>();

        public string RootNamespace { get; set; } = "";

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
