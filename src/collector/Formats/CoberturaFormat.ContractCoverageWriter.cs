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
                decimal lineCount = 0;
                decimal lineHitCount = 0;
                foreach (var line in lines)
                {
                    lineCount++;
                    if (contract.HitMap.TryGetValue(line.Address, out var hitCount)
                        && hitCount > 0)
                    {
                        lineHitCount++;
                    }
                }

                return lineHitCount / lineCount;
            }

            public void WritePackage(XmlWriter writer)
            {
                var lineRate = CalculateLineRate(DebugInfo.Methods.SelectMany(m => m.SequencePoints));

                using (var _ = writer.StartElement("package"))
                { 
                    // TODO: branch-rate, complexity
                    writer.WriteAttributeString("name", contract.Name);
                    writer.WriteAttributeString("scripthash", $"{contract.DebugInfo.Hash}");
                    writer.WriteAttributeString("line-rate", $"{lineRate}");
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
                using (var _ = writer.StartElement("class"))
                {
                    // TODO: branch-rate, complexity
                    writer.WriteAttributeString("name", name);
                    if (filename.Length > 0) { writer.WriteAttributeString("filename", filename); }
                    writer.WriteAttributeString("line-rate", $"{lineRate}");
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
                using (var _ = writer.StartElement("method"))
                {
                    // TODO: branch-rate, complexity
                    writer.WriteAttributeString("name", method.Name);
                    writer.WriteAttributeString("signature", $"({signature})");
                    writer.WriteAttributeString("line-rate", $"{lineRate}");
                    using (var __ = writer.StartElement("lines"))
                    {
                        foreach (var sp in method.SequencePoints)
                        {
                            WriteLine(writer, sp);
                        }
                    }
                }
            }

            void WriteLine(XmlWriter writer, NeoDebugInfo.SequencePoint sp)
            {
                var hits = contract.HitMap.TryGetValue(sp.Address, out var value) ? value : 0;
                using (var _ = writer.StartElement("line"))
                {
                    // TODO: branch (t/f), condition-coverage
                    writer.WriteAttributeString("number", $"{sp.Start.Line}");
                    writer.WriteAttributeString("hits", $"{hits}");
                    // if (line.Branches.Count > 0)
                    // {
                    //     writer.WriteAttributeString("branch", $"{true}");
                    //     using (var __ = writer.StartElement("conditions"))
                    //     {
                    //         foreach (var branch in line.Branches)
                    //         {
                    //             WriteCondition(writer, branch);
                    //         }
                    //     }
                    // }
                    // else 
                    // {
                    //     writer.WriteAttributeString("branch", $"{false}");
                    // }
                }
            }

            void WriteCondition(XmlWriter writer, BranchCoverage branch)
            {
                using (var _ = writer.StartElement("condition"))
                {
                    // TODO: coverage
                    writer.WriteAttributeString("number", $"{branch.Address}");
                    writer.WriteAttributeString("type", "jump");
                }
            }
        }
    }
}