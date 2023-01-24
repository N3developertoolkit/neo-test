using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Xml;
using Neo.Collector;
using Neo.Collector.Formats;
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
        var coverage = collector.CollectCoverage().First();
     
        var coverageWriter = new CoberturaFormat.ContractCoverageWriter(coverage);
        var method = coverage.DebugInfo.Methods[0];
        var rate = coverageWriter.CalculateBranchRate(method, 0);

        using var stream = new System.IO.MemoryStream();
        using var xmlWriter = new System.Xml.XmlTextWriter(stream, null);
        coverageWriter.WriteLine(xmlWriter, method, 0);
        xmlWriter.Flush();
        stream.Flush();

        var str = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        ;
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
    public void TestName2()
    {
        // Given
    
        // When
    
        // Then
    }

    

    [Fact]
    public void TestName()
    {
        var debugInfo = TestFiles.GetResource("registrar.debug.json", NeoDebugInfo.Load);
        var nef = TestFiles.GetResource("0xc8855ad814f63da8551a2e7f021ac58897bdf532.nef", NefFile.Load);
        var instructionMap = nef.Script.EnumerateInstructions().ToImmutableDictionary(i => i.address, i => i.instruction);

        foreach (var method in debugInfo.Methods)
        {
            for (int i = 0; i < method.SequencePoints.Count; i++)
            {
                NeoDebugInfo.SequencePoint sp = method.SequencePoints[i];
                var last = method.GetLineLastAddress(i, instructionMap);
                var ins = instructionMap.GetBranchPaths(sp.Address, last).ToArray();
                ;
            }
        }
    }
}
