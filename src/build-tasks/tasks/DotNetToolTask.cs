using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Utilities;
using SemVersion;

namespace Neo.BuildTasks
{

    public abstract class DotNetToolTask : Task
    {
        protected const string DotNetExe = "dotnet";

        protected record struct ToolPackage(string Name, SemanticVersion Version);

        protected ProcessRunResults RunTool(string packageName, string exeName, string args, string workingDir)
        {
            return GetLocalToolVersion(packageName, workingDir) is not null
                ? ProcessRunner.Run(DotNetExe, $"{exeName} {args}", workingDir)
                : GetGlobalToolVersion(packageName) is not null
                    ? ProcessRunner.Run(exeName, args)
                    : throw new Exception();
        }
        protected SemanticVersion? GetGlobalToolVersion(string tool)
        {
            return GetToolPackages(string.Empty)
                .SingleOrDefault(p => p.Name.Equals(tool, StringComparison.OrdinalIgnoreCase))
                .Version;
        }

        protected SemanticVersion? GetLocalToolVersion(string tool, string workingDirectory)
        {
            if (!Directory.Exists(workingDirectory)) throw new ArgumentException(nameof(workingDirectory));

            return GetToolPackages(workingDirectory)
                .SingleOrDefault(p => p.Name.Equals(tool, StringComparison.OrdinalIgnoreCase))
                .Version;
        }

        static IEnumerable<ToolPackage> GetToolPackages(string workingDirectory)
        {
            var args = $"tool list {(string.IsNullOrEmpty(workingDirectory) ? "--global" : "--local")}";
            var results = ProcessRunner.Run(DotNetExe, args, workingDirectory);
            return ParseTable(results.Output)
                .Where(row => row.Count > 0)
                .Where(row => !IsSeparator(row))
                .Skip(1)
                .Select(ParseToolPackage);
        }

        static ToolPackage ParseToolPackage(IReadOnlyList<string> row)
        {
            if (row.Count < 2) throw new ArgumentException(nameof(row));
            return new ToolPackage(row[0], SemanticVersion.Parse(row[1]));
        }

        static IEnumerable<IReadOnlyList<string>> ParseTable(IReadOnlyList<string> table)
        {
            for (int i = 0; i < table.Count; i++)
            {
                yield return ParseTableRow(table[i]);
            }
        }

        static bool IsSeparator(IReadOnlyList<string> row)
        {
            if (row.Count != 1) return false;
            var col = row[0];
            for (var i = 0; i < col.Length; i++)
            {
                if (col[i] != '-') return false;
            }
            return true;
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
