using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Neo.Collector.Models;

namespace Neo.Collector
{
    using Method = NeoDebugInfo.Method;
    using SequencePoint = NeoDebugInfo.SequencePoint;

    class ContractCoverage
    {
        readonly string contractName;
        readonly NeoDebugInfo debugInfo;
        readonly IReadOnlyList<(int address, Instruction instruction)> instructions;
        readonly Dictionary<int, uint> hitMap = new Dictionary<int, uint>();
        readonly Dictionary<int, (uint branchCount, uint continueCount)> branchMap = new Dictionary<int, (uint branchCount, uint continueCount)>();

        public Hash160 ScriptHash { get; }
        public IReadOnlyDictionary<int, uint> HitMap => hitMap;
        public IReadOnlyDictionary<int, (uint branchCount, uint continueCount)> BranchMap => branchMap;

        public ContractCoverage(string contractName, NeoDebugInfo debugInfo)
        {
            if (string.IsNullOrEmpty(contractName)) throw new ArgumentException("Invalid contract name", nameof(contractName));
            if (debugInfo is null) throw new ArgumentNullException(nameof(debugInfo));

            this.contractName = contractName;
            this.debugInfo = debugInfo;
            ScriptHash = debugInfo.Hash;
        }

        public void RecordHit(int address)
        {
            var hitCount = hitMap.TryGetValue(address, out var value) ? value : 0;
            hitMap[address] = hitCount + 1;
        }

        public void RecordBranch(int address, int offsetAddress, int branchResult)
        {
            RecordHit(address);
            var (branchCount, continueCount) = branchMap.TryGetValue(address, out var value)
                ? value : (0, 0);
            branchMap[address] = branchResult == address
                ? (branchCount, continueCount + 1)
                : branchResult == offsetAddress
                    ? (branchCount + 1, continueCount)
                    : throw new FormatException($"Branch result {branchResult} did not equal {address} or {offsetAddress}");
        }

        public static bool TryCreate(string contractName, string manifestPath, out ContractCoverage value)
        {
            var dirname = Path.GetDirectoryName(manifestPath);
            var basename = Path.GetFileNameWithoutExtension(manifestPath);

            if (manifestPath.EndsWith(".manifest.json"))
            {
                basename = Path.GetFileNameWithoutExtension(basename);
            }

            var nefPath = Path.Combine(dirname, $"{basename}.nef");
            if (NeoDebugInfo.TryLoadContractDebugInfo(nefPath, out var debugInfo))
            {
                value = new ContractCoverage(contractName, debugInfo);
                return true;
            }

            value = null;
            return false;
        }

        public void WriteCoberturaPackage(XmlWriter writer)
        {
            using (var _ = writer.StartElement("package"))
            {
                writer.WriteAttributeString("name", contractName);
                using (var __ = writer.StartElement("classes"))
                {
                    foreach (var group in debugInfo.Methods.GroupBy(m => m.Namespace).OrderBy(kvp => kvp.Key))
                    {
                        WriteCoberturaClass(writer, group.Key, group);
                    }
                }
            }
        }

        void WriteCoberturaClass(XmlWriter writer, string name, IEnumerable<Method> methods)
        {
            var doc = methods
                .SelectMany(m => m.SequencePoints)
                .Select(p => debugInfo.Documents[p.Document])
                .FirstOrDefault();
            if (string.IsNullOrEmpty(doc)) doc = "<unknown>";
            using (var _ = writer.StartElement("class"))
            {
                writer.WriteAttributeString("name", name);
                writer.WriteAttributeString("filename", doc);
                using (var __ = writer.StartElement("methods"))
                {
                    foreach (var method in methods)
                    {
                        WriteCoberturaMethod(writer, method);
                    }
                }
            }
        }

        private void WriteCoberturaMethod(XmlWriter writer, Method method)
        {
            using (var _ = writer.StartElement("method"))
            {
                var @params = method.Parameters
                    .Where(p => !(p.Name == "this" && p.Type == "Any" && p.Index == 0))
                    .Select(p => p.Type);
                writer.WriteAttributeString("name", method.Name);
                writer.WriteAttributeString("signature", $"({string.Join(",", @params)})");
                // writer.WriteAttributeString("range", $"{method.Range.Start}-{method.Range.End}");
                using (var _2 = writer.StartElement("lines"))
                {
                    for (int i = 0; i < method.SequencePoints.Count; i++)
                    {
                        var sp = method.SequencePoints[i];

                        using (var _3 = writer.StartElement("line"))
                        {
                            writer.WriteAttributeString("number", $"{sp.Start.Line}");

                            if (TryGetBranchAddress(sp, method.SequencePoints.GetNextOrDefault(i), method.Range, out var branchAddress))
                            {
                                writer.WriteAttributeString("branch", "True");
                            }
                            else
                            {
                                var hits = hitMap.TryGetValue(sp.Address, out var value) ? value : 0;
                                writer.WriteAttributeString("hits", $"{hits}");
                                writer.WriteAttributeString("branch", "False");
                            }
                        }
                    }
                }
            }
        }

        bool TryGetBranchAddress(SequencePoint sequencePoint, SequencePoint nextSequencePoint, (int Start, int End) methodRange, out int branchAddress)
        {
            // if (instructions is null) { throw new NotImplementedException(); }

            var nextAddress = nextSequencePoint is null ? int.MaxValue : nextSequencePoint.Address;

            var pointInstructions = instructions.Where(t => t.address >= sequencePoint.Address);
            foreach (var (address, instruction) in pointInstructions)
            {
                if (address > methodRange.End) break;
                if (address >= nextAddress) break;
                if (instruction.IsBranchInstruction())
                {
                    branchAddress = address;
                    return true;
                }
            }

            branchAddress = default;
            return false;
        }
    }
}