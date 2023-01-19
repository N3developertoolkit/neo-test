using System;
using System.Collections.Generic;
using System.IO;
using Neo.Collector.Models;

namespace Neo.Collector.Formats
{
    interface ICoverageFormat
    {
        void WriteReport(IEnumerable<ContractCoverage> coverage, Action<string, Action<Stream>> writeAttachement);
    }
}