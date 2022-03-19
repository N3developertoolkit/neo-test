using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Neo.BuildTasks
{
    public abstract class DotNetToolTask : Task
    {
        internal enum ToolType { Local, Global }

        protected abstract string Command { get; }
        protected abstract string PackageId { get; }
        protected abstract string WorkingDirectory { get; }

        protected abstract string GetArguments();

        public override bool Execute()
        {
            var packageId = PackageId;
            var directory = WorkingDirectory;
            if (FindTool(packageId, directory, out var toolType, out var version))
            {
                Log.LogWarning($"{packageId} {toolType} tool ({version})");

                var command = toolType == ToolType.Global ? Command : "dotnet";
                var arguments = toolType == ToolType.Global 
                    ? GetArguments() 
                    : $"{Command} {GetArguments()}";

                Log.LogWarning($"Running '{command}' '{arguments}' in {directory}");

                if (TryExecute(command, arguments, directory, out var output))
                {
                    foreach (var o in output)
                    {
                        Log.LogWarning($"{Command}: {o}");
                    }
                }

                return true;
            }

            Log.LogError($"Could not locate {packageId} tool package");
            return false;
        }

        internal bool FindTool(string package, string directory, out ToolType toolType, out string version)
        {
            if (TryExecute("dotnet", "tool list --local", directory, out var output)
                && ContainsPackage(output, package, out version))
            {
                toolType = ToolType.Local;
                return true;
            }

            if (TryExecute("dotnet", "tool list --global", directory, out output)
                && ContainsPackage(output, package, out version))
            {
                toolType = ToolType.Global;
                return true;
            }

            toolType = ToolType.Local;
            version = string.Empty;
            return false;
        }


        internal bool TryExecute(string command, string arguments, string directory, out IReadOnlyCollection<string> output)
        {
            var results = ProcessRunner.Run(command, arguments, directory);

            var success = true;
            if (results.ExitCode != 0)
            {
                Log.LogError($"{command} {arguments} returned {results.ExitCode}");
                success = false;
            }

            if (results.Error.Any())
            {
                success = false;
            }

            if (!success)
            {
                foreach (var error in results.Error)
                {
                    Log.LogError(error);
                }
                foreach (var o in results.Output)
                {
                    Log.LogWarning(o);
                }
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
