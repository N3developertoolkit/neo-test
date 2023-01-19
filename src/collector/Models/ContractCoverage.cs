using System.Collections.Generic;
using System.Linq;

namespace Neo.Collector.Models
{
    partial class ContractCoverage
    {
        public readonly string Name;
        public readonly IReadOnlyDictionary<int, Instruction> InstructionMap;
        public readonly NeoDebugInfo DebugInfo; 
        public readonly IReadOnlyDictionary<int, uint> HitMap;
        public readonly IReadOnlyDictionary<int, (uint BranchCount, uint ContinueCount)> BranchHitMap;

        public IEnumerable<(int Address, Instruction Instruction)> Instructions => InstructionMap
            .Select(kvp => (kvp.Key, kvp.Value))
            .OrderBy(h => h.Key);

        public ContractCoverage(string name, IReadOnlyDictionary<int, Instruction> instructionMap, NeoDebugInfo debugInfo, IReadOnlyDictionary<int, uint> hitMap, IReadOnlyDictionary<int, (uint, uint)> branchHitMap)
        {
            Name = name;
            InstructionMap = instructionMap;
            DebugInfo = debugInfo;
            HitMap = hitMap;
            BranchHitMap = branchHitMap;
        }
    }
}
