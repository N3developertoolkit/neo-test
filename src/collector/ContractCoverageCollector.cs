using System;
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
        const string MANIFEST_FILE_ATTRIBUTE_NAME = "ManifestFileAttribute";
        const string SEQUENCE_POINT_ATTRIBUTE_NAME = "SequencePointAttribute";

        readonly string coveragePath;
        readonly IDictionary<Hash160, ContractCoverage> contractMap = new Dictionary<Hash160, ContractCoverage>();
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
                        && ContractCoverage.TryCreate(contractName, manifestPath, out var coverage))
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
            // TODO: Read .neo-script files
            logger.LogWarning(dataCtx, $"OnSessionEnd {e.Context.SessionId}");

            foreach (var filename in Directory.EnumerateFiles(coveragePath))
            {
                try
                {
                    if (Path.GetExtension(filename) != COVERAGE_FILE_EXT) continue;
                    ParseRawCoverageFile(filename);
                }
                catch (Exception ex)
                {
                    logger.LogException(dataCtx, ex, DataCollectorMessageLevel.Error);
                }
            }

            var coverageReportPath = Path.Combine(coveragePath, $"neo.cobertura.xml");
            WriteAttachment(coverageReportPath, textWriter =>
            {
                try
                {
                    // XmlTextWriter *NOT* in a using block because WriteAttachment will flush 
                    // and close the stream
                    var writer = new XmlTextWriter(textWriter);
                    using (var _ = writer.StartDocument())
                    using (var __ = writer.StartElement("coverage"))
                    {
                        writer.WriteAttributeString("version", ThisAssembly.AssemblyInformationalVersion);
                        writer.WriteAttributeString("timestamp", $"{DateTime.Now.Ticks}");

                        using (var ___ = writer.StartElement("packages"))
                        {
                            foreach (var coverage in contractMap.Values)
                            {
                                coverage.WriteCoberturaPackage(writer);
                            }
                        }
                    }
                    writer.Flush();
                }
                catch (Exception ex)
                {
                    logger.LogError(dataCtx, ex.Message);
                }

            });

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
                        hash = Hash160.TryParse(line, out var value)
                            ? value
                            : throw new FormatException($"could not parse script hash {line}");
                    }
                    else
                    {
                        if (contractMap.TryGetValue(hash, out var coverage))
                        {
                            var values = line.Trim().Split(' ');
                            var ip = uint.Parse(values[0].Trim());

                            if (values.Length == 1)
                            {
                                coverage.RecordHit(ip);
                            }
                            else if (values.Length == 3)
                            {
                                var offset = uint.Parse(values[1].Trim());
                                var branchResult = uint.Parse(values[2].Trim());
                                coverage.RecordBranch(ip, offset, branchResult);
                            }
                            else
                            {
                                throw new FormatException($"Unexpected number of values ({values.Length})");
                            }
                        }
                    }
                }
            }
        }
    }
}