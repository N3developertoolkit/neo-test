using System;
using Microsoft.Build.Utilities;

namespace Neo.BuildTasks
{
    public abstract class DotNetToolTask : Task
    {
        protected record struct TaskDetails(string PackageName, string Command, string Args, string WorkingDirectory);

        protected abstract TaskDetails GetTaskDetails();

        public override bool Execute()
        {
            try
            {
                var details = GetTaskDetails();

                var runner = new DotNetToolRunner();
                var output = runner.Run(details.PackageName, details.Command, details.Args, details.WorkingDirectory);

                for (int i = 0; i < output.Count; i++)
                {
                    Log.LogMessage(output[i]);
                }

            }
            catch (ProcessRunnerException ex)
            {
                Log.LogError($"{ex.Command} {ex.Args} returned {ex.Results.ExitCode}");
                foreach (var e in ex.Results.Error)
                {
                    Log.LogError(e);
                }
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex);
            }

            return !Log.HasLoggedErrors;
        }
    }
}
