using System.Collections.Generic;
using System.Linq;

namespace Neo.Collector.Models
{
    class ContractCoverage
    {
        public readonly string Name;
        public readonly NeoDebugInfo DebugInfo; 
        public readonly IReadOnlyDictionary<int, Instruction> InstructionMap;
        public readonly IReadOnlyDictionary<int, uint> HitMap;
        public readonly IReadOnlyDictionary<int, (uint BranchCount, uint ContinueCount)> BranchHitMap;

        public ContractCoverage(string name, NeoDebugInfo debugInfo, IReadOnlyDictionary<int, Instruction> instructionMap, IReadOnlyDictionary<int, uint> hitMap, IReadOnlyDictionary<int, (uint, uint)> branchHitMap)
        {
            Name = name;
            InstructionMap = instructionMap;
            DebugInfo = debugInfo;
            HitMap = hitMap;
            BranchHitMap = branchHitMap;
        }
    }
}
