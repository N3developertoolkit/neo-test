using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Neo.Collector.Models;

namespace Neo.Collector
{
    using ContractMap = Dictionary<(string name, Hash160 hash), IEnumerable<SequencePoint>>;
    using HitMaps = Dictionary<Hash160, Dictionary<uint, uint>>;
    using BranchMaps = Dictionary<Hash160, Dictionary<uint, (uint branchCount, uint continueCount)>>;

    public class ContractCoverageWriter : IDisposable
    {
        readonly ContractMap contracts;
        readonly HitMaps hitMaps;
        readonly BranchMaps branchMaps;
        readonly Stream stream;
        readonly XmlWriter writer;
        bool disposed = false;

        public ContractCoverageWriter(string filename, ContractMap contracts, HitMaps hitMaps, BranchMaps branchMaps)
        {
            this.contracts = contracts;
            this.hitMaps = hitMaps;
            this.branchMaps = branchMaps;

            stream = File.OpenWrite(filename);
            writer = new XmlTextWriter(stream, System.Text.Encoding.UTF8)
            {
                Formatting = Formatting.Indented
            };
        }

        public void Flush()
        {
            if (disposed) throw new ObjectDisposedException(nameof(ContractCoverageWriter));
            writer.Flush();
            stream.Flush();
        }

        public void Dispose()
        {
            if (!disposed)
            {
                writer.Dispose();
                stream.Dispose();
                disposed = true;
            }
        }

        // https://github.com/cobertura/cobertura/blob/master/cobertura/src/site/htdocs/xml/coverage-loose.dtd
        public void WriteCoberturaCoverage()
        {
            if (disposed) throw new ObjectDisposedException(nameof(ContractCoverageWriter));

            using (var _ = writer.StartDocument())
            using (var __ = writer.StartElement("coverage"))
            {
                writer.WriteAttributeString("version", ThisAssembly.AssemblyInformationalVersion);
                writer.WriteAttributeString("timestamp", $"{DateTime.Now.Ticks}");

                using (var ___ = writer.StartElement("packages"))
                {
                    foreach (var kvp in contracts)
                    {
                        WriteCoberturaPackage(kvp.Key.name, kvp.Value);
                    }
                }
            }
        }

        void WriteCoberturaPackage(string name, IEnumerable<SequencePoint> sequencePoints)
        {
            using (var _ = writer.StartElement("package"))
            {
                writer.WriteAttributeString("name", name);
                using (var __ = writer.StartElement("classes"))
                {
                    foreach (var group in sequencePoints.GroupBy(sp => (sp.Namespace, sp.Document)))
                    {
                        WriteCoberturaClass(group.Key.Namespace, group.Key.Document, group);
                    }
                }
            }
        }

        void WriteCoberturaClass(string name, string filename, IEnumerable<SequencePoint> sequencePoints)
        {
            using (var _ = writer.StartElement("class"))
            {
                writer.WriteAttributeString("name", name);
                writer.WriteAttributeString("filename", filename);
                using (var __ = writer.StartElement("methods"))
                {
                    foreach (var group in sequencePoints.GroupBy(sp => (sp.Name)))
                    {
                        WriteCoberturaMethod(group.Key, group);
                    }
                }
            }
        }

        void WriteCoberturaMethod(string name, IEnumerable<SequencePoint> sequencePoints)
        {
            using (var _ = writer.StartElement("method"))
            {
                writer.WriteAttributeString("name", name);
                writer.WriteAttributeString("signature", "{TBD}");
                using (var __ = writer.StartElement("lines"))
                {
                    foreach (var sp in sequencePoints)
                    {
                        using (var ___ = writer.StartElement("line"))
                        {
                            writer.WriteAttributeString("name", $"{sp.Start.Line}");
                            writer.WriteAttributeString("hits", $"0");
                            writer.WriteAttributeString("branch", $"False");
                        }
                    }
                }
            }
        }
    }
}