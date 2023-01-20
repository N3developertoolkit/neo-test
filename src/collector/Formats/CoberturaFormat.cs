using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Neo.Collector.Models;

namespace Neo.Collector.Formats
{
    partial class CoberturaFormat : ICoverageFormat
    {
        public void WriteReport(IReadOnlyList<ContractCoverage> coverage, Action<string, Action<Stream>> writeAttachement)
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

        internal void WriteReport(XmlWriter writer, IReadOnlyList<ContractCoverage> coverage)
        {
            uint lineCount = 0;
            uint hitCount = 0;
            foreach (var contract in coverage)
            {
                var lines = contract.DebugInfo.Methods.SelectMany(m => m.SequencePoints);
                bool hitFunc(int address) => contract.HitMap.TryGetValue(address, out var count) && count > 0;
                var rate = Utility.GetLineRate(lines, hitFunc);
                lineCount += rate.lineCount;
                hitCount += rate.hitCount;
            }
            var lineRate = Utility.CalculateLineRate(lineCount, hitCount);

            using (var _ = writer.StartDocument())
            using (var __ = writer.StartElement("coverage"))
            {
                // TODO: branch-rate, branches-covered, branches-valid, complexity
                writer.WriteAttributeString("line-rate", $"{lineRate:N4}");
                writer.WriteAttributeString("lines-covered", $"{hitCount}");
                writer.WriteAttributeString("lines-valid", $"{lineCount}");
                writer.WriteAttributeString("version", ThisAssembly.AssemblyFileVersion);
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