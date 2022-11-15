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
            // logger.LogWarning(environmentContext.SessionDataCollectionContext, "Initialize");

            this.logger = logger;
            this.events = events;
            dataCtx = environmentContext.SessionDataCollectionContext;

            events.SessionStart += OnSessionStart;
            events.SessionEnd += OnSessionEnd;
            events.TestHostLaunched += OnTestHostLaunched;
            events.TestCaseStart += OnTestCaseStart;
            events.TestCaseEnd += OnTestCaseEnd;
        }

        protected override void Dispose(bool disposing)
        {
            // logger.LogWarning(dataCtx, "Dispose");

            events.SessionStart -= OnSessionStart;
            events.SessionEnd -= OnSessionEnd;
            events.TestHostLaunched -= OnTestHostLaunched;
            events.TestCaseStart -= OnTestCaseStart;
            events.TestCaseEnd -= OnTestCaseEnd;

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


        private void OnSessionStart(object sender, SessionStartEventArgs e)
        {
            // logger.LogWarning(dataCtx, $"OnSessionStart {e.Context.SessionId}");
        }

        private void OnSessionEnd(object sender, SessionEndEventArgs e)
        {
            // logger.LogWarning(dataCtx, $"SessionEndEventArgs {e.Context.SessionId}");
            ParseCoverageFiles();
        }

        private void OnTestHostLaunched(object sender, TestHostLaunchedEventArgs e)
        {
            // logger.LogWarning(dataCtx, $"OnTestHostLaunched {e.Context.SessionId}");
        }

        private void OnTestCaseStart(object sender, TestCaseStartEventArgs e)
        {
            // logger.LogWarning(dataCtx, $"OnTestCaseStart {e.Context.SessionId} {e.TestCaseName}");
        }

        private void OnTestCaseEnd(object sender, TestCaseEndEventArgs e)
        {
            // logger.LogWarning(dataCtx, $"OnTestCaseEnd {e.Context.SessionId} {e.TestCaseName}");
        }

        void ParseCoverageFiles()
        {
            foreach (var file in Directory.EnumerateFiles(coveragePath))
            {
                if (Path.GetExtension(file) == COVERAGE_FILE_EXT)
                {
                    var (hitMaps, branchMaps) = ParseCoverageFile(file);
                }
            }
        }

        (HitMaps, BranchMaps) ParseCoverageFile(string filename)
        {
            logger.LogWarning(dataCtx, $"ParseCoverageFile {filename}");

            var hitMaps = new HitMaps();
            var branchMaps = new BranchMaps();

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
                            case 2:
                                {
                                    var offset = uint.Parse(values[1].Trim());
                                    var brLine = reader.ReadLine();
                                    if (!brLine.StartsWith("br")) throw new FormatException();
                                    var branchResult = uint.Parse(line.Substring(2).Trim());

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
                            default: throw new FormatException();
                        }
                    }
                }
            }

            return (hitMaps, branchMaps);
        }
    }
}