using System.IO;
using System.Text;
using Microsoft.Build.Framework;

namespace Neo.BuildTasks
{
    // https://ithrowexceptions.com/2020/08/04/implementing-and-debugging-custom-msbuild-tasks.html
    // https://maheep.wordpress.com/2017/05/22/msbuild-writing-a-custom-task/
    // https://natemcmaster.com/blog/2017/07/05/msbuild-task-in-nuget/
    // https://natemcmaster.com/blog/tags/#msbuild


    public class NeoExpressBatch : DotNetToolTask
    {
        const string PACKAGE_ID = "Neo.Express";
        const string COMMAND = "neoxp";

        protected override string Command => COMMAND;
        protected override string PackageId => PACKAGE_ID;
        protected override string WorkingDirectory
            => Path.GetDirectoryName(BuildEngine.ProjectFileOfTaskNode);

        [Required]
        public string BatchFile { get; set; } = "";

        public string InputFile { get; set; } = "";

        public bool Reset { get; set; }

        public string Checkpoint { get; set; } = "";

        public bool Trace { get; set; }

        protected override string GetArguments()
        {
            var batchPath = Path.Combine(WorkingDirectory, BatchFile);
            var inputPath = Path.Combine(WorkingDirectory, 
                string.IsNullOrEmpty(InputFile) 
                    ? "default.neo-express" 
                    : InputFile);

            var builder = new StringBuilder("batch ");
            builder.AppendFormat("\"{0}\" --input \"{1}\"", batchPath, inputPath);

            if (Reset)
            {
                builder.Append(" --reset");
                if (!string.IsNullOrEmpty(Checkpoint))
                {
                    builder.AppendFormat(":\"{0}\"", Path.Combine(WorkingDirectory, Checkpoint));
                }
            }

            if (Trace) { builder.Append(" --trace"); }

            return builder.ToString();
        }
    }
}
