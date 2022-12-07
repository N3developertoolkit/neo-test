using System;
using System.Collections.Generic;
using static Neo.Collector.Models.NeoDebugInfo;

namespace Neo.Collector.Models
{
    partial class ContractCoverage
    {
        public struct MethodCoverage
        {
            readonly Method method;
            public readonly string Document;
            public readonly IReadOnlyList<LineCoverage> Lines;

            public string Namespace => method.Namespace;
            public string Name => method.Name;
            public IReadOnlyList<Parameter> Parameters => method.Parameters;

            public MethodCoverage(in Method method, string document, IReadOnlyList<LineCoverage> lines)
            {
                this.method = method;
                Document = document;
                Lines = lines;
            }
        }
    }
}
