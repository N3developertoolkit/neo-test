using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Neo.Collector;
using Neo.Collector.Formats;
using Neo.Collector.Models;
using Xunit;

namespace test_collector;

public class UnitTest1
{
    [Fact]
    public void test_generate_coverage()
    {
        var logger = new Moq.Mock<ILogger>();
        var collector = new CodeCoverageCollector(logger.Object);
        collector.TrackTestContract("contract", "registrar.nefdbgnfo");
        collector.LoadTestOutput("run1");
        var coverage = collector.CollectCoverage().ToList();
     
        var format = new CoberturaFormat();
        Dictionary<string, string> outputMap = new();

        format.WriteReport(coverage, writeAttachment);

        void writeAttachment(string filename, Action<Stream> writeAttachment)
        {
            using var stream = new MemoryStream();
            var text = Encoding.UTF8.GetString(stream.ToArray());
            outputMap.Add(filename, text);
        }
    }
}
