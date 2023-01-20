using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Neo.Collector.Models;
using static Neo.Collector.Models.ContractCoverage;

namespace Neo.Collector.Formats
{
    class CoberturaFormat : ICoverageFormat
    {
        public void WriteReport(IEnumerable<ContractCoverage> coverage, Action<string, Action<Stream>> writeAttachement)
        {
            writeAttachement("neo.cobertura.xml", stream => 
            {
                var textWriter = new StreamWriter(stream);
                var xmlWriter = new XmlTextWriter(textWriter) { Formatting = Formatting.Indented };
                WriteReport(xmlWriter, coverage);
                xmlWriter.Flush();
                textWriter.Flush();
            });
        }

        internal void WriteReport(XmlWriter writer, IEnumerable<ContractCoverage> coverage)
        {
            using (var _ = writer.StartDocument())
            using (var __ = writer.StartElement("coverage"))
            {
                writer.WriteAttributeString("version", ThisAssembly.AssemblyInformationalVersion);
                writer.WriteAttributeString("timestamp", $"{DateTimeOffset.Now.ToUnixTimeSeconds()}");

                using (var ___ = writer.StartElement("packages"))
                {
                    foreach (var contract in coverage)
                    {
                        WritePackage(writer, contract);
                    }
                }
            }
        }

        void WritePackage(XmlWriter writer, ContractCoverage contract)
        {
            using (var _ = writer.StartElement("package"))
            {
                writer.WriteAttributeString("name", contract.Name);
                writer.WriteAttributeString("scripthash", $"{contract.DebugInfo.Hash}");
                // writer.WriteAttributeString("line-rate", $"{contract.CalcLineCoverage().AsPercentage() / 100}");
                using (var __ = writer.StartElement("classes"))
                {
                    // foreach (var group in contract.Methods.GroupBy(m => m.Namespace))
                    // {
                    //     WriteClass(writer, group.Key, group);
                    // }
                }
            }
        }

        void WriteClass(XmlWriter writer, string name, IEnumerable<MethodCoverage> methods)
        {
            using (var _ = writer.StartElement("class"))
            {
                writer.WriteAttributeString("name", name);
                writer.WriteAttributeString("line-rate", $"{methods.CalcLineCoverage().AsPercentage() / 100}");
                using (var __ = writer.StartElement("methods"))
                {
                    foreach (var method in methods)
                    {
                        WriteMethod(writer, method);
                    }
                }
            }
        }

        void WriteMethod(XmlWriter writer, MethodCoverage method)
        {
            using (var _ = writer.StartElement("method"))
            {
               
                var signature = string.Join(", ", method.Parameters.Select(p => p.Type));
                writer.WriteAttributeString("name", method.Name);
                writer.WriteAttributeString("signature", $"({signature})");
                writer.WriteAttributeString("line-rate", $"{method.CalcLineCoverage().AsPercentage() / 100}");
                using (var __ = writer.StartElement("lines"))
                {
                    foreach (var line in method.Lines)
                    {
                        WriteLine(writer, line);
                    }
                }
            }
        }

        void WriteLine(XmlWriter writer, LineCoverage line)
        {
            using (var _ = writer.StartElement("line"))
            {
                writer.WriteAttributeString("number", $"{line.Start.Line}");
                writer.WriteAttributeString("hits", $"{line.HitCount}");
                if (line.Branches.Count > 0)
                {
                    writer.WriteAttributeString("branch", $"{true}");
                    using (var __ = writer.StartElement("conditions"))
                    {
                        foreach (var branch in line.Branches)
                        {
                            WriteCondition(writer, branch);
                        }
                    }
                }
                else 
                {
                    writer.WriteAttributeString("branch", $"{false}");
                }
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