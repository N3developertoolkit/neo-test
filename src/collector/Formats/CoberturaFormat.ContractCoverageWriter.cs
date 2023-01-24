using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Neo.Collector.Models;
using static Neo.Collector.Models.ContractCoverage;

namespace Neo.Collector.Formats
{
    partial class CoberturaFormat
    {
        class ContractCoverageWriter
        {
            readonly ContractCoverage contract;

            NeoDebugInfo DebugInfo => contract.DebugInfo;

            public ContractCoverageWriter(ContractCoverage contract)
            {
                this.contract = contract;
            }

            decimal CalculateLineRate(IEnumerable<NeoDebugInfo.SequencePoint> lines)
            {
                bool hitFunc(int address) => contract.HitMap.TryGetValue(address, out var count) && count > 0;
                return Utility.CalculateLineRate(lines, hitFunc);
            }

            (uint branchCount, uint branchHit) CalculateBranchRate(IEnumerable<NeoDebugInfo.Method> methods)
            {
                var branchCount = 0u;
                var branchHit = 0u;
                foreach (var method in methods)
                {
                    var rate = CalculateBranchRate(method);
                    branchCount += rate.branchCount;
                    branchHit += rate.branchHit;
                }
                return (branchCount, branchHit);
            }

            (uint branchCount, uint branchHit) CalculateBranchRate(NeoDebugInfo.Method method)
            {
                var branchCount = 0u;
                var branchHit = 0u;
                for (int i = 0; i < method.SequencePoints.Count; i++)
                {
                    var rate = CalculateBranchRate(method, i);
                    branchCount += rate.branchCount;
                    branchHit += rate.branchHit;
                }
                return (branchCount, branchHit);
            }

            (uint branchCount, uint branchHit) CalculateBranchRate(NeoDebugInfo.Method method, int index)
            {
                var branchCount = 0u;
                var branchHit = 0u;
                foreach (var (address, _) in GetBranchInstructions(method, index))
                {
                    var (branchHitCount, continueHitCount) = contract.BranchHitMap.TryGetValue(address, out var value) ? value : (0, 0);
                    branchCount += 2;
                    branchHit += branchHitCount == 0 ? 0u : 1u;
                    branchHit += continueHitCount == 0 ? 0u : 1u;

                }
                return (branchCount, branchHit);
            }

            IEnumerable<(int address, OpCode opCode)> GetBranchInstructions(NeoDebugInfo.Method method, int index)
            {
                var instructionMap = contract.InstructionMap;
                var address = method.SequencePoints[index].Address;
                var last = method.GetLineLastAddress(index, instructionMap);

                while (address < last)
                {
                    var ins = instructionMap[address];

                    if (ins.IsBranchInstruction())
                    {
                        yield return (address, ins.OpCode);
                    }
                    address += ins.Size;
                }
            }

            public void WritePackage(XmlWriter writer)
            {
                var lineRate = CalculateLineRate(DebugInfo.Methods.SelectMany(m => m.SequencePoints));
                var (branchCount, branchHit) = CalculateBranchRate(DebugInfo.Methods);
                var branchRate = Utility.CalculateHitRate(branchCount, branchHit);

                using (var _ = writer.StartElement("package"))
                {
                    writer.WriteAttributeString("name", contract.Name);
                    writer.WriteAttributeString("scripthash", $"{contract.DebugInfo.Hash}");
                    writer.WriteAttributeString("line-rate", $"{lineRate:N4}");
                    writer.WriteAttributeString("branch-rate", $"{branchRate:N4}");
                    using (var __ = writer.StartElement("classes"))
                    {
                        foreach (var group in contract.DebugInfo.Methods.GroupBy(m => m.Namespace))
                        {
                            WriteClass(writer, group.Key, group);
                        }
                    }
                }
            }

            void WriteClass(XmlWriter writer, string name, IEnumerable<NeoDebugInfo.Method> methods)
            {
                var docIndex = methods.SelectMany(m => m.SequencePoints).Select(sp => sp.Document).FirstOrDefault();
                var filename = docIndex < contract.DebugInfo.Documents.Count
                    ? contract.DebugInfo.Documents[docIndex] : string.Empty;
                var lineRate = CalculateLineRate(methods.SelectMany(m => m.SequencePoints));
                var (branchCount, branchHit) = CalculateBranchRate(methods);
                var branchRate = Utility.CalculateHitRate(branchCount, branchHit);
                using (var _ = writer.StartElement("class"))
                {
                    writer.WriteAttributeString("name", name);
                    if (filename.Length > 0) { writer.WriteAttributeString("filename", filename); }
                    writer.WriteAttributeString("line-rate", $"{lineRate:N4}");
                    writer.WriteAttributeString("branch-rate", $"{branchRate:N4}");
                    using (var __ = writer.StartElement("methods"))
                    {
                        foreach (var method in methods)
                        {
                            WriteMethod(writer, method);
                        }
                    }
                }
            }

            void WriteMethod(XmlWriter writer, NeoDebugInfo.Method method)
            {
                var signature = string.Join(", ", method.Parameters.Select(p => p.Type));
                var lineRate = CalculateLineRate(method.SequencePoints);
                var (branchCount, branchHit) = CalculateBranchRate(method);
                var branchRate = Utility.CalculateHitRate(branchCount, branchHit);
                using (var _ = writer.StartElement("method"))
                {
                    writer.WriteAttributeString("name", method.Name);
                    writer.WriteAttributeString("signature", $"({signature})");
                    writer.WriteAttributeString("line-rate", $"{lineRate:N4}");
                    writer.WriteAttributeString("branch-rate", $"{branchRate:N4}");
                    using (var __ = writer.StartElement("lines"))
                    {
                        for (int i = 0; i < method.SequencePoints.Count; i++)
                        {
                            WriteLine(writer, method, i);
                        }
                    }
                }
            }

            void WriteLine(XmlWriter writer, NeoDebugInfo.Method method, int index)
            {
                var sp = method.SequencePoints[index];
                var hits = contract.HitMap.TryGetValue(sp.Address, out var value) ? value : 0;
                var (branchCount, branchHit) = CalculateBranchRate(method, index);

                using (var _ = writer.StartElement("line"))
                {
                    writer.WriteAttributeString("number", $"{sp.Start.Line}");
                    writer.WriteAttributeString("hits", $"{hits}");

                    if (branchCount == 0)
                    {
                        writer.WriteAttributeString("branch", $"{false}");
                    }
                    else
                    {
                        var branchRate = Utility.CalculateHitRate(branchCount, branchHit);
                        writer.WriteAttributeString("branch", $"{true}");
                        writer.WriteAttributeString("condition-coverage", $"{branchRate * 100:N4}% ({branchHit}/{branchCount}");
                        using (var __ = writer.StartElement("conditions"))
                        {
                            foreach (var (address, opCode) in GetBranchInstructions(method, index))
                            {
                                var (condBranchCount, condContinueCount) = contract.BranchHitMap[address];
                                var coverage = condBranchCount == 0 ? 0m : 1m
                                    + condContinueCount == 0 ? 0m : 1m;
                                using (var _3 = writer.StartElement("condition"))
                                {
                                    writer.WriteAttributeString("number", $"{address}");
                                    writer.WriteAttributeString("type", $"{opCode}");
                                    writer.WriteAttributeString("coverage", $"{coverage * 100}%");
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
