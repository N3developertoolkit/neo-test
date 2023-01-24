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

            this.logger.LogWarning($"Initialize {this.coveragePath}");
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
            foreach (var testSource in e.GetPropertyValue<IList<string>>("TestSources"))
            {
                LoadTestSource(testSource);
            }

            foreach (XmlElement node in configXml.SelectNodes("DebugInfo"))
            {
                LoadDebugInfoSetting(node);
            }
        }

        internal void LoadDebugInfoSetting(XmlElement node)
        {
            logger.LogWarning($"LoadDebugInfoSetting {node.InnerText}");
            if (NeoDebugInfo.TryLoad(node.InnerText, out var debugInfo))
            {
                var name = node.HasAttribute("name")
                    ? node.GetAttribute("name")
                    : Path.GetFileNameWithoutExtension(node.InnerText);
                collector.TrackContract(name, debugInfo);
            }
            else
            {
                logger.LogError($"LoadDebugInfoSetting {node.InnerText}");
            }
        }

        internal void LoadTestSource(string testSource)
        {
            logger.LogWarning($"LoadTestSource {testSource}");
            if (Utility.TryLoadAssembly(testSource, out var asm))
            {
                foreach (var type in asm.DefinedTypes)
                {
                    if (TryGetContractAttribute(type, out var contractName, out var manifestPath)
                        && NeoDebugInfo.TryLoadManifestDebugInfo(manifestPath, out var debugInfo))
                    {
                        collector.TrackContract(contractName, debugInfo);
                    }
                }
            }
            else
            {
                logger.LogError($"LoadTestSource {testSource}");
            }
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
            collector.LoadCoverageFiles(coveragePath);
            IReadOnlyList<ContractCoverage> coverage = collector.CollectCoverage().ToList();

            new CoberturaFormat().WriteReport(coverage, WriteAttachment);
            new RawCoverageFormat().WriteReport(coverage, WriteAttachment);
        }

        void WriteAttachment(string filename, Action<Stream> writeAttachment)
        {
            try
            {
                var path = Path.Combine(coveragePath, filename);
                using (var stream = File.OpenWrite(path))
                {
                    writeAttachment(stream);
                    stream.Flush();
                }
                dataSink.SendFileAsync(environmentContext.SessionDataCollectionContext, path, false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex.Message, ex);
                logger.LogError(ex.StackTrace);
            }
        }
    }
}