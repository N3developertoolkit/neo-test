namespace Neo.Collector.Models
{
    partial class ContractCoverage
    {
        public struct BranchCoverage
        {
            public readonly int Address;
            public readonly uint BranchCount;
            public readonly uint ContinueCount;

            public BranchCoverage(int address, (uint branchCount, uint continueCount) counts)
            {
                Address = address;
                BranchCount = counts.branchCount;
                ContinueCount = counts.continueCount;
            }
        }
    }
}
