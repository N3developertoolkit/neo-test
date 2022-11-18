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
        readonly string manifestPath;
        readonly NeoDebugInfo debugInfo;
        readonly NefFile nefFile;
        readonly IDictionary<uint, uint> hitMap = new Dictionary<uint, uint>();
        readonly IDictionary<uint, (uint branchCount, uint continueCount)> branchMap = new Dictionary<uint, (uint branchCount, uint continueCount)>();

        public Hash160 ScriptHash { get; }
        public IReadOnlyDictionary<uint, uint> HitMap => (IReadOnlyDictionary<uint, uint>)hitMap;
        public IReadOnlyDictionary<uint, (uint branchCount, uint continueCount)> BranchMap => 
            (IReadOnlyDictionary<uint, (uint branchCount, uint continueCount)>)branchMap;

        public ContractCoverage(string contractName, string manifestPath, NeoDebugInfo debugInfo, NefFile nefFile)
        {
            this.contractName = contractName;
            this.manifestPath = manifestPath;
            this.debugInfo = debugInfo;
            this.nefFile = nefFile;
            ScriptHash = debugInfo.Hash;
            if (!(nefFile is null))
            {
                var hash = nefFile.CalculateScriptHash();
                if (!hash.Equals(debugInfo.Hash))
                {
                    throw new ArgumentException("Script hashes don't match");
                }
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
                value = new ContractCoverage(contractName, manifestPath, debugInfo, nefFile);
                return true;
            }

            value = null;
            return false;
        }

        // public void WriteCoberturaPackage(string filename)
        // {
        //     using (var stream = File.OpenWrite(filename))
        //     using (var writer = new XmlTextWriter(stream, System.Text.Encoding.UTF8))
        //     {
        //         WriteCoberturaCoverage(writer);
        //         writer.Flush();
        //         stream.Flush();
        //     }
        // }

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

            // using (var _ = writer.StartDocument())
            // using (var __ = writer.StartElement("coverage"))
            // {
            //     writer.WriteAttributeString("version", ThisAssembly.AssemblyInformationalVersion);
            //     writer.WriteAttributeString("timestamp", $"{DateTime.Now.Ticks}");

            //     using (var ___ = writer.StartElement("packages"))
            //     {
            //         foreach (var kvp in contracts)
            //         {
            //             WriteCoberturaPackage(kvp.Key.name, kvp.Value);
            //         }
            //     }
            // }

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
                var sig = string.Join(",", method.Parameters.Select(p => p.Type));
                writer.WriteAttributeString("name", method.Name);
                writer.WriteAttributeString("signature", $"({sig})");
                using (var __ = writer.StartElement("lines"))
                {
                    foreach (var sp in method.SequencePoints)
                    {
                        WriteCoberturaLine(writer, sp);
                    }
                }
            }
        }

        private void WriteCoberturaLine(XmlWriter writer, NeoDebugInfo.SequencePoint sp)
        {
            using (var _ = writer.StartElement("line"))
            {
                writer.WriteAttributeString("name", $"{sp.Start.Line}");
            }
        }
    }
}