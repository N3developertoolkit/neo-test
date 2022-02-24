using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SimpleJSON;

namespace Neo.BuildTasks
{
    public class NeoContractInterface : Task
    {
        [Required]
        public ITaskItem OutputFile { get; set; } = default!;

        [Required]
        public ITaskItem ManifestFile { get; set; } = default!;

        public string RootNamespace { get; set; } = "";

        public override bool Execute()
        {
            try
            {
                var manifestJson = Utility.ReadJson(ManifestFile);
                var manifest = NeoManifest.FromJson(manifestJson);

                var source = ContractGenerator.GenerateContractInterface(manifest, RootNamespace);
                if (string.IsNullOrEmpty(source))
                    throw new Exception("Invalid generated source");

                Utility.FileOperationWithRetry(() => 
                {
                    var directory = Path.GetDirectoryName(OutputFile.ItemSpec);
                    if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
                    File.WriteAllText(OutputFile.ItemSpec, source);
                });
            }
            catch (Exception ex)
            {
                Log.LogError(ex.Message);
            }

            return !Log.HasLoggedErrors;
        }


    }
}
