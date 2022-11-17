using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.DataCollection;

namespace Neo.Collector
{
    using HitMaps = Dictionary<string, Dictionary<uint, uint>>;
    using BranchMaps = Dictionary<string, Dictionary<uint, (uint branchCount, uint continueCount)>>;

    public class SequencePoint
    {
        public string Document { get; }
        public uint Address { get; }
        public (uint Line, uint Column) Start { get; }
        public (uint Line, uint Column) End { get; }

        public SequencePoint(string document, uint address, (uint Line, uint Column) start, (uint Line, uint Column) end)
        {
            Document = document;
            Address = address;
            Start = start;
            End = end;
        }
    }

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
        DataCollectionEvents events;
        DataCollectionLogger logger;
        DataCollectionContext dataCtx;
        IList<string> testSources;

        public ContractCoverageCollector()
        {
            do
            {
                coveragePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            }
            while (Directory.Exists(coveragePath));
        }

        public override void Initialize(
                System.Xml.XmlElement configurationElement,
                DataCollectionEvents events,
                DataCollectionSink dataSink,
                DataCollectionLogger logger,
                DataCollectionEnvironmentContext environmentContext)
        {
            this.logger = logger;
            this.events = events;
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
                    if (TryGetContractAttribute(type, out var name, out var hash))
                    {
                        logger.LogWarning(dataCtx, $"  {type.Namespace}.{type.Name}: {name} ({hash})");

                        foreach (var sp in GetSequencePoints(type))
                        {
                            var doc = Path.GetFileName(sp.Document);
                            logger.LogWarning(dataCtx, $"    {doc} {sp.Address} {sp.Start.Line}");   
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

        void OnSessionEnd(object sender, SessionEndEventArgs e)
        {
            logger.LogWarning(dataCtx, $"OnSessionStart {e.Context.SessionId}");

            var (hitMaps, branchMaps) = ParseCoverageFiles();
        }

        (HitMaps hitMaps, BranchMaps branchMaps) ParseCoverageFiles()
        {
            var hitMaps = new HitMaps();
            var branchMaps = new BranchMaps();

            foreach (var filename in Directory.EnumerateFiles(coveragePath))
            {
                try
                {
                    if (Path.GetExtension(filename) != COVERAGE_FILE_EXT) continue;
                    ParseCoverageFile(filename, hitMaps, branchMaps);
                }
                catch (Exception ex)
                {
                    logger.LogException(dataCtx, ex, DataCollectorMessageLevel.Error);
                }
            }

            return (hitMaps, branchMaps);
        }

        void ParseCoverageFile(string filename, HitMaps hitMaps, BranchMaps branchMaps)
        {
            logger.LogWarning(dataCtx, $"ParseCoverageFile {filename}");

            using (var stream = File.OpenRead(filename))
            using (var reader = new StreamReader(stream))
            {
                var hash = string.Empty;
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    if (line.StartsWith("0x"))
                    {
                        hash = line.Trim();
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
                                            : throw new FormatException();
                                }
                                break;
                            default:
                                throw new FormatException();
                        }
                    }
                }
            }
        }
    }
}