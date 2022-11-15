using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.DataCollection;

namespace Neo.Collector
{
    [DataCollectorFriendlyName("Neo code coverage")]
    [DataCollectorTypeUri("my://new/datacollector")]
    public class ContractCoverageCollector : DataCollector, ITestExecutionEnvironmentSpecifier
    {
        DataCollectionEvents events;
        DataCollectionLogger logger;
        DataCollectionContext dataCtx;
        const string COVERAGE_PATH_ENV_NAME = "NEO_TEST_APP_ENGINE_COVERAGE_PATH";
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
            logger.LogWarning(environmentContext.SessionDataCollectionContext, "Initialize");

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
            logger.LogWarning(dataCtx, "Dispose");

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
            logger.LogWarning(dataCtx, $"OnSessionStart {e.Context.SessionId}");
        }

        private void OnSessionEnd(object sender, SessionEndEventArgs e)
        {
            logger.LogWarning(dataCtx, $"SessionEndEventArgs {e.Context.SessionId}");
        }

        private void OnTestHostLaunched(object sender, TestHostLaunchedEventArgs e)
        {
            logger.LogWarning(dataCtx, $"OnTestHostLaunched {e.Context.SessionId}");
        }

        private void OnTestCaseStart(object sender, TestCaseStartEventArgs e)
        {
            logger.LogWarning(dataCtx, $"OnTestCaseStart {e.Context.SessionId} {e.TestCaseName}");
        }

        private void OnTestCaseEnd(object sender, TestCaseEndEventArgs e)
        {
            logger.LogWarning(dataCtx, $"OnTestCaseEnd {e.Context.SessionId} {e.TestCaseName}");
        }
    }
}