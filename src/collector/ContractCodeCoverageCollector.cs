using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Neo.Collector.Models;

namespace Neo.Collector
{
    using Method = NeoDebugInfo.Method;

    class ContractCodeCoverageCollector
    {
        readonly string contractName;
        readonly NeoDebugInfo debugInfo;
        readonly Dictionary<int, uint> hitMap = new Dictionary<int, uint>();
        readonly Dictionary<int, (uint branchCount, uint continueCount)> branchMap = new Dictionary<int, (uint branchCount, uint continueCount)>();
        IReadOnlyList<(int address, Instruction instruction)> instructions = null;
        IReadOnlyDictionary<int, Instruction> instructionMap = null;

        public string ContractName => contractName;
        public Hash160 ScriptHash => debugInfo.Hash;
        public IReadOnlyDictionary<int, uint> HitMap => hitMap;
        public IReadOnlyDictionary<int, (uint branchCount, uint continueCount)> BranchMap => branchMap;

        public ContractCodeCoverageCollector(string contractName, NeoDebugInfo debugInfo)
        {
            if (string.IsNullOrEmpty(contractName)) throw new ArgumentException("Invalid contract name", nameof(contractName));
            if (debugInfo is null) throw new ArgumentNullException(nameof(debugInfo));

            this.contractName = contractName;
            this.debugInfo = debugInfo;
        }

        public static bool TryCreate(string contractName, string manifestPath, out ContractCodeCoverageCollector value)
        {
            var dirname = Path.GetDirectoryName(manifestPath);
            var basename = GetBaseName(manifestPath, ".manifest.json");
            var nefPath = Path.Combine(dirname, Path.ChangeExtension(basename, ".nef"));
            if (NeoDebugInfo.TryLoadContractDebugInfo(nefPath, out var debugInfo))
            {
                value = new ContractCodeCoverageCollector(contractName, debugInfo);
                return true;
            }

            value = null;
            return false;
        }

        public static string GetBaseName(string path, string extension = "")
        {
            var filename = Path.GetFileName(path);
            if (string.IsNullOrEmpty(extension)) return filename;
            return filename.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
                ? filename.Substring(0, filename.Length - extension.Length)
                : filename;
        }

        public void RecordScript(byte[] script)
        {
            if (!(instructionMap is null)) throw new InvalidOperationException();
            if (!(instructions is null)) throw new InvalidOperationException();
            instructions = script.EnumerateInstructions()
                .OrderBy(t => t.address)
                .ToArray();
            instructionMap = script.EnumerateInstructions().ToDictionary(t => t.address, t => t.instruction);

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

        public OldContractCoverage GetCoverage(int runCount)
        {
            var methods = new List<MethodCoverage>();
            foreach (var method in debugInfo.Methods)
            {
                if (method.SequencePoints.Count > 0)
                {
                    methods.Add(GetMethodCoverage(method));
                }
            }
            return new OldContractCoverage(runCount, contractName, debugInfo.Hash, methods);
        }

        MethodCoverage GetMethodCoverage(Method method)
        {
            if (method.SequencePoints.Count == 0) throw new ArgumentException("Invalid method with no sequence points", nameof(method));

            var doc = debugInfo.Documents[method.SequencePoints[0].Document];

            var lines = new List<LineCoverage>();
            for (var i = 0; i < method.SequencePoints.Count; i++)
            {
                var sp = method.SequencePoints[i];
                var hitCount = hitMap.TryGetValue(sp.Address, out var _hitCount) ? _hitCount : 0;

                var nextSPAddress = method.SequencePoints.GetNextOrDefault(i)?.Address ?? int.MaxValue;
                var branches = new List<BranchCoverage>();
                foreach (var (address, instruction) in instructions.Where(t => t.address >= sp.Address))
                {
                    if (address > method.Range.End) break;
                    if (address >= nextSPAddress) break;
                    if (instruction.IsBranchInstruction())
                    {
                        var counts = branchMap.TryGetValue(address, out var _counts) ? _counts : (0,0);
                        branches.Add(new BranchCoverage(address, counts));
                    }
                }
                lines.Add(new LineCoverage(sp, hitCount, branches));
            }

            return new MethodCoverage(method, doc, lines);
        }

        void ComputePaths(Method method)
        {
            var branchPoints = new List<int>();

            // instructions
            //     .SkipWhile(t => t.address < method.Range.Start)
            //     .TakeWhile(t => t.address <= method.Range.End)
            //     .Where(t => t.instruction.IsBranchInstruction())


            var current = method.Range.Start;
            while (current <= method.Range.End)
            {
                var ins = instructionMap[current];
                // if (ins.IsBranchInstruction()) branchPoints.Add(current)
                var offset = ins.GetBranchOffset();
                if (offset == 0)
                {
                    current += ins.Size;
                }


                

            }

        }
    }
}