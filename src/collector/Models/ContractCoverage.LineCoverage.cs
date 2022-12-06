using System.Collections.Generic;

namespace Neo.Collector.Models
{
    partial class ContractCoverage
    {
        public struct LineCoverage
        {
            readonly NeoDebugInfo.SequencePoint sequencePoint;
            public readonly uint HitCount;
            public readonly IReadOnlyList<BranchCoverage> Branches;

            public int Address => sequencePoint.Address;
            public (int Line, int Column) Start => sequencePoint.Start;

            public LineCoverage(in NeoDebugInfo.SequencePoint sequencePoint, uint hitCount, IReadOnlyList<BranchCoverage> branches)
            {
                this.sequencePoint = sequencePoint;
                HitCount = hitCount;
                Branches = branches;
            }
        }
    }
}
