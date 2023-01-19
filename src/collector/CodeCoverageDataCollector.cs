using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.DataCollection;
using Neo.Collector.Formats;
using Neo.Collector.Models;

namespace Neo.Collector
{
    [DataCollectorFriendlyName("Neo code coverage")]
    [DataCollectorTypeUri("datacollector://Neo/ContractCodeCoverage/1.0")]
    public partial class CodeCoverageDataCollector : DataCollector, ITestExecutionEnvironmentSpecifier
    {
        const string COVERAGE_PATH_ENV_NAME = "NEO_TEST_APP_ENGINE_COVERAGE_PATH";
        private const string MANIFEST_FILE_EXT = ".manifest.json";
        private const string NEF_FILE_EXT = ".nef";
        const string TEST_HARNESS_NAMESPACE = "NeoTestHarness";
        const string CONTRACT_ATTRIBUTE_NAME = "ContractAttribute";

        readonly string coveragePath;
        CodeCoverageCollector collector;

        DataCollectionEvents events;
        DataCollectionSink dataSink;
        DataCollectionEnvironmentContext environmentContext;
        ILogger logger;
        XmlElement configXml;

        public CodeCoverageDataCollector()
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
            this.configXml = configurationElement;
            this.events = events;
            this.dataSink = dataSink;
            this.environmentContext = environmentContext;
            this.logger = new Logger(logger, environmentContext);

            events.SessionStart += OnSessionStart;
            events.SessionEnd += OnSessionEnd;
            collector = new CodeCoverageCollector(this.logger);
        }

        protected override void Dispose(bool disposing)
        {
            events.SessionStart -= OnSessionStart;
            events.SessionEnd -= OnSessionEnd;
            base.Dispose(disposing);
        }

        public IEnumerable<KeyValuePair<string, string>> GetTestExecutionEnvironmentVariables()
        {
            yield return new KeyValuePair<string, string>(COVERAGE_PATH_ENV_NAME, coveragePath);
        }

        void OnSessionStart(object sender, SessionStartEventArgs e)
        {
            logger.LogWarning($"OnSessionStart {configXml.OuterXml}");

            foreach (XmlNode node in configXml.GetElementsByTagName("DebugInfo"))
            {
                if (NeoDebugInfo.TryLoad(node.InnerText, out var debugInfo))
                {
                    var baseName = Path.GetFileNameWithoutExtension(node.InnerText);
                    collector.TrackContract(baseName, debugInfo);
                }
            }

            var testSources = e.GetPropertyValue<IList<string>>("TestSources");

            for (int i = 0; i < testSources.Count; i++)
            {
                LoadTestSource(testSources[i]);
            }
        }

        internal void LoadTestSource(string testSource)
        {
            if (TryLoadAssembly(testSource, out var asm))
            {
                foreach (var type in asm.DefinedTypes)
                {
                    if (TryGetContractAttribute(type, out var contractName, out var manifestPath))
                    {
                        // Note, some file systems are case sensitive. 
                        // Using StringComparison.OrdinalIgnoreCase could lead to incorrect base names on such systems. 
                        var baseName = manifestPath.EndsWith(MANIFEST_FILE_EXT, StringComparison.OrdinalIgnoreCase)
                            ? manifestPath.Substring(0, manifestPath.Length - MANIFEST_FILE_EXT.Length)
                            : manifestPath;
                        var nefPath = Path.Combine(
                            Path.GetDirectoryName(manifestPath),
                            Path.ChangeExtension(baseName, NEF_FILE_EXT));

                        if (NeoDebugInfo.TryLoadContractDebugInfo(nefPath, out var debugInfo))
                        {
                            collector.TrackContract(contractName, debugInfo);
                        }
                    }
                }
            }
        }

        static bool TryLoadAssembly(string path, out Assembly assembly)
        {
            if (File.Exists(path))
            {
                try
                {
                    assembly = Assembly.LoadFile(path);
                    return true;
                }
                catch { }
            }

            assembly = default;
            return false;
        }

        static bool TryGetContractAttribute(TypeInfo type, out string name, out string manifestPath)
        {
            if (type.IsInterface)
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
            }

            name = "";
            manifestPath = "";
            return false;
        }

        void OnSessionEnd(object sender, SessionEndEventArgs e)
        {
            foreach (var filename in Directory.EnumerateFiles(coveragePath))
            {
                logger.LogWarning($"  {filename}");

                try
                {
                    collector.LoadSessionOutput(filename);
                }
                catch (Exception ex)
                {
                    logger.LogError(string.Empty, ex);
                }
            }

            var reportPath = Path.Combine(coveragePath, $"neo.cobertura.xml");
            WriteAttachment(reportPath, stream =>
            {
                var format = new CoberturaFormat();
                format.WriteReport(stream, collector.CollectCoverage());
            });

            foreach (var contract in collector.ContractCollectors)
            {
                reportPath = Path.Combine(coveragePath, $"{contract.ScriptHash}.coverage.txt");
                WriteAttachment(reportPath, stream =>
                {
                    var writer = new StreamWriter(stream);
                    writer.WriteLine("HITS");
                    foreach (var hit in contract.HitMap.OrderBy(h => h.Key))
                    {
                        writer.WriteLine($"{hit.Key} {hit.Value}");
                    }
                    writer.WriteLine("BRANCHES");
                    foreach (var hit in contract.BranchMap.OrderBy(h => h.Key))
                    {
                        writer.WriteLine($"{hit.Key} {hit.Value.branchCount} {hit.Value.continueCount}");
                    }
                    writer.Flush();
                });
            }
        }

        void WriteAttachment(string filename, Action<Stream> writeAttachment)
        {
            try
            {
                logger.LogWarning($"  WriteAttachment {filename}");

                using (var stream = File.OpenWrite(filename))
                {
                    writeAttachment(stream);
                    stream.Flush();
                }
                dataSink.SendFileAsync(environmentContext.SessionDataCollectionContext, filename, false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex.Message);
            }
        }
    }
}