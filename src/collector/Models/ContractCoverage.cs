using System.Collections.Generic;

namespace Neo.Collector.Models
{
    partial class ContractCoverage
    {
        public readonly string Name;
        public readonly Hash160 ScriptHash;
        public readonly IReadOnlyList<MethodCoverage> Methods;

        public ContractCoverage(string name, in Hash160 scriptHash, IReadOnlyList<MethodCoverage> methods)
        {
            Name = name;
            ScriptHash = scriptHash;
            Methods = methods;
        }
    }
}
