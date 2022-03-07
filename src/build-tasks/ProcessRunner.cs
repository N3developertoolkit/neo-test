using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.BuildTasks
{
    // https://github.com/jamesmanning/RunProcessAsTask
    class ProcessRunner
    {
        public record struct Results(int ExitCode, IReadOnlyList<string> Output, IReadOnlyList<string> Error);

        static void Run(string command, string arguments, string workingDirectory, Action<Results> onExited)
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

            process.Exited += (sender, args) => {
                onExited(new Results(process.ExitCode, output, error)); 
            };

            if (!process.Start()) throw new Exception("Process failed to start");
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }

        public static Task<Results> RunAsync(string command, string arguments, string workingDirectory = "")
        {
            TaskCompletionSource<Results> tcs = new();
            Run(command, arguments, workingDirectory, results => tcs.TrySetResult(results));
            return tcs.Task; 
        }

        public static Results Run(string command, string arguments, string workingDirectory = "")
        {
            ManualResetEvent completeEvent = new(false);
            Results results = default;
            Run(command, arguments, workingDirectory, r => 
            {
                results = r;
                completeEvent.Set();
            });
            completeEvent.WaitOne();
            return results;
        }
    }
}
