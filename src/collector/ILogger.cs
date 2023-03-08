using System;

namespace Neo.Collector
{
    public interface ILogger
    {
        void LogError(string text, Exception? exception = null);
        void LogWarning(string text);
    }
}