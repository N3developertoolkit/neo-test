using System;
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
        var logger = new Moq.Mock<ILogger>();
        var collector = new CodeCoverageCollector(logger.Object);
        collector.LoadTestContract("test contract", "registrar.debug.json");
        collector.LoadTestOutput(".run1.");
        var coverage = collector.CollectCoverage();
        foreach (var contract in coverage)
        {
            var bar = contract.CalcLineCoverage().AsPercentage();
            //             foreach (var method in contract.Methods)
            //             {
            //                 var name = method.Name;
            //                 var foo = method.CalcLineCoverage().AsPercentage();
            // ;
        }


    }

}
