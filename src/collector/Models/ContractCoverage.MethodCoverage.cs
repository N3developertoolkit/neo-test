using System.Collections.Generic;

namespace Neo.Collector.Models
{
    partial class ContractCoverage
    {
        public class MethodCoverage
        {
            readonly NeoDebugInfo.Method method;
            public readonly string Document;
            public readonly IReadOnlyList<LineCoverage> Lines;

            public string Namespace => method.Namespace;
            public string Name => method.Name;

            public MethodCoverage(in NeoDebugInfo.Method method, string document, IReadOnlyList<LineCoverage> lines)
            {
                this.method = method;
                Document = document;
                Lines = lines;
            }
        }
    }
}
