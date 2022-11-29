using System.Collections.Generic;
using Neo.Collector.Models;

namespace Neo.Collector
{
    class OldContractCoverage
    {
        public readonly int TestRunCount;
        public readonly string ContractName;
        public readonly Hash160 ScriptHash;
        public readonly IReadOnlyList<MethodCoverage> Methods;

        public OldContractCoverage(int testRunCount, string contractName, Hash160 scriptHash, IReadOnlyList<MethodCoverage> methods)
        {
            TestRunCount = testRunCount;
            ContractName = contractName;
            ScriptHash = scriptHash;
            Methods = methods;
        }
    }
}