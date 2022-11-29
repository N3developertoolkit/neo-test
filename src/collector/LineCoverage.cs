using System.Collections.Generic;
using Neo.Collector.Models;

namespace Neo.Collector
{
    using SequencePoint = NeoDebugInfo.SequencePoint;

    class LineCoverage
    {
        readonly SequencePoint sequencePoint;
        public readonly uint HitCount;
        public readonly IReadOnlyList<BranchCoverage> Branches;

        public int Address => sequencePoint.Address;
        public (int Line, int Column) Start => sequencePoint.Start;

        public LineCoverage(SequencePoint sequencePoint, uint hitCount, IReadOnlyList<BranchCoverage> branches)
        {
            this.sequencePoint = sequencePoint;
            HitCount = hitCount;
            Branches = branches;
        }
    }
}