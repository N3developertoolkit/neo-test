using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Neo.BuildTasks
{
    public class NeoCompilerCSharp : DotNetToolTask
    {
        const string PackageName = "neo.compiler.csharp";
        const string Command = "nccs";

        [Required] public ITaskItem[] Sources { get; set; } = Array.Empty<ITaskItem>();
        public byte AddressVersion { get; set; } = 53;
        public string ContractName { get; set; } = string.Empty;
        public bool DisableInlining { get; set; } = false;
        public bool DisableOptimizations { get; set; } = false;
        public bool GenerateAssembly { get; set; } = false;
        public bool GenerateDebugInfo { get; set; } = false;
        public ITaskItem? OutputDirectory { get; set; }
        public ITaskItem? WorkingDirectory { get; set; }

        protected override TaskDetails GetTaskDetails()
        {
            if (Sources.Length <= 0) throw new Exception("Empty Sources Collection");
            for (int i = 0; i < Sources.Length; i++)
            {
                if (!File.Exists(Sources[i].ItemSpec)) 
                    throw new FileNotFoundException("", Sources[i].ItemSpec);
            }

            var argsBuilder = new StringBuilder();
            for (int i = 0; i < Sources.Length; i++)
                argsBuilder.Append($" {Sources[i].ItemSpec}");
            if (OutputDirectory is not null)
                argsBuilder.Append($" --output {OutputDirectory.ItemSpec}");
            if (!string.IsNullOrEmpty(ContractName))
                argsBuilder.Append($" --contract-name {ContractName}");
            if (GenerateDebugInfo) argsBuilder.Append(" --debug");
            if (GenerateAssembly) argsBuilder.Append(" --assembly");
            if (DisableOptimizations) argsBuilder.Append(" --no-optimize");
            if (DisableInlining) argsBuilder.Append(" --no-inline");
            argsBuilder.Append($" --address-version {AddressVersion}");

            var workingDir = WorkingDirectory?.ItemSpec
                ?? OutputDirectory?.ItemSpec
                ?? Path.GetDirectoryName(Sources[0].ItemSpec);

            return new TaskDetails(PackageName, Command, argsBuilder.ToString(), workingDir);
        }
    }
}
