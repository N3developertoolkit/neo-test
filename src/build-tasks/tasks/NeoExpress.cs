using System;
using System.Collections;
using System.IO;
using System.Text;
using Microsoft.Build.Framework;

namespace Neo.BuildTasks
{
    public class NeoExpress : DotNetToolTask
    {
        const string PackageName = "neo.express";
        const string Command = "neoxp";

        [Required] public ITaskItem BatchFile { get; set; } = default!;
        [Required] public ITaskItem ExpressFile { get; set; } = default!;
        public bool Reset { get; set; } = false;
        public ITaskItem? ResetCheckpoint { get; set; }
        public bool Trace { get; set; } = false;
        public ITaskItem? WorkingDirectory { get; set; }

        protected override TaskDetails GetTaskDetails()
        {
            if (ExpressFile is null || !File.Exists(ExpressFile.ItemSpec))
                throw new Exception();
            if (BatchFile is null || !File.Exists(BatchFile.ItemSpec))
                throw new Exception();

            var argsBuilder = new StringBuilder($"batch {BatchFile.ItemSpec} --input {ExpressFile.ItemSpec}");
            if (Trace)
            {
                argsBuilder.Append(" --trace");
            }

            if (Reset)
            {
                if (ResetCheckpoint is null)
                {
                    argsBuilder.Append(" --reset");
                }
                else
                {
                    if (!File.Exists(ResetCheckpoint.ItemSpec)) throw new FileNotFoundException("", ResetCheckpoint.ItemSpec);
                    argsBuilder.Append($" --reset:{ResetCheckpoint.ItemSpec}");
                }
            }

            var workingDir = WorkingDirectory?.ItemSpec ?? Path.GetDirectoryName(ExpressFile.ItemSpec);
            return new TaskDetails(PackageName, Command, argsBuilder.ToString(), workingDir);
        }
    }
}
