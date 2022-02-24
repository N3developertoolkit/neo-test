using System;
using System.Collections.Generic;
using System.Linq;
using SemVersion;

namespace Neo.BuildTasks
{
    class ProcessRunnerException : Exception
    {
        public string Command { get; }
        public string Args { get; }
        public ProcessRunner.Results Results { get; }

        public ProcessRunnerException(string command, string args, ProcessRunner.Results results) 
        {
            Command = command;
            Args = args;
            Results = results;
        }
    }

    class DotNetToolRunner
    {
        const string DotNetExe = "dotnet";

        public delegate ProcessRunner.Results ProcessRunnerFunc(string command, string args, string workingDirectory);

        ProcessRunnerFunc processRunner;

        public DotNetToolRunner()
        {
            this.processRunner = ProcessRunner.Run;
        }

        public DotNetToolRunner(ProcessRunnerFunc processRunner)
        {
            this.processRunner = processRunner;
        }

        public IReadOnlyList<string> Run(string packageName, string command, string args, string workingDirectory, Action<string>? logCommandLine = null)
        {
            if (TryGetLocalTool(packageName, workingDirectory, out _))
            {
                logCommandLine?.Invoke($"{DotNetExe} {command} {args}");
                var results = processRunner(DotNetExe, $"{command} {args}", workingDirectory);
                if (results.ExitCode != 0) throw new ProcessRunnerException(DotNetExe, $"{command} {args}", results);
                return results.Output;
            }

            if (TryGetGlobalTool(packageName, out _))
            {
                logCommandLine?.Invoke($"{command} {args}");
                var results = processRunner(command, args, string.Empty);
                if (results.ExitCode != 0) throw new ProcessRunnerException(command, args, results);
                return results.Output;
            }

            throw new Exception($"Could not locate {packageName} tool package");
        }

        internal bool TryGetGlobalTool(string packageName, out (string Name, SemanticVersion Version) package)
            => TryGetTool(DotNetExe, "tool list --global", string.Empty, packageName, out package);

        internal bool TryGetLocalTool(string packageName, string workingDirectory, out (string Name, SemanticVersion Version) package)
            => TryGetTool(DotNetExe, "tool list --local", workingDirectory, packageName, out package);

        internal bool TryGetTool(string command, string args, string workingDirectory, string packageName, out (string Name, SemanticVersion Version) package)
        {
            var results = processRunner(command, args, workingDirectory);
            if (results.ExitCode != 0) throw new Exception($"\"{command} {args}\" returned exit code {results.ExitCode}");
            return DotNetToolRunner.TryGetTool(results.Output, packageName, out package);
        }

        internal static bool TryGetTool(IReadOnlyList<string> output, string packageName, out (string Name, SemanticVersion Version) package)
        {
            foreach (var tool in ParseToolPackageTable(output))
            {
                if (tool.Name.Equals(packageName, StringComparison.OrdinalIgnoreCase))
                {
                    package = tool;
                    return true;
                }
            }

            package = default;
            return false;
        }

        internal static IEnumerable<(string Name, SemanticVersion Version)> ParseToolPackageTable(IReadOnlyList<string> output)
        {
            return ParseTable(output)
                .Skip(1)
                .Select(row => 
                {
                    if (row.Count < 2) throw new ArgumentException(nameof(row));
                    return (row[0], SemanticVersion.Parse(row[1]));
                });
        }

        internal static IEnumerable<IReadOnlyList<string>> ParseTable(IReadOnlyList<string> output)
        {
            for (int i = 0; i < output.Count; i++)
            {
                var row = output[i].Trim();
                if (row.Length == 0) continue;
                if (row.All(c => c == '-')) continue;
                yield return ParseTableRow(row);
            }

            static IReadOnlyList<string> ParseTableRow(string row)
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
}
