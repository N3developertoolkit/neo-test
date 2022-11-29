using System.Collections.Generic;
using Neo.Collector.Models;

namespace Neo.Collector
{
    using Method = NeoDebugInfo.Method;

    class MethodCoverage
    {
        readonly Method method;
        public readonly string Document;
        public readonly IReadOnlyList<LineCoverage> Lines;

        public string Namespace => method.Namespace;
        public string Name => method.Name;

        public MethodCoverage(Method method, string document, IReadOnlyList<LineCoverage> lines)
        {
            this.method = method;
            Document = document;
            Lines = lines;
        }
    }
}