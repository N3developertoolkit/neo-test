using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Neo.Collector.Models;
using System.Diagnostics.CodeAnalysis;

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

        public static bool TryLoadAssembly(string path, [MaybeNullWhen(false)] out Assembly assembly)
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
    }
}