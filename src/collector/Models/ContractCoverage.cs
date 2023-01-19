using System.Collections.Generic;
using System.Linq;

namespace Neo.Collector.Models
{
    partial class ContractCoverage
    {
        public readonly string Name;
        public readonly NeoDebugInfo DebugInfo; 
        public readonly IReadOnlyDictionary<int, uint> HitMap;
        public readonly IReadOnlyDictionary<int, (uint BranchCount, uint ContinueCount)> BranchHitMap;

        public IEnumerable<(int Address, uint Count)> Hits => HitMap
            .Select(kvp => (kvp.Key, kvp.Value))
            .OrderBy(h => h.Key);

        public ContractCoverage(string name, NeoDebugInfo debugInfo, IReadOnlyDictionary<int, uint> hitMap, IReadOnlyDictionary<int, (uint, uint)> branchHitMap)
        {
            Name = name;
            DebugInfo = debugInfo;
            HitMap = hitMap;
            BranchHitMap = branchHitMap;
        }
    }
}
