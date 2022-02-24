using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Neo.BuildTasks
{
    public class NeoContractManifestExtra : Task
    {
        [Required]
        public ITaskItem ManifestFile { get; set; } = default!;

        [Required]
        public ITaskItem ExtraJsonFile { get; set; } = default!;

        [Required]
        public string ExtraPropertyName { get; set; } = string.Empty;

        public override bool Execute()
        {
            try
            {
                if (string.IsNullOrEmpty(ExtraPropertyName))
                    throw new Exception();

                var manifest = Utility.ReadJson(ManifestFile);
                var extra = Utility.ReadJson(ExtraJsonFile);

                NeoManifest.UpdateExtra(manifest, extra, ExtraPropertyName);
                Utility.FileOperationWithRetry(() => 
                {
                    File.WriteAllText(ManifestFile.ItemSpec, manifest.ToString());
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
