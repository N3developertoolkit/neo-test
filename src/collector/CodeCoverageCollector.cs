using System;
using System.Collections.Generic;
using System.IO;
using Neo.Collector.Models;

namespace Neo.Collector
{
    // Testable version of CodeCoverageDataCollector
    class CodeCoverageCollector
    {
        internal const string COVERAGE_FILE_EXT = ".neo-coverage";
        internal const string SCRIPT_FILE_EXT = ".neo-script";

        readonly ILogger logger;
        readonly IDictionary<Hash160, ContractCoverage> coverageMap = new Dictionary<Hash160, ContractCoverage>();
        int rawCoverageFileCount = 0;

        public CodeCoverageCollector(ILogger logger)
        {
            this.logger = logger;
        }

        public void TrackContract(string contractName, NeoDebugInfo debugInfo)
        {
            if (!coverageMap.ContainsKey(debugInfo.Hash))
            {
                coverageMap.Add(debugInfo.Hash, new ContractCoverage(contractName, debugInfo));
            }
        }

        public void LoadSessionOutput(string filename)
        {
            var ext = Path.GetExtension(filename);
            switch (ext)
            {
                case COVERAGE_FILE_EXT:
                    using (var stream = File.OpenRead(filename))
                    {
                        LoadRawCoverage(stream);
                    }
                    break;
                case SCRIPT_FILE_EXT:
                    if (Hash160.TryParse(Path.GetFileNameWithoutExtension(filename), out var hash))
                    {
                        using (var stream = File.OpenRead(filename))
                        {
                            LoadScript(hash, stream);
                        }
                    }
                    break;
                default: 
                    logger.LogWarning($"Invalid Session Output extension {ext}");
                    break;
            }
        }

        internal void LoadRawCoverage(Stream stream)
        {
            rawCoverageFileCount++;

            var reader = new StreamReader(stream);
            var hash = Hash160.Zero;
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (line.StartsWith("0x"))
                {
                    hash = Hash160.TryParse(line.Trim(), out var value)
                        ? value
                        : Hash160.Zero;
                }
                else
                {
                    if (hash != Hash160.Zero
                        && coverageMap.TryGetValue(hash, out var coverage))
                    {
                        var values = line.Trim().Split(' ');
                        if (values.Length > 0
                            && int.TryParse(values[0].Trim(), out var ip))
                        {
                            if (values.Length == 1)
                            {
                                coverage.RecordHit(ip);
                            }
                            else if (values.Length == 3
                                && int.TryParse(values[1].Trim(), out var offset)
                                && int.TryParse(values[2].Trim(), out var branchResult))
                            {
                                coverage.RecordBranch(ip, offset, branchResult);
                            }
                            else
                            {
                                throw new InvalidDataException($"Invalid raw coverage data line '{line}'");
                            }
                        }
                    }
                }
            }
        }

        internal void LoadScript(Hash160 hash, Stream stream)
        {
            if (coverageMap.TryGetValue(hash, out var coverage))
            {
                coverage.RecordScript(stream.EnumerateInstructions());
            }
        }
    }
}