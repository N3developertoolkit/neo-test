using System;
using System.Collections.Generic;
using System.IO;
using Neo.Collector.Models;

namespace Neo.Collector.Formats
{
    class RawCoverageFormat : ICoverageFormat
    {
        public void WriteReport(IEnumerable<ContractCoverage> coverage, Action<string, Action<Stream>> writeAttachement)
        {
            foreach (var contract in coverage)
            {
                var filename = $"{contract.DebugInfo.Hash}.coverage.txt";
                writeAttachement(filename, stream =>
                {
                    var writer = new StreamWriter(stream);
                    foreach (var (address, count) in contract.Hits)
                    {
                        if (contract.BranchHitMap.TryGetValue(address, out var branchHits))
                        {
                            writer.WriteLine($"{address} {branchHits.BranchCount} {branchHits.ContinueCount}");
                        }
                        else
                        {
                            writer.WriteLine($"{address} {count}");
                        }
                    }
                    writer.Flush();
                });
            }
        }
    }
}