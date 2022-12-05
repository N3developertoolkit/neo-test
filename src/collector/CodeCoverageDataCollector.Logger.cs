using System;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.DataCollection;

namespace Neo.Collector
{
    public partial class CodeCoverageDataCollector
    {
        class Logger : ILogger
        {
            readonly DataCollectionLogger logger;
            readonly DataCollectionContext collectionContext;

            public Logger(DataCollectionLogger logger, DataCollectionEnvironmentContext environmentContext)
            {
                this.logger = logger;
                collectionContext = environmentContext.SessionDataCollectionContext;
            }

            public void LogError(string text, Exception exception = null)
            {
                if (exception is null)
                    logger.LogError(collectionContext, text);
                else
                    logger.LogError(collectionContext, text, exception);
            }

            public void LogWarning(string text)
            {
                logger.LogWarning(collectionContext, text);
            }
        }
    }
}