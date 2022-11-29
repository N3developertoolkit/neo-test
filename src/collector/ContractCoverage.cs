using System.Collections.Generic;
using Neo.Collector.Models;

namespace Neo.Collector
{
    class ContractCoverage
    {
        public readonly int TestRunCount;
        public readonly string ContractName;
        public readonly Hash160 ScriptHash;
        public readonly IReadOnlyList<MethodCoverage> Methods;

        public ContractCoverage(int testRunCount, string contractName, Hash160 scriptHash, IReadOnlyList<MethodCoverage> methods)
        {
            TestRunCount = testRunCount;
            ContractName = contractName;
            ScriptHash = scriptHash;
            Methods = methods;
        }
    }
}