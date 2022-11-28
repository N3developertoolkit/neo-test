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

    class MethodCoverage
    {
        readonly Method method;
        public readonly IReadOnlyList<LineCoverage> Lines;

        public string Namespace => method.Namespace;
        public string Name => method.Name;

        public MethodCoverage(Method method, IEnumerable<LineCoverage> lines)
        {
            this.method = method;
            Lines = lines.ToArray();
        }
    }

    class LineCoverage
    {
        public readonly SequencePoint SequencePoint;
        public readonly IReadOnlyList<(int address, Instruction instruction)> Instructions;

        public LineCoverage(SequencePoint sp, IEnumerable<(int address, Instruction instruction)> instructions)
        {
            SequencePoint = sp;
            Instructions = instructions.ToArray();
        }
    }

    class ContractCoverage
    {
        readonly string contractName;
        readonly NeoDebugInfo debugInfo;
        readonly Dictionary<int, uint> hitMap = new Dictionary<int, uint>();
        readonly Dictionary<int, (uint branchCount, uint continueCount)> branchMap = new Dictionary<int, (uint branchCount, uint continueCount)>();
        IReadOnlyList<(int address, Instruction instruction)> instructions = null;

        public string ContractName => contractName;
        public Hash160 ScriptHash => debugInfo.Hash;
        public IReadOnlyDictionary<int, uint> HitMap => hitMap;
        public IReadOnlyDictionary<int, (uint branchCount, uint continueCount)> BranchMap => branchMap;

        public ContractCoverage(string contractName, NeoDebugInfo debugInfo)
        {
            if (string.IsNullOrEmpty(contractName)) throw new ArgumentException("Invalid contract name", nameof(contractName));
            if (debugInfo is null) throw new ArgumentNullException(nameof(debugInfo));

            this.contractName = contractName;
            this.debugInfo = debugInfo;
        }

        public static bool TryCreate(string contractName, string manifestPath, out ContractCoverage value)
        {
            var dirname = Path.GetDirectoryName(manifestPath);
            var basename = GetBaseName(manifestPath, ".manifest.json");
            var nefPath = Path.Combine(dirname, Path.ChangeExtension(basename, ".nef"));
            if (NeoDebugInfo.TryLoadContractDebugInfo(nefPath, out var debugInfo))
            {
                value = new ContractCoverage(contractName, debugInfo);
                return true;
            }

            value = null;
            return false;
        }

        static string GetBaseName(string path, string extension = "")
        {
            var filename = Path.GetFileName(path);
            if (string.IsNullOrEmpty(extension)) return filename;
            return filename.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
                ? filename.Substring(0, filename.Length - extension.Length)
                : filename;
        }

        public void RecordScript(byte[] script)
        {
            if (!(instructions is null)) throw new InvalidOperationException();
            instructions = script.EnumerateInstructions()
                .OrderBy(t => t.address)
                .ToArray();
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

        public IEnumerable<MethodCoverage> GetMethodCoverages()
        {
            foreach (var method in debugInfo.Methods)
            {
                var lines = GetLineCoverages(method);
                yield return new MethodCoverage(method, lines);
            }
        }

        IEnumerable<LineCoverage> GetLineCoverages(Method method)
        {
            for (var i = 0; i < method.SequencePoints.Count; i++)
            {
                var sp = method.SequencePoints[i];
                var nextSPAddress = method.SequencePoints.GetNextOrDefault(i)?.Address ?? int.MaxValue;

                var ins = instructions
                    .Where(t => t.address >= sp.Address
                        && t.address <= method.Range.End 
                        && t.address < nextSPAddress);
                yield return new LineCoverage(sp, ins);
            }
        }

        // IEnumerable<(SequencePoint, IEnumerable<(int, Instruction)>)> FooMethod(Method method)
        // {
        // }

        // IEnumerable<(int address, Instruction instruction)> GetMethod(Method method, int index)
        // {
        //     var sp = method.SequencePoints[index];
        //     var nextSP = method.SequencePoints.GetNextOrDefault(index)?.Address ?? int.MaxValue;

        //     foreach (var ins in instructions.SkipWhile(t => t.address < sp.Address))
        //     {
        //         if (ins.address > method.Range.End) yield break;
        //         if (ins.address >= nextSP) yield break;
        //         yield return ins;
        //     }
        // }

        // public void WriteCoberturaPackage(XmlWriter writer)
        // {
        //     using (var _p = writer.StartElement("package"))
        //     {
        //         writer.WriteAttributeString("name", contractName);
        //         using (var _c = writer.StartElement("classes"))
        //         {
        //             foreach (var group in debugInfo.Methods.GroupBy(m => m.Namespace).OrderBy(kvp => kvp.Key))
        //             {
        //                 WriteCoberturaClass(writer, group.Key, group);
        //             }
        //         }
        //     }
        // }

        // void WriteCoberturaClass(XmlWriter writer, string name, IEnumerable<Method> methods)
        // {
        //     var doc = methods
        //         .SelectMany(m => m.SequencePoints)
        //         .Select(p => debugInfo.Documents[p.Document])
        //         .FirstOrDefault();
        //     doc = string.IsNullOrEmpty(doc) ? "<unknown>" : doc;

        //     using (var _c = writer.StartElement("class"))
        //     {
        //         writer.WriteAttributeString("name", name);
        //         writer.WriteAttributeString("filename", doc);
        //         using (var _m = writer.StartElement("methods"))
        //         {
        //             foreach (var method in methods)
        //             {
        //                 WriteCoberturaMethod(writer, method);
        //             }
        //         }
        //     }
        // }

        // private void WriteCoberturaMethod(XmlWriter writer, Method method)
        // {
        //     using (var _ = writer.StartElement("method"))
        //     {
        //         var @params = method.Parameters
        //             .Where(p => !(p.Name == "this" && p.Type == "Any" && p.Index == 0))
        //             .Select(p => p.Type);
        //         writer.WriteAttributeString("name", method.Name);
        //         writer.WriteAttributeString("signature", $"({string.Join(",", @params)})");
        //         // writer.WriteAttributeString("range", $"{method.Range.Start}-{method.Range.End}");
        //         using (var _2 = writer.StartElement("lines"))
        //         {
        //             for (int i = 0; i < method.SequencePoints.Count; i++)
        //             {
        //                 var sp = method.SequencePoints[i];

        //                 using (var _3 = writer.StartElement("line"))
        //                 {
        //                     writer.WriteAttributeString("number", $"{sp.Start.Line}");

        //                     // if (TryGetBranchAddress(sp, method.SequencePoints.GetNextOrDefault(i), method.Range, out var branchAddress))
        //                     // {
        //                     //     writer.WriteAttributeString("branch", "True");
        //                     // }
        //                     // else
        //                     // {
        //                     //     var hits = hitMap.TryGetValue(sp.Address, out var value) ? value : 0;
        //                     //     writer.WriteAttributeString("hits", $"{hits}");
        //                     //     writer.WriteAttributeString("branch", "False");
        //                     // }
        //                 }
        //             }
        //         }
        //     }
        // }

        // bool TryGetBranchAddress(SequencePoint sequencePoint, SequencePoint nextSequencePoint, (int Start, int End) methodRange, out int branchAddress)
        // {
        //     // if (instructions is null) { throw new NotImplementedException(); }

        //     var nextAddress = nextSequencePoint is null ? int.MaxValue : nextSequencePoint.Address;

        //     // var pointInstructions = instructions.Where(t => t.address >= sequencePoint.Address);
        //     // foreach (var (address, instruction) in pointInstructions)
        //     // {
        //     //     if (address > methodRange.End) break;
        //     //     if (address >= nextAddress) break;
        //     //     if (instruction.IsBranchInstruction())
        //     //     {
        //     //         branchAddress = address;
        //     //         return true;
        //     //     }
        //     // }

        //     branchAddress = default;
        //     return false;
        // }
    }
}