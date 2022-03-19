using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Neo.BuildTasks
{
    internal enum ToolType { Local, Global }

    public abstract class DotNetToolTask : Task
    {

        protected abstract string Command { get; }
        protected abstract string PackageId { get; }
        protected virtual string WorkingDirectory
            => Path.GetDirectoryName(BuildEngine.ProjectFileOfTaskNode);

        
        ToolType toolType;

        [Output]
        public string ToolType => $"{toolType}";

        [Output]
        public string CommandLine { get; set; } = string.Empty;


        protected abstract string GetArguments();
        protected virtual void ExecutionSuccess(IReadOnlyCollection<string> output)
        {
            foreach (var o in output)
            {
                Log.LogWarning($"{Command}: {o}");
            }
        }

        public override bool Execute()
        {
            var packageId = PackageId;
            var directory = WorkingDirectory;
            if (FindTool(packageId, directory, out var toolType, out var version))
            {
                this.toolType = toolType;
                Log.LogWarning($"{packageId} {toolType} tool ({version})");

                var command = toolType == Neo.BuildTasks.ToolType.Global ? Command : "dotnet";
                var arguments = toolType == Neo.BuildTasks.ToolType.Global
                    ? GetArguments()
                    : $"{Command} {GetArguments()}";

                CommandLine = $"{command} {arguments}";
                Log.LogWarning($"Running '{command}' '{arguments}' in {directory}");

                if (TryExecute(command, arguments, directory, out var output))
                {
                    ExecutionSuccess(output);
                    return true;
                }

                return false;
            }

            Log.LogError($"Could not locate {packageId} tool package");
            return false;
        }

        internal bool FindTool(string package, string directory, out ToolType toolType, out string version)
        {
            if (TryExecute("dotnet", "tool list --local", directory, out var output)
                && ContainsPackage(output, package, out version))
            {
                toolType = Neo.BuildTasks.ToolType.Local;
                return true;
            }

            if (TryExecute("dotnet", "tool list --global", directory, out output)
                && ContainsPackage(output, package, out version))
            {
                toolType = Neo.BuildTasks.ToolType.Global;
                return true;
            }

            toolType = Neo.BuildTasks.ToolType.Local;
            version = string.Empty;
            return false;
        }


        internal bool TryExecute(string command, string arguments, string directory, out IReadOnlyCollection<string> output)
        {
            var results = ProcessRunner.Run(command, arguments, directory);

            if (results.ExitCode != 0 || results.Error.Any())
            {
                if (results.ExitCode != 0)
                    Log.LogError($"{command} {arguments} returned {results.ExitCode}");
                else
                    Log.LogWarning($"{command} {arguments} returned {results.ExitCode}");

                foreach (var error in results.Error)
                {
                    Log.LogError(error);
                }
                foreach (var o in results.Output)
                {
                    Log.LogWarning(o);
                }

                output = Array.Empty<string>();
                return false;
            }

            foreach (var o in results.Output)
            {
                Log.LogWarning(o);
            }

            output = results.Output;
            return true;
        }

        internal static bool ContainsPackage(IReadOnlyCollection<string> output, string package, out string version)
        {
            foreach (var o in output.Skip(2))
            {
                var row = ParseTableRow(o);
                if (row.Count < 2) continue;
                if (row[0].Equals(package, StringComparison.InvariantCultureIgnoreCase))
                {
                    version = row[1];
                    return true;
                }
            }

            version = string.Empty;
            return false;
        }

        internal static IReadOnlyList<string> ParseTableRow(string row)
        {
            const string ColumnDelimiter = "      ";

            List<string> columns = new();
            while (true)
            {
                row = row.TrimStart();
                if (row.Length == 0) break;
                var index = row.IndexOf(ColumnDelimiter);
                if (index == -1)
                {
                    columns.Add(row);
                    break;
                }
                else
                {
                    columns.Add(row.Substring(0, index).Trim());
                    row = row.Substring(index);
                }
            }
            return columns;
        }
    }
}
