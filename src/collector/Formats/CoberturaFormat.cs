using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Neo.Collector.Models;

namespace Neo.Collector.Formats
{
    partial class CoberturaFormat : ICoverageFormat
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

                using (var ___ = writer.StartElement("sources"))
                {
                    foreach (var contract in coverage)
                    {
                        writer.WriteElementString("source", contract.DebugInfo.DocumentRoot);
                    }
                }

                using (var ___ = writer.StartElement("packages"))
                {
                    foreach (var contract in coverage)
                    {
                        var ccWriter = new ContractCoverageWriter(contract);
                        ccWriter.WritePackage(writer);
                    }
                }
            }
        }
    }
}