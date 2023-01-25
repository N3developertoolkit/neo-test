using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Neo.Collector.Models;

namespace Neo.Collector
{
    static class Utility
    {
        // Note, some file systems are case sensitive. 
        // Using StringComparison.OrdinalIgnoreCase could lead to incorrect base names on such systems. 
        public static string GetBaseName(string path, string suffix, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            path = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(suffix)
                && path.EndsWith(suffix, comparison))
            {
                return path.Substring(0, path.Length - suffix.Length);
            }
            return path;
        }

        public static bool TryLoadAssembly(string path, out Assembly assembly)
        {
            if (File.Exists(path))
            {
                try
                {
                    assembly = Assembly.LoadFrom(path);
                    return true;
                }
                catch { }
            }

            assembly = default;
            return false;
        }


        public static decimal CalculateHitRate(uint lineCount, uint hitCount)
            => lineCount == 0 ? 1m : new decimal(hitCount) / new decimal(lineCount);

        // public static (uint branchCount, uint branchHit) CalculateBranchRate(
        //     IEnumerable<(int address, OpCode opCode)> lines, Func<int, (uint, uint)> hitFunc)
        // {
        //     var branchCount = 0u;
        //     var branchHit = 0u;
        //     foreach (var (address, _) in lines)
        //     {
        //         var (branchHitCount, continueHitCount) = hitFunc(address);
        //         branchCount += 2;
        //         branchHit += branchHitCount == 0 ? 0u : 1u;
        //         branchHit += continueHitCount == 0 ? 0u : 1u;

        //     }
        //     return (branchCount, branchHit);
        // }
    }
}