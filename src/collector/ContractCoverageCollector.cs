using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.DataCollection;

namespace Neo.Collector
{
    using HitMaps = Dictionary<string, Dictionary<uint, uint>>;
    using BranchMaps = Dictionary<string, Dictionary<uint, (uint branchCount, uint continueCount)>>;

    [DataCollectorFriendlyName("Neo code coverage")]
    [DataCollectorTypeUri("my://new/datacollector")]
    public class ContractCoverageCollector : DataCollector, ITestExecutionEnvironmentSpecifier
    {
        const string COVERAGE_PATH_ENV_NAME = "NEO_TEST_APP_ENGINE_COVERAGE_PATH";
        const string COVERAGE_FILE_EXT = ".neo-coverage";
        const string TEST_HARNESS_NAMESPACE = "NeoTestHarness";
        const string CONTRACT_ATTRIBUTE_NAME = "ContractAttribute";
        const string SEQUENCE_POINT_ATTRIBUTE_NAME = "SequencePointAttribute";

        readonly string coveragePath;
        readonly Dictionary<string, IReadOnlyDictionary<uint, SequencePoint>> contractSequencePoints 
            = new Dictionary<string, IReadOnlyDictionary<uint, SequencePoint>>();
        DataCollectionEvents events;
        DataCollectionSink dataSink;
        DataCollectionLogger logger;
        DataCollectionContext dataCtx;

        public ContractCoverageCollector()
        {
            coveragePath = GetTempFile();
        }

