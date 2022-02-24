using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;

namespace Neo.BuildTasks
{
    public class NeoCompilerCSharp : DotNetToolTask
    {
        const string PackageName = "neo.compiler.csharp";
        const string ExeName = "nccs";

        [Required]
        public ITaskItem[] Sources { get; set; } = Array.Empty<ITaskItem>();

        public byte AddressVersion { get; set; } = 53;
        public string ContractName { get; set; } = string.Empty;
        public bool DisableInlining { get; set; } = false;
        public bool DisableOptimizations { get; set; } = false;
        public bool GenerateAssembly { get; set; } = false;
        public bool GenerateDebugInfo { get; set; } = false;
        public ITaskItem? OutputDirectory { get; set; }
        public ITaskItem? WorkingDirectory { get; set; }

        public override bool Execute()
        {
            try
            {
                if (Sources.Length <= 0) throw new Exception();
                foreach (var source in Sources)
                {
                    if (!File.Exists(source.ItemSpec)) throw new Exception();
                }
                var builder = new System.Text.StringBuilder();
                for (int i = 0; i < Sources.Length; i++)
                    builder.Append($" {Sources[i].ItemSpec}");
                if (OutputDirectory is not null)
                    builder.Append($" --output {OutputDirectory.ItemSpec}");
                if (!string.IsNullOrEmpty(ContractName))
                    builder.Append($" --contract-name {ContractName}");
                if (GenerateDebugInfo) builder.Append(" --debug");
                if (GenerateAssembly) builder.Append(" --assembly");
                if (DisableOptimizations) builder.Append(" --no-optimize");
                if (DisableInlining) builder.Append(" --no-inline");
                builder.Append($" --address-version {AddressVersion}");

                var workingDir = WorkingDirectory?.ItemSpec
                    ?? OutputDirectory?.ItemSpec
                    ?? Path.GetDirectoryName(Sources[0].ItemSpec);
                var results = RunTool(PackageName, ExeName, builder.ToString(), workingDir);

            }
            catch (Exception ex)
            {
                Log.LogError(ex.Message);
            }

            return !Log.HasLoggedErrors;
        }
    }
}
