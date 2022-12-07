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
        IReadOnlyDictionary<int, Instruction> instructions = null;

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

        public ContractCoverage CollectCoverage()
        {
            var methods = debugInfo.Methods.Select(CollectMethodCoverage);
            return new ContractCoverage(contractName, debugInfo.Hash, methods.ToList());
        }

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

        internal IEnumerable<ImmutableStack<int>> CollectBranchPaths(int address) => CollectBranchPaths(ImmutableStack.Create(address));

        IEnumerable<ImmutableStack<int>> CollectBranchPaths(ImmutableStack<int> path)
        {
            if (path.IsEmpty) throw new ArgumentException();
            var address = path.Peek();
            var max = instructions.Keys.Max();

            while (true)
            {
                var ins = address > max ? new Instruction(OpCode.RET) : instructions[address];
                if (ins.IsBranchInstruction())
                {
                    var branchOffset = ins.GetBranchOffset();
                    foreach (var foo in CollectBranchPaths(path.Push(address + branchOffset)))
                    {
                        yield return foo;
                    }

                    path = path.Push(address + ins.Size);

                }
                else if (ins.IsCallInstruction())
                {
                    var callOffset = ins.GetCallOffset();
                

                }
                else if (ins.OpCode == OpCode.RET)
                {
                    yield return path;
                }
                else
                {
                    path = path.Push(address + ins.Size);
                }
                
            }
        }


        // void CollectPath(ImmutableStack<ExecutionContext> stack)
        // {
        //     if (stack.IsEmpty) throw new InvalidOperationException();
        //     var ins = instructions[stack.Peek().Address];
        //     var callOffset = ins.GetCallOffset();
        //     if (callOffset > 0)
        //     {

        //     }
        //     var branchOffset = ins.GetBranchOffset();
        //     if (branchOffset > 0)
        //     {

        //     }
        //     if (ins.OpCode == OpCode.RET)
        //     {
        //         yield return 

        //     }

        //     stack.Peek().Step(ins.Size);

        // }

    }

    class ExecutionPath
    {
        ImmutableStack<int> path;

        public int Address => path.Peek();
        public IEnumerable<int> Path => path;

        public ExecutionPath(int start)
        {
            path = ImmutableStack.Create(start);
        }

        public void Step(Instruction ins)
        {
            path = path.Push(Address + ins.Size);
        }
    }
}