        static string GetTempFile()
        {
            string tempPath;
            do
            {
                tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            }
            while (Directory.Exists(tempPath));
            return tempPath;
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
                    if (TryGetContractAttribute(type, out var name, out var _hash)
                        && TryParseScriptHash(_hash, out var hash))
                    {
                        var spMap = GetSequencePoints(type).ToDictionary(sp => sp.Address);
                        if (spMap.Count > 0)
                        {
                            contractSequencePoints[hash] = spMap;
                        }
                    }
                }
            }
        }

        bool TryGetContractAttribute(TypeInfo type, out string name, out string hash)
        {
            foreach (var a in type.GetCustomAttributesData())
            {
                if (a.AttributeType.Name == CONTRACT_ATTRIBUTE_NAME && a.AttributeType.Namespace == TEST_HARNESS_NAMESPACE)
                {
                    name = (string)a.ConstructorArguments[0].Value;
                    hash = a.ConstructorArguments.Count == 2
                        ? (string)a.ConstructorArguments[1].Value
                        : string.Empty;
                    return true;
                }
            }
            name = string.Empty;
            hash = string.Empty;
            return false;
        }

        IEnumerable<SequencePoint> GetSequencePoints(TypeInfo type)
        {
            foreach (var a in type.GetCustomAttributesData())
            {
                if (a.AttributeType.Name == SEQUENCE_POINT_ATTRIBUTE_NAME && a.AttributeType.Namespace == TEST_HARNESS_NAMESPACE)
                {
                    var path = (string)a.ConstructorArguments[0].Value;
                    var address = (uint)a.ConstructorArguments[1].Value;
                    var startLine = (uint)a.ConstructorArguments[2].Value;
                    var startColumn = (uint)a.ConstructorArguments[3].Value;
                    var endLine = (uint)a.ConstructorArguments[4].Value;
                    var endColumn = (uint)a.ConstructorArguments[5].Value;

                    yield return new SequencePoint(path, address, (startLine, startColumn), (endLine, endColumn));
                }
            }
        }

        static bool TryParseScriptHash(string text, out string hash)
        {
            if (TryParseHexString(text, out var buffer)
                && buffer.Length == 20)
            {
                hash = BitConverter.ToString(buffer);
                return true;
            }

            hash = string.Empty;
            return false;
        }

        static bool TryParseHexString(string text, out byte[] buffer)
        {
            buffer = Array.Empty<byte>();
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text.Substring(2);
            if (text.Length % 2 != 0) return false;

            var length = text.Length / 2;
            buffer = new byte[length];
            for (var i = 0; i < length; i++)
            {
                var str = text.Substring(i * 2, 2);
                if (!byte.TryParse(str, NumberStyles.AllowHexSpecifier, null, out buffer[i]))
                {
                    return false;
                }
            }
            return true;
        }

        void OnSessionEnd(object sender, SessionEndEventArgs e)
        {
            logger.LogWarning(dataCtx, $"OnSessionStart {e.Context.SessionId}");

            var (hitMaps, branchMaps) = ParseRawCoverageFiles();

            var reportPath = Path.Combine(coveragePath, "neo.coverage.xml");
            using (var stream = File.OpenWrite(reportPath))
            using (var writer = new XmlTextWriter(stream, System.Text.Encoding.UTF8))
            {
                writer.Formatting = Formatting.Indented;
                WriteCoberturaCoverageFile(writer, hitMaps, branchMaps);
                writer.Flush();
                stream.Flush();
            }
            dataSink.SendFileAsync(dataCtx, reportPath, false);
        }

        // https://github.com/cobertura/cobertura/blob/master/cobertura/src/site/htdocs/xml/coverage-loose.dtd
        void WriteCoberturaCoverageFile(XmlWriter writer, HitMaps hitMaps, BranchMaps branchMaps)
        {
            using (var doc = writer.StartDocument())
            using (var coverage = writer.StartElement("coverage"))
            {
                writer.WriteAttributeString("version", ThisAssembly.AssemblyInformationalVersion);
                writer.WriteAttributeString("timestamp", $"{DateTime.Now.Ticks}");

                using (var packages = writer.StartElement("packages"))
                using (var package = writer.StartElement("package"))
                {
                    writer.WriteAttributeString("name", "<Contract name TBD>");

                    using (var classes = writer.StartElement("classes"))
                    using (var @class = writer.StartElement("class"))
                    {
                        writer.WriteAttributeString("name", "<class name TBD>");
                        writer.WriteAttributeString("filename", "<class filename TBD>");
                    }
                }
            }
        }

        (HitMaps hitMaps, BranchMaps branchMaps) ParseRawCoverageFiles()
        {
            var hitMaps = new HitMaps();
            var branchMaps = new BranchMaps();

            foreach (var filename in Directory.EnumerateFiles(coveragePath))
            {
                try
                {
                    if (Path.GetExtension(filename) != COVERAGE_FILE_EXT) continue;
                    ParseRawCoverageFile(filename, hitMaps, branchMaps);
                }
                catch (Exception ex)
                {
                    logger.LogException(dataCtx, ex, DataCollectorMessageLevel.Error);
                }
            }

            return (hitMaps, branchMaps);
        }

        void ParseRawCoverageFile(string filename, HitMaps hitMaps, BranchMaps branchMaps)
        {
            using (var stream = File.OpenRead(filename))
            using (var reader = new StreamReader(stream))
            {
                var hash = string.Empty;
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    if (line.StartsWith("0x"))
                    {
                        hash = TryParseScriptHash(line, out var value) 
                            ? value 
                            : throw new FormatException($"could not parse script hash {line}");
                    }
                    else
                    {
                        var values = line.Trim().Split(' ');
                        var ip = uint.Parse(values[0].Trim());

                        if (!hitMaps.TryGetValue(hash, out var hitMap))
                        {
                            hitMap = new Dictionary<uint, uint>();
                            hitMaps[hash] = hitMap;
                        }
                        hitMap[ip] = hitMap.TryGetValue(ip, out var hitCount) ? hitCount + 1 : 1;

                        switch (values.Length)
                        {
                            case 1:
                                break;
                            case 3:
                                {
                                    var offset = uint.Parse(values[1].Trim());
                                    var branchResult = uint.Parse(values[2].Trim());

                                    if (!branchMaps.TryGetValue(hash, out var branchMap))
                                    {
                                        branchMap = new Dictionary<uint, (uint branchCount, uint continueCount)>();
                                        branchMaps[hash] = branchMap;
                                    }
                                    var (branchCount, continueCount) = branchMap.TryGetValue(ip, out var _branchHit)
                                        ? _branchHit 
                                        : (branchCount: 0, continueCount: 0);
                                    branchMap[ip] = branchResult == offset
                                        ? (branchCount, continueCount + 1)
                                        : branchResult == ip
                                            ? (branchCount + 1, continueCount)
                                            : throw new FormatException($"Branch result {branchResult} did not equal {ip} or {offset}");
                                }
                                break;
                            default:
                                throw new FormatException($"Unexpected number of values ({values.Length})");
                        }
                    }
                }
            }
        }
    }
}