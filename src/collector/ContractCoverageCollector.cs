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
    using ContractMap = Dictionary<(string name, string hash), IEnumerable<SequencePoint>>;
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
        readonly ContractMap contractSequencePoints = new ContractMap();
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
                        && _hash.TryParseScriptHash(out var hash))
                    {
                        var sequencePoints = GetSequencePoints(type).ToList();
                        if (sequencePoints.Count > 0)
                        {
                            contractSequencePoints[(name, hash)] = sequencePoints;
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
                    var @namespace = (string)a.ConstructorArguments[1].Value;
                    var name = (string)a.ConstructorArguments[2].Value;
                    var address = (uint)a.ConstructorArguments[3].Value;
                    var startLine = (uint)a.ConstructorArguments[4].Value;
                    var startColumn = (uint)a.ConstructorArguments[5].Value;
                    var endLine = (uint)a.ConstructorArguments[6].Value;
                    var endColumn = (uint)a.ConstructorArguments[7].Value;

                    yield return new SequencePoint(path, @namespace, name, address, (startLine, startColumn), (endLine, endColumn));
                }
            }
        }

        void OnSessionEnd(object sender, SessionEndEventArgs e)
        {
            logger.LogWarning(dataCtx, $"OnSessionStart {e.Context.SessionId}");

            var (hitMaps, branchMaps) = ParseRawCoverageFiles();

            var reportPath = Path.Combine(coveragePath, "neo.coverage.xml");
            using (var writer = new ContractCoverageWriter(reportPath, contractSequencePoints, hitMaps, branchMaps))
            {
                writer.Flush();
            }
            dataSink.SendFileAsync(dataCtx, reportPath, false);
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
                        hash = line.TryParseScriptHash(out var value) 
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