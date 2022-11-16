using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.DataCollection;

namespace Neo.Collector
{
    using HitMaps = Dictionary<string, Dictionary<uint, uint>>;
    using BranchMaps = Dictionary<string, Dictionary<uint, (uint branchCount, uint continueCount)>>;

    [DataCollectorFriendlyName("Neo code coverage")]
    [DataCollectorTypeUri("my://new/datacollector")]
    public class ContractCoverageCollector : DataCollector, ITestExecutionEnvironmentSpecifier
    {
        DataCollectionEvents events;
        DataCollectionLogger logger;
        DataCollectionContext dataCtx;
        const string COVERAGE_PATH_ENV_NAME = "NEO_TEST_APP_ENGINE_COVERAGE_PATH";
        const string COVERAGE_FILE_EXT = ".neo-coverage";
        const string DEBUG_INFO_PATH_ELEMENT = "DebugInfoPath";
        readonly string coveragePath;

        public ContractCoverageCollector()
        {
            coveragePath = GetTempPath();
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
            events.SessionEnd += OnSessionEnd;

            var debugInfoPaths = configurationElement.GetElementsByTagName(DEBUG_INFO_PATH_ELEMENT);
            for (var i = 0; i < debugInfoPaths.Count; i++)
            {
                var pathNode = debugInfoPaths[i];
                logger.LogWarning(dataCtx, $"Initialize {pathNode.Value}");
            }
        }

        protected override void Dispose(bool disposing)
        {
            events.SessionEnd -= OnSessionEnd;
            base.Dispose(disposing);
        }

        public IEnumerable<KeyValuePair<string, string>> GetTestExecutionEnvironmentVariables()
        {
            logger.LogWarning(dataCtx, $"GetTestExecutionEnvironmentVariables {coveragePath}");
            yield return new KeyValuePair<string, string>(COVERAGE_PATH_ENV_NAME, coveragePath);
        }

        static string GetTempPath()
        {
            string tempPath;
            do
            {
                tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            }
            while (Directory.Exists(tempPath));
            return tempPath;
        }

        private void OnSessionEnd(object sender, SessionEndEventArgs e)
        {
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