using System;
using System.IO;
using System.Text;
using Microsoft.Build.Framework;

namespace Neo.BuildTasks
{
    public class NeoCsc : DotNetToolTask
    {
        const string PACKAGE_ID = "Neo.Compiler.CSharp";
        const string COMMAND = "nccs";
        const byte DEFAULT_ADDRESS_VERSION = 53;

        protected override string Command => COMMAND;

        protected override string PackageId => throw new System.NotImplementedException();

        [Required]
        public ITaskItem[] Files { get; set; } = Array.Empty<ITaskItem>();
        public ITaskItem? Output { get; set; }
        public string ContractName { get; set; } = "";
        public bool Debug { get; set; }
        public bool Assembly { get; set; }
        public bool Optimize { get; set; }
        public bool Inline { get; set; }
        public byte AddressVersion { get; set; } = DEFAULT_ADDRESS_VERSION;



        protected override string GetArguments()
        {
            var builder = new StringBuilder();
            foreach (var file in Files)
            {
                builder.AppendFormat(" {0}", file.ItemSpec);
            }

            if (Output is not null)
            {
                builder.AppendFormat(" --Output {0}", Output.ItemSpec);
            }

            if (!string.IsNullOrEmpty(ContractName))
            {
                builder.AppendFormat(" --contract-name {0}", ContractName);
            }

            if (Debug) builder.Append(" --debug");
            if (Assembly) builder.Append(" --assembly");
            if (!Optimize) builder.Append(" --no-optimize");
            if (!Inline) builder.Append(" --no-inline");
            if (AddressVersion != DEFAULT_ADDRESS_VERSION)
            {
                builder.AppendFormat(" --address-version {0}", AddressVersion);
            }

            return builder.ToString();
        }
    }
}
