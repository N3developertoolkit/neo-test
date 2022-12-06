using System;
using System.Collections.Generic;
using System.Linq;
using Neo.Collector.Models;

namespace Neo.Collector
{
    class ContractCoverage
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

        // public void TracePaths(in NeoDebugInfo.Method method)
        // {
        //     if (instructions is null) throw new InvalidOperationException();
        //     // TracePaths()

        // }

        // void TracePaths(int address)
        // {
        //     var ins = instructions[address];
        //     var offset = ins.GetBranchOffset();
        //     if (offset == 0)
        //     {
        //         TracePaths(address)
        //     }
        // }


    }
}