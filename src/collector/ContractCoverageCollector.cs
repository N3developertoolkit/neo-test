using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Neo.Collector.Models;
using static Neo.Collector.Models.ContractCoverage;

namespace Neo.Collector
{
    class ContractCoverageCollector
    {
        readonly string contractName;
        readonly NeoDebugInfo debugInfo;
        readonly Dictionary<int, uint> hitMap = new Dictionary<int, uint>();
        readonly Dictionary<int, (uint branchCount, uint continueCount)> branchMap = new Dictionary<int, (uint branchCount, uint continueCount)>();
        IReadOnlyDictionary<int, Instruction> instructionMap = null;

        public Hash160 ScriptHash => debugInfo.Hash;
        public IReadOnlyDictionary<int, uint> HitMap => hitMap;
        public IReadOnlyDictionary<int, (uint branchCount, uint continueCount)> BranchMap => branchMap;

        public ContractCoverageCollector(string contractName, NeoDebugInfo debugInfo)
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
            if (!(this.instructionMap is null))
            {
                throw new InvalidOperationException($"RecordScript already called for {contractName}");
            }

            var instructionMap = new SortedDictionary<int, Instruction>();
            foreach (var (address, instruction) in instructions)
            {
                instructionMap.Add(address, instruction);
            }
            this.instructionMap = instructionMap;
        }

        public ContractCoverage CollectCoverage() 
            => new ContractCoverage(contractName, debugInfo, instructionMap, hitMap, branchMap);

        // MethodCoverage CollectMethodCoverage(NeoDebugInfo.Method method)
        // {
        //     var doc = method.SequencePoints.Select(sp => debugInfo.Documents[sp.Document]).FirstOrDefault();
        //     var lines = new List<LineCoverage>(method.SequencePoints.Count);
        //     for (int i = 0; i < method.SequencePoints.Count; i++)
        //     {
        //         var sp = method.SequencePoints[i];
        //         var hitCount = hitMap.TryGetValue(sp.Address, out var _hitCount)
        //             ? _hitCount
        //             : 0;

        //         var nextSPAddress = i + 1 < method.SequencePoints.Count
        //             ? method.SequencePoints[i + 1].Address
        //             : int.MaxValue;
        //         var paths = FindPaths(sp.Address, methodEnd: method.Range.End, nextSPAddress: nextSPAddress).ToList();
        //         if (paths.Count == 0) throw new InvalidOperationException();
        //         if (paths.Count == 1)
        //         {
        //             lines.Add(new LineCoverage(sp, hitCount, Array.Empty<BranchCoverage>()));
        //         }
        //         else
        //         {

        //         }




        //         // while (address <= method.Range.End && address < nextSPAddress)
        //         // {
        //         //     var ins = instructions[address];
        //         //     if (ins.IsBranchInstruction())
        //         //     {
        //         //         var counts = branchMap.TryGetValue(address, out var _counts) ? _counts : (0, 0);
        //         //         branches.Add(new BranchCoverage(address, counts));
        //         //     }
        //         //     address += ins.Size;
        //         // }

                
        //     }
        //     return new MethodCoverage(method, doc, lines);
        // }

        // static int CalculateHash(IEnumerable<int> path)
        // {
        //     unchecked // Overflow is fine, just wrap
        //     {
        //         int hash = 17;
        //         foreach (var value in path)
        //         {
        //             hash = hash * 23 + value.GetHashCode();
        //         }
        //         return hash;
        //     }
        // }

        internal IEnumerable<ImmutableQueue<int>> FindPaths(int address, ImmutableQueue<int> path = null, int methodEnd = int.MaxValue, int nextSPAddress = int.MaxValue)
        {
            var maxAddress = instructionMap.Keys.Max();
            path = path is null ? ImmutableQueue<int>.Empty : path;

            while (true)
            {
                var ins = address <= maxAddress 
                    ? instructionMap[address] 
                    : new Instruction(OpCode.RET);

                if (ins.IsBranchInstruction())
                {
                }
                
                if (ins.IsCallInstruction())
                {
                    var offset = ins.GetCallOffset();
                    var paths = Enumerable.Empty<ImmutableQueue<int>>();
                    foreach (var callPath in FindPaths(address + offset))
                    {
                        var tempPath = path;
                        foreach (var item in callPath)
                        {
                            tempPath = tempPath.Enqueue(item);
                        }
                        paths = paths.Concat(FindPaths(address + ins.Size, tempPath, methodEnd, nextSPAddress));
                    }
                    return paths;
                }

                address += ins.Size;
                if (ins.OpCode == OpCode.RET 
                    || address > methodEnd 
                    || address >= nextSPAddress)
                {
                    return Enumerable.Repeat(path, 1);
                }
            }
        }
    }
}
