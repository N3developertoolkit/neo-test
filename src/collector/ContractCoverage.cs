using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Neo.Collector.Models;

namespace Neo.Collector
{
    class ContractCoverage
    {
        readonly string contractName;
        readonly NeoDebugInfo debugInfo;
        readonly IReadOnlyList<(int address, Instruction instruction)> instructions;
        readonly IDictionary<uint, uint> hitMap = new Dictionary<uint, uint>();
        readonly IDictionary<uint, (uint branchCount, uint continueCount)> branchMap = new Dictionary<uint, (uint branchCount, uint continueCount)>();

        public Hash160 ScriptHash { get; }
        public IReadOnlyDictionary<uint, uint> HitMap => (IReadOnlyDictionary<uint, uint>)hitMap;
        public IReadOnlyDictionary<uint, (uint branchCount, uint continueCount)> BranchMap => 
            (IReadOnlyDictionary<uint, (uint branchCount, uint continueCount)>)branchMap;

        public ContractCoverage(string contractName, NeoDebugInfo debugInfo, NefFile nefFile)
        {
            if (string.IsNullOrEmpty(contractName)) throw new ArgumentException("Invalid contract name", nameof(contractName));
            if (debugInfo is null) throw new ArgumentNullException(nameof(debugInfo));

            this.contractName = contractName;
            this.debugInfo = debugInfo;
            ScriptHash = debugInfo.Hash;

            if (!(nefFile is null))
            {
                var hash = nefFile.CalculateScriptHash();
                if (!hash.Equals(debugInfo.Hash))
                {
                    throw new ArgumentException("DebugInfo script hash doesn't match NefFile script hash");
                }

                instructions = nefFile.EnumerateInstructions().ToArray();
            }
        }

        public void RecordHit(uint address)
        {
            var hitCount = hitMap.TryGetValue(address, out var value) ? value : 0;
            hitMap[address] = hitCount + 1;
        }

        public void RecordBranch(uint address, uint offsetAddress, uint branchResult)
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
                var nefFile = NefFile.TryLoad(nefPath, out var _nefFile) ? _nefFile : null;
                value = new ContractCoverage(contractName, debugInfo, nefFile);
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


        void WriteCoberturaClass(XmlWriter writer, string name, IEnumerable<NeoDebugInfo.Method> methods)
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

        private void WriteCoberturaMethod(XmlWriter writer, NeoDebugInfo.Method method)
        {
            using (var _ = writer.StartElement("method"))
            {
                var @params = method.Parameters
                    .Where(p => !(p.Name == "this" && p.Type == "Any" && p.Index == 0))
                    .Select(p => p.Type);
                writer.WriteAttributeString("name", method.Name);
                writer.WriteAttributeString("signature", $"({string.Join(",", @params)})");
                writer.WriteAttributeString("range", $"{method.Range.Start}-{method.Range.End}");
                using (var _2 = writer.StartElement("lines"))
                {
                    for (int i = 0; i < method.SequencePoints.Count; i++)
                    {
                        NeoDebugInfo.SequencePoint sp = method.SequencePoints[i];
                        // Note, end may not be a valid 
                        // var end = i < method.SequencePoints.Count
                        //     ? method.SequencePoints[i + 1]
                        //     : null;
                        var isBranch = IsBranchInstruction(method, i);
                        using (var _3 = writer.StartElement("line"))
                        {
                            writer.WriteAttributeString("name", $"{sp.Start.Line}");
                            writer.WriteAttributeString("branch", $"{isBranch.HasValue}");
                            writer.WriteAttributeString("address", $"{sp.Address}");
                        }
                    }
                }
            }
        }

        int? IsBranchInstruction(NeoDebugInfo.Method method, int index)
        {
            if (instructions is null) { throw new NotImplementedException(); }

            var sequencePoint = method.SequencePoints[index];

            // var sp = method.SequencePoints[sequencePointIndex];
            // var nextSeqPointAddress = sequencePointIndex < method.SequencePoints.Count
            //     ? method.SequencePoints[sequencePointIndex + 1].Address
            //     : -1;
            var pointInstructions = instructions.Where(t => t.address >= sequencePoint.Address);
            foreach (var (address, instruction) in pointInstructions)
            { 
                if (address > method.Range.End) break;
                // if (address >= nextSeqPointAddress) break;
                if (instruction.IsBranchInstruction()) return address;
            }
            return null;
        }
    }
}