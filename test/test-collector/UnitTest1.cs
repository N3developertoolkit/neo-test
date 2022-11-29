using System.Collections.Generic;
using System.IO;
using System.Linq;
using Neo.Collector;
using Neo.Collector.Models;
using Xunit;

namespace test_collector;

public class UnitTest1
{
    [Fact]
    public void Test1()
    {
        var collector = new CodeCoverageCollector();
        collector.LoadTestContract("test contract", "registrar.debug.json");

        foreach (var file in TestFiles.GetResourceNames(".run1."))
        {
            using var stream = TestFiles.GetResourceStream(file);
            collector.LoadSessionOutput(file, stream);
        }
    }

}

static class TestExtensions
{

}