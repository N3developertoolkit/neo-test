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

            public ContractCoverageWriter(ContractCoverage contract)
            {
                this.contract = contract;
            }

            public void WritePackage(XmlWriter writer)
            {
                using (var _ = writer.StartElement("package"))
                {
                    writer.WriteAttributeString("name", contract.Name);
                    writer.WriteAttributeString("scripthash", $"{contract.DebugInfo.Hash}");
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
                using (var _ = writer.StartElement("class"))
                {
                    writer.WriteAttributeString("name", name);
                    if (docIndex < contract.DebugInfo.Documents.Count)
                    {
                        writer.WriteAttributeString("filename", contract.DebugInfo.Documents[docIndex]);
                    }
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
                using (var _ = writer.StartElement("method"))
                {
                    var signature = string.Join(", ", method.Parameters.Select(p => p.Type));
                    writer.WriteAttributeString("name", method.Name);
                    writer.WriteAttributeString("signature", $"({signature})");
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
                using (var _ = writer.StartElement("line"))
                {
                    writer.WriteAttributeString("number", $"{sp.Start.Line}");
                    // writer.WriteAttributeString("hits", $"{line.HitCount}");
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
                    writer.WriteAttributeString("number", $"{branch.Address}");
                    writer.WriteAttributeString("type", "jump");
                }
            }
        }
    }
}