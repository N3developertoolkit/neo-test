using System;
using System.Collections.Generic;
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

        public override void Initialize(
                System.Xml.XmlElement configurationElement,
                DataCollectionEvents events,
                DataCollectionSink dataSink,
                DataCollectionLogger logger,
                DataCollectionEnvironmentContext environmentContext)
        {
            logger.LogWarning(environmentContext.SessionDataCollectionContext, "ContractCoverageCollector::Initialize");

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
            logger.LogWarning(dataCtx, "ContractCoverageCollector::Dispose");

            events.SessionStart -= OnSessionStart;
            events.SessionEnd -= OnSessionEnd;
            events.TestHostLaunched -= OnTestHostLaunched;
            events.TestCaseStart -= OnTestCaseStart;
            events.TestCaseEnd -= OnTestCaseEnd;

            base.Dispose(disposing);
        }

        public IEnumerable<KeyValuePair<string, string>> GetTestExecutionEnvironmentVariables()
        {
            logger.LogWarning(dataCtx, $"ContractCoverageCollector::GetTestExecutionEnvironmentVariables");

            return Enumerable.Empty<KeyValuePair<string, string>>();
        }

        private void OnSessionStart(object sender, SessionStartEventArgs e)
        {
            logger.LogWarning(dataCtx, $"ContractCoverageCollector::OnSessionStart {e.Context.SessionId}");
        }

        private void OnSessionEnd(object sender, SessionEndEventArgs e)
        {
            logger.LogWarning(dataCtx, $"ContractCoverageCollector::SessionEndEventArgs {e.Context.SessionId}");
        }

        private void OnTestHostLaunched(object sender, TestHostLaunchedEventArgs e)
        {
            logger.LogWarning(dataCtx, $"ContractCoverageCollector::OnTestHostLaunched {e.Context.SessionId}");
        }

        private void OnTestCaseStart(object sender, TestCaseStartEventArgs e)
        {
            logger.LogWarning(dataCtx, $"ContractCoverageCollector::OnTestCaseStart {e.Context.SessionId} {e.TestCaseName}");
        }

        private void OnTestCaseEnd(object sender, TestCaseEndEventArgs e)
        {
            logger.LogWarning(dataCtx, $"ContractCoverageCollector::OnTestCaseEnd {e.Context.SessionId} {e.TestCaseName}");
        }
    }
}