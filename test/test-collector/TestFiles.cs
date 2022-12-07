using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Neo.Collector;
using Neo.Collector.Models;

namespace test_collector;

static class TestFiles
{
    public static void LoadTestContract(this CodeCoverageCollector @this, string contractName, string debugInfoFileName)
    {
        var debugInfo = GetResource(debugInfoFileName, stream =>
        {
            using var reader = new StreamReader(stream);
            var json = SimpleJSON.JSON.Parse(reader.ReadToEnd());
            return NeoDebugInfo.FromDebugInfoJson(json);
        });
        @this.TrackContract(contractName, debugInfo);
    }

    public static void LoadTestOutput(this CodeCoverageCollector @this, string dirName)
    {
        foreach (var file in GetResourceNames(dirName))
        {
            using var stream = GetResourceStream(file);
            var ext = Path.GetExtension(file);
            switch (ext)
            {
                case CodeCoverageCollector.COVERAGE_FILE_EXT:
                    @this.LoadRawCoverage(stream);
                    break;
                case CodeCoverageCollector.SCRIPT_FILE_EXT:
                    {
                        var array = file.Split('.');
                        @this.LoadScript(Hash160.Parse(array[^2]), stream);
                    }
                    break;
                case CodeCoverageCollector.NEF_FILE_EXT:
                    {
                        var array = file.Split('.');
                        @this.LoadNef(Hash160.Parse(array[^2]), stream);
                    }
                    break;
                default:
                    break;
            }
        }
    }

    public static T GetResource<T>(string name, Func<Stream, T> convertFunc)
    {
        using var stream = GetResourceStream(name);
        return convertFunc(stream);
    }

    public static Stream GetResourceStream(string name)
    {
        var assembly = typeof(TestFiles).Assembly;
        var stream = assembly.GetManifestResourceStream(name);
        if (stream is not null)
        {
            return stream;
        }

        var resourceName = assembly.GetManifestResourceNames()
            .SingleOrDefault(n => n.EndsWith(name, StringComparison.OrdinalIgnoreCase));
        stream = string.IsNullOrEmpty(resourceName) ? null : assembly.GetManifestResourceStream(resourceName);
        return stream ?? throw new FileNotFoundException();
    }

    public static IEnumerable<string> GetResourceNames(string dirName = "")
    {
        var assembly = typeof(TestFiles).Assembly;
        var names = assembly.GetManifestResourceNames();
        return string.IsNullOrEmpty(dirName)
            ? names
            : names.Where(n => n.Contains(dirName, StringComparison.OrdinalIgnoreCase));
    }
}