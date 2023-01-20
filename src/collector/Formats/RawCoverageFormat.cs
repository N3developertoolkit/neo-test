using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Neo.Collector.Models;

namespace Neo.Collector.Formats
{
    class RawCoverageFormat : ICoverageFormat
    {
        public void WriteReport(IReadOnlyList<ContractCoverage> coverage, Action<string, Action<Stream>> writeAttachement)
        {
            foreach (var contract in coverage)
            {
                var filename = $"{contract.DebugInfo.Hash}.coverage.txt";
                writeAttachement(filename, stream =>
                {
                    var writer = new StreamWriter(stream);
                    var addresses = contract.InstructionMap.Select(kvp => kvp.Key).OrderBy(h => h);
                    foreach (var address in addresses)
                    {
                        if (contract.BranchHitMap.TryGetValue(address, out var branchHits))
                        {
                            writer.WriteLine($"{address} {branchHits.BranchCount} {branchHits.ContinueCount}");
                        }
                        else if (contract.HitMap.TryGetValue(address, out var count))
                        {
                            writer.WriteLine($"{address} {count}");
                        }
                        else
                        {
                            writer.WriteLine($"{address} 0");
                        }
                    }
                    writer.Flush();
                });
            }
        }
    }
}