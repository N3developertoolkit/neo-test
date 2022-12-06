using System.Collections.Generic;

namespace Neo.Collector.Models
{
    partial class ContractCoverage
    {
        public readonly string Name;
        public readonly IReadOnlyList<MethodCoverage> Methods;

        public ContractCoverage(string name, IReadOnlyList<MethodCoverage> methods)
        {
            Name = name;
            Methods = methods;
        }
    }
}
