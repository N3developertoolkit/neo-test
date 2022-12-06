using System;
using System.Collections.Generic;
using System.Linq;

namespace Neo.Collector.Models
{
    partial class ContractCoverage
    {
        readonly string contractName;
        readonly NeoDebugInfo debugInfo;
        readonly Dictionary<int, uint> hitMap = new Dictionary<int, uint>();
        readonly Dictionary<int, (uint branchCount, uint continueCount)> branchMap = new Dictionary<int, (uint branchCount, uint continueCount)>();
        IReadOnlyDictionary<int, Instruction> instructions = null;

        public ContractCoverage(string contractName, NeoDebugInfo debugInfo)
        {
            this.contractName = contractName;
            this.debugInfo = debugInfo;
        }

        public void RecordHit(int address)
        {
            var hitCount = hitMap.TryGetValue(address, out var value) ? value : 0;
            hitMap[address] = hitCount + 1;
        }

        public void RecordBranch(int address, int offsetAddress, int branchResult)
        {
            var (branchCount, continueCount) = branchMap.TryGetValue(address, out var value)
                ? value : (0, 0);
            branchMap[address] = branchResult == address
                ? (branchCount, continueCount + 1)
                : branchResult == offsetAddress
                    ? (branchCount + 1, continueCount)
                    : throw new FormatException($"Branch result {branchResult} did not equal {address} or {offsetAddress}");
        }

        public void RecordScript(IEnumerable<(int address, Instruction instruction)> instructions)
        {
            if (!(this.instructions is null))
            {
                throw new InvalidOperationException($"RecordScript already called for {contractName}");
            }

            var _instructions = new SortedDictionary<int, Instruction>();
            foreach (var (address, instruction) in instructions)
            {
                _instructions.Add(address, instruction);
            }
            this.instructions = _instructions;
        }

        public IEnumerable<MethodCoverage> CollectMethodCoverage()
            => debugInfo.Methods.Select(CollectMethodCoverage);

        MethodCoverage CollectMethodCoverage(NeoDebugInfo.Method method)
        {
            var doc = method.SequencePoints.Select(sp => debugInfo.Documents[sp.Document]).FirstOrDefault();
            var lines = new List<LineCoverage>(method.SequencePoints.Count);
            for (int i = 0; i < method.SequencePoints.Count; i++)
            {
                var sp = method.SequencePoints[i];
                var hitCount = hitMap.TryGetValue(sp.Address, out var _hitCount)
                    ? _hitCount
                    : 0;

                var branches = new List<BranchCoverage>();
                var address = sp.Address;
                var nextSPAddress = i + 1 < method.SequencePoints.Count
                    ? method.SequencePoints[i + 1].Address
                    : int.MaxValue;

                while (address <= method.Range.End && address < nextSPAddress)
                {
                    var ins = instructions[address];
                    if (ins.IsBranchInstruction())
                    {
                        var counts = branchMap.TryGetValue(address, out var _counts) ? _counts : (0, 0);
                        branches.Add(new BranchCoverage(address, counts));
                    }
                    address += ins.Size;
                }

                lines.Add(new LineCoverage(sp, hitCount, branches));
            }
            return new MethodCoverage(method, doc, lines);
        }
    }
}
