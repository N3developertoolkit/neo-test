using System;
using System.Collections.Generic;
using System.Threading;

namespace Neo.BuildTasks
{
    class ProcessRunner
    {
        public record struct Results(int ExitCode, IReadOnlyList<string> Output, IReadOnlyList<string> Error);

        public static Results Run(string command, string arguments, string workingDirectory = "")
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo(command, arguments)
            {
                UseShellExecute = false,
                // RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = string.IsNullOrEmpty(workingDirectory) ? null : workingDirectory,
            };

            var process = new System.Diagnostics.Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true,
            };

            List<string> output = new();
            process.OutputDataReceived += (sender, args) => { if (args.Data != null) { output.Add(args.Data); } };

            List<string> error = new();
            process.ErrorDataReceived += (sender, args) => { if (args.Data != null) { error.Add(args.Data); } };

            ManualResetEvent completeEvent = new(false);
            process.Exited += (sender, args) => { completeEvent.Set(); };

            if (!process.Start()) throw new Exception("Process failed to start");
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            completeEvent.WaitOne();

            return new Results(process.ExitCode, output, error);
        }
    }
}
