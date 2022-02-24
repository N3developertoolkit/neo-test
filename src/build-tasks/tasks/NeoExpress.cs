using System;
using System.Collections;
using System.IO;
using Microsoft.Build.Framework;

namespace Neo.BuildTasks
{
    public class NeoExpress : DotNetToolTask
    {
        const string PackageName = "neo.express";
        const string ExeName = "neoxp";

        [Required] public ITaskItem BatchFile { get; set; } = default!;
        [Required] public ITaskItem ExpressFile { get; set; } = default!;

        public bool Reset { get; set; } = false;
        public ITaskItem? ResetCheckpoint { get; set; }
        public bool Trace { get; set; } = false;
        public ITaskItem? WorkingDirectory { get; set; }

        public override bool Execute()
        {
            try
            {
                if (ExpressFile is null || !File.Exists(ExpressFile.ItemSpec))
                    throw new Exception();
                if (BatchFile is null || !File.Exists(BatchFile.ItemSpec))
                    throw new Exception();

                var args = $"batch {BatchFile.ItemSpec} --input {ExpressFile.ItemSpec}";
                if (Trace)
                {
                    args += " --trace";
                }

                if (Reset)
                {
                    args += " --reset";
                    if (ResetCheckpoint is not null)
                    {
                        if (!File.Exists(ResetCheckpoint.ItemSpec)) throw new Exception();
                        args += $":{ResetCheckpoint.ItemSpec}";
                    }
                }

                var workingDir = WorkingDirectory?.ItemSpec 
                    ?? Path.GetDirectoryName(ExpressFile.ItemSpec);

                var results = RunTool(PackageName, ExeName, args, workingDir);
            }
            catch (Exception ex)
            {
                Log.LogError(ex.Message);
            }

            return !Log.HasLoggedErrors;
        }
    }
}
