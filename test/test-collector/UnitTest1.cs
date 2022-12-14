using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
        // var logger = new Moq.Mock<ILogger>();
        // var collector = new CodeCoverageCollector(logger.Object);
        // collector.LoadTestContract("test contract", "registrar.debug.json");
        // collector.LoadTestOutput(".run1.");
        // var coverage = collector.CollectCoverage();
        // foreach (var contract in coverage)
        // {
        //     var bar = contract.CalcLineCoverage().AsPercentage();
        //     //             foreach (var method in contract.Methods)
        //     //             {
        //     //                 var name = method.Name;
        //     //                 var foo = method.CalcLineCoverage().AsPercentage();
        //     // ;
        // }


    }

    

    [Fact]
    public void TestName()
    {
        var debugInfo = TestFiles.GetResource("registrar.debug.json", NeoDebugInfo.Load);
        var coverage = new ContractCoverageCollector("test", debugInfo);
        var nef = TestFiles.GetResource("0xc8855ad814f63da8551a2e7f021ac58897bdf532.nef", NefFile.Load);
        coverage.RecordScript(nef.Script.EnumerateInstructions());

        var paths = coverage.FindPaths(4, null, 50, 50).ToArray();
;           
    }

}
