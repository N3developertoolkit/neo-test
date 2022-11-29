﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.DataCollection;
using Neo.Collector.Models;

namespace Neo.Collector
{
    [DataCollectorFriendlyName("Neo code coverage")]
    [DataCollectorTypeUri("my://new/datacollector")]
    public class ContractCoverageCollector : DataCollector, ITestExecutionEnvironmentSpecifier
    {
        const string COVERAGE_PATH_ENV_NAME = "NEO_TEST_APP_ENGINE_COVERAGE_PATH";
        const string COVERAGE_FILE_EXT = ".neo-coverage";
        const string SCRIPT_FILE_EXT = ".neo-script";
        const string TEST_HARNESS_NAMESPACE = "NeoTestHarness";
        const string CONTRACT_ATTRIBUTE_NAME = "ContractAttribute";
        // const string MANIFEST_FILE_ATTRIBUTE_NAME = "ManifestFileAttribute";
        // const string SEQUENCE_POINT_ATTRIBUTE_NAME = "SequencePointAttribute";

        readonly string coveragePath;
        readonly IDictionary<Hash160, ContractCoverageManager> contractMap = new Dictionary<Hash160, ContractCoverageManager>();
        DataCollectionEvents events;
        DataCollectionSink dataSink;
        DataCollectionLogger logger;
        DataCollectionContext dataCtx;

        public ContractCoverageCollector()
        {
            do
            {
                coveragePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            }
            while (Directory.Exists(coveragePath));
        }

        public override void Initialize(
                XmlElement configurationElement,
                DataCollectionEvents events,
                DataCollectionSink dataSink,
                DataCollectionLogger logger,
                DataCollectionEnvironmentContext environmentContext)
        {
            this.events = events;
            this.dataSink = dataSink;
            this.logger = logger;
            dataCtx = environmentContext.SessionDataCollectionContext;
            events.SessionStart += OnSessionStart;
            events.SessionEnd += OnSessionEnd;
        }

        protected override void Dispose(bool disposing)
        {
            events.SessionStart -= OnSessionStart;
            events.SessionEnd -= OnSessionEnd;
            base.Dispose(disposing);
        }
        public IEnumerable<KeyValuePair<string, string>> GetTestExecutionEnvironmentVariables()
        {
            logger.LogWarning(dataCtx, $"GetTestExecutionEnvironmentVariables {coveragePath}");
            yield return new KeyValuePair<string, string>(COVERAGE_PATH_ENV_NAME, coveragePath);
        }

        void OnSessionStart(object sender, SessionStartEventArgs e)
        {
            logger.LogWarning(dataCtx, $"OnSessionStart {e.Context.SessionId}");
            var testSources = e.GetPropertyValue<IList<string>>("TestSources");

            foreach (var src in testSources)
            {
                var asm = Assembly.LoadFile(src);
                foreach (var type in asm.DefinedTypes)
                {
                    if (TryGetContractAttribute(type, out var contractName, out var manifestPath) 
                        && ContractCoverageManager.TryCreate(contractName, manifestPath, out var coverage))
                    {
                        logger.LogWarning(dataCtx, $"  {contractName} {coverage.ScriptHash}");
                        contractMap.Add(coverage.ScriptHash, coverage);
                    }
                }
            }
        }

        bool TryGetContractAttribute(TypeInfo type, out string name, out string manifestPath)
        {
            foreach (var a in type.GetCustomAttributesData())
            {
                if (a.AttributeType.Name == CONTRACT_ATTRIBUTE_NAME && a.AttributeType.Namespace == TEST_HARNESS_NAMESPACE)
                {
                    name = (string)a.ConstructorArguments[0].Value;
                    manifestPath = (string)a.ConstructorArguments[1].Value;
                    return true;
                }
            }

            name = "";
            manifestPath = "";
            return false;
        }

