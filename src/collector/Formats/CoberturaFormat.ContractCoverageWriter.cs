using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Neo.Collector.Models;

namespace Neo.Collector.Formats
{
    partial class CoberturaFormat
    {
        internal class ContractCoverageWriter
        {
            readonly ContractCoverage contract;

            NeoDebugInfo DebugInfo => contract.DebugInfo;

            public ContractCoverageWriter(ContractCoverage contract)
            {
                this.contract = contract;
            }

            bool GetAddressHit(int address) => contract.HitMap.TryGetValue(address, out var count) && count > 0;
            (uint, uint) GetBranchHit(int address) => contract.BranchHitMap.TryGetValue(address, out var value) ? value : (0, 0);

            public void WritePackage(XmlWriter writer)
            {
                var lineRate = DebugInfo.Methods.SelectMany(m => m.SequencePoints).CalculateLineRate(GetAddressHit);
                var branchRate = contract.InstructionMap.CalculateBranchRate(DebugInfo.Methods, GetBranchHit);

                using (var _ = writer.StartElement("package"))
                {
                    // TODO: complexity
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

            internal void WriteClass(XmlWriter writer, string name, IEnumerable<NeoDebugInfo.Method> methods)
            {
                var docIndex = methods.SelectMany(m => m.SequencePoints).Select(sp => sp.Document).FirstOrDefault();
                var filename = docIndex < contract.DebugInfo.Documents.Count
                    ? contract.DebugInfo.Documents[docIndex] : string.Empty;
                var lineRate = methods.SelectMany(m => m.SequencePoints).CalculateLineRate(GetAddressHit);
                var branchRate = contract.InstructionMap.CalculateBranchRate(methods, GetBranchHit);

                using (var _ = writer.StartElement("class"))
                { 
                    // TODO: complexity
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

                    using (var __ = writer.StartElement("lines"))
                    {
                        foreach (var method in methods)
                        {
                            for (int i = 0; i < method.SequencePoints.Count; i++)
                            {
                                WriteLine(writer, method, i);
                            }
                        }
                    }
                }
            }

            internal void WriteMethod(XmlWriter writer, NeoDebugInfo.Method method)
            {
                var signature = string.Join(", ", method.Parameters.Select(p => p.Type));
                var lineRate = method.SequencePoints.CalculateLineRate(GetAddressHit);
                var branchRate = contract.InstructionMap.CalculateBranchRate(method, GetBranchHit);

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

            internal void WriteLine(XmlWriter writer, NeoDebugInfo.Method method, int index)
            {
                var sp = method.SequencePoints[index];
                var hits = contract.HitMap.TryGetValue(sp.Address, out var value) ? value : 0;
                var (branchCount, branchHit) = contract.InstructionMap.GetBranchRate(method, index, GetBranchHit);

                using (var _ = writer.StartElement("line"))
                {
                    writer.WriteAttributeString("number", $"{sp.Start.Line}");
                    writer.WriteAttributeString("address", $"{sp.Address}");
                    writer.WriteAttributeString("hits", $"{hits}");

                    if (branchCount == 0)
                    {
                        writer.WriteAttributeString("branch", $"{false}");
                    }
                    else
                    {
                        var branchRate = Utility.CalculateHitRate(branchCount, branchHit);

                        writer.WriteAttributeString("branch", $"{true}");
                        writer.WriteAttributeString("condition-coverage", $"{branchRate * 100:N}% ({branchHit}/{branchCount})");
                        using (var __ = writer.StartElement("conditions"))
                        {
                            foreach (var (address, opCode) in contract.InstructionMap.GetBranchInstructions(method, index))
                            {
                                var (condBranchCount, condContinueCount) = GetBranchHit(address);
                                var coverage = condBranchCount == 0 ? 0m : 1m;
                                coverage += condContinueCount == 0 ? 0m : 1m;

                                using (var _3 = writer.StartElement("condition"))
                                {
                                    writer.WriteAttributeString("number", $"{address}");
                                    writer.WriteAttributeString("type", $"{opCode}");
                                    writer.WriteAttributeString("coverage", $"{coverage / 2m * 100m}%");
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