        void OnSessionEnd(object sender, SessionEndEventArgs e)
        {
            logger.LogWarning(dataCtx, $"OnSessionEnd {e.Context.SessionId}");

            int rawCoverageFileCount = 0;
            foreach (var filename in Directory.EnumerateFiles(coveragePath))
            {
                logger.LogWarning(dataCtx, $"  {filename}");

                try
                {
                    var ext = Path.GetExtension(filename);
                    if (ext == COVERAGE_FILE_EXT)
                    {
                        rawCoverageFileCount++;
                        ParseRawCoverageFile(filename);
                    }
                    if (ext == SCRIPT_FILE_EXT)
                    {
                        ParseScriptFile(filename);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogException(dataCtx, ex, DataCollectorMessageLevel.Error);
                }
            }

            var reportPath = Path.Combine(coveragePath, $"neo.coverage.xml");
            WriteAttachment(reportPath, textWriter =>
            {
                // XmlTextWriter *NOT* in a using block because WriteAttachment will flush 
                // and close the stream
                var writer = new XmlTextWriter(textWriter)
                {
                    Formatting = Formatting.Indented,
                };
                WriteCoverage(writer, rawCoverageFileCount);
                writer.Flush();
            });
            // foreach (var coverage in contractMap.Values)
            // {
            //     logger.LogWarning(dataCtx, $"  {coverage.ContractName} {coverage.ScriptHash}");
            //     foreach (var group in coverage.GetMethodCoverages().GroupBy(m => m.Namespace))
            //     {
            //         logger.LogWarning(dataCtx, $"    {group.Key}");
            //         foreach (var method in group)
            //         {
            //             logger.LogWarning(dataCtx, $"      {method.Name}");
            //             foreach (var line in method.Lines)
            //             {
            //                 logger.LogWarning(dataCtx, $"        {line.SequencePoint.Start.Line}");
            //                 foreach (var br in line.BranchInstructions)
            //                 {
            //                     logger.LogWarning(dataCtx, $"          {br.address} {br.instruction.OpCode}");
            //                 }
            //                 // foreach (var (address, instruction) in line.Instructions)
            //                 // {
            //                 //     logger.LogWarning(dataCtx, $"          {address} {instruction.OpCode}");
            //                 // }
            //             }
            //         }
            //     }
            // }


            // var coverageReportPath = Path.Combine(coveragePath, $"neo.cobertura.xml");
            // WriteAttachment(coverageReportPath, textWriter =>
            // {
            //     // XmlTextWriter *NOT* in a using block because WriteAttachment will flush 
            //     // and close the stream
            //     var writer = new XmlTextWriter(textWriter)
            //     {
            //         Formatting = Formatting.Indented,
            //     };
            //     WriteCoberturaCoverage(writer);
            //     writer.Flush();
            // });

            foreach (var coverage in contractMap)
            {
                var rawReportPath = Path.Combine(coveragePath, $"{coverage.Key}.raw.txt");
                WriteAttachment(rawReportPath, writer =>
                {
                    writer.WriteLine("HITS");
                    foreach (var hit in coverage.Value.HitMap.OrderBy(t => t.Key))
                    {
                        writer.WriteLine($"{hit.Key} {hit.Value}");
                    }
                    writer.WriteLine("BRANCHES");
                    foreach (var br in coverage.Value.BranchMap.OrderBy(t => t.Key))
                    {
                        writer.WriteLine($"{br.Key} {br.Value.branchCount} {br.Value.continueCount}");
                    }
                });
            }
        }

        void WriteCoverage(XmlWriter writer, int rawCoverageFileCount)
        {
            using (var _ = writer.StartDocument())
            using (var __ = writer.StartElement("coverage"))
            {
                writer.WriteAttributeString("version", ThisAssembly.AssemblyInformationalVersion);
                writer.WriteAttributeString("timestamp", $"{DateTimeOffset.Now.ToUnixTimeMilliseconds()}");
                writer.WriteAttributeString("testrun-count", $"{rawCoverageFileCount}");

                using (var ___ = writer.StartElement("contracts"))
                {
                    foreach (var coverageMgr in contractMap.Values)
                    {
                        WriteCoverage(writer, coverageMgr.GetCoverage(rawCoverageFileCount));
                    }
                }
            }
        }

        void WriteCoverage(XmlWriter writer, ContractCoverage coverage)
        {
            using (var _ = writer.StartElement("contract"))
            {
                writer.WriteAttributeString("name", coverage.ContractName);
                writer.WriteAttributeString("scripthash", $"{coverage.ScriptHash}");

                using (var __ = writer.StartElement("methods"))
                {
                    foreach (var method in coverage.Methods)
                    {
                        WriteCoverage(writer, method);
                    }
                }
            }
        }

        void WriteCoverage(XmlWriter writer, MethodCoverage coverage)
        {
            using (var _ = writer.StartElement("method"))
            {
                writer.WriteAttributeString("namespace", coverage.Namespace);
                writer.WriteAttributeString("name", coverage.Name);
                writer.WriteAttributeString("document", coverage.Document);

                using (var __ = writer.StartElement("lines"))
                {
                    foreach (var line in coverage.Lines)
                    {
                        WriteCoverage(writer, line);
                    }
                }
            }
        }

        void WriteCoverage(XmlWriter writer, LineCoverage coverage)
        {
            using (var _1 = writer.StartElement("line"))
            {
                writer.WriteAttributeString("number", $"{coverage.Start.Line}");
                writer.WriteAttributeString("address", $"{coverage.Address}");
                writer.WriteAttributeString("hits", $"{coverage.HitCount}");

                if (coverage.Branches.Count == 0)
                {
                    writer.WriteAttributeString("branch", "False");
                }
                else
                {
                    writer.WriteAttributeString("branch", "True");
                    using (var __ = writer.StartElement("branches"))
                    {
                        foreach (var branch in coverage.Branches)
                        {
                            using (var ___ = writer.StartElement("branch"))
                            {
                                writer.WriteAttributeString("address", $"{branch.Address}");
                                writer.WriteAttributeString("branch-count", $"{branch.BranchCount}");
                                writer.WriteAttributeString("continue-count", $"{branch.ContinueCount}");
                            }
                        }
                    }
                }
            }
        }

        void WriteAttachment(string filename, Action<TextWriter> writeAttachment)
        {
            try
            {
                logger.LogWarning(dataCtx, $"  WriteAttachment {filename}");

                using (var stream = File.OpenWrite(filename))
                using (var writer = new StreamWriter(stream))
                {
                    writeAttachment(writer);
                    writer.Flush();
                    stream.Flush();
                }
                dataSink.SendFileAsync(dataCtx, filename, false);
            }
            catch (Exception ex)
            {
                logger.LogError(dataCtx, ex.Message);
            }
        }

        private void ParseScriptFile(string filename)
        {
            if (Hash160.TryParse(Path.GetFileNameWithoutExtension(filename), out var hash)
                && contractMap.TryGetValue(hash, out var coverage))
            {
                coverage.RecordScript(File.ReadAllBytes(filename));
            }
        }

        void ParseRawCoverageFile(string filename)
        {
            using (var stream = File.OpenRead(filename))
            using (var reader = new StreamReader(stream))
            {
                var hash = Hash160.Zero;
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    if (line.StartsWith("0x"))
                    {
                        hash = Hash160.TryParse(line.Trim(), out var value)
                            ? value
                            : Hash160.Zero;
                    }
                    else
                    {
                        if (hash != Hash160.Zero 
                            && contractMap.TryGetValue(hash, out var coverage))
                        {
                            var values = line.Trim().Split(' ');
                            if (values.Length > 0
                                && int.TryParse(values[0].Trim(), out var ip))
                            {
                                if (values.Length == 1)
                                {
                                    coverage.RecordHit(ip);
                                }
                                else if (values.Length == 3
                                    && int.TryParse(values[1].Trim(), out var offset)
                                    && int.TryParse(values[2].Trim(), out var branchResult))
                                {
                                    coverage.RecordBranch(ip, offset, branchResult);
                                }
                                else
                                {
                                    logger.LogWarning(dataCtx, $"Invalid raw coverage data line '{line}'");
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}