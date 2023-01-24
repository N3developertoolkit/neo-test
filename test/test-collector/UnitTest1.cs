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
    public void Test1()
    {
        const string DEBUG_INFO_PATH = @"C:\Users\harry\Source\neo\seattle\samples\registrar\src\bin\sc\registrar.nefdbgnfo";
        const string COVERAGE_PATH = @"C:\Users\harry\AppData\Local\Temp\lc5xob3e.rza";
        
        var logger = new Moq.Mock<ILogger>();
        var collector = new CodeCoverageCollector(logger.Object);
        var debugInfo = NeoDebugInfo.TryLoad(DEBUG_INFO_PATH, out var value) ? value : throw new Exception();
        collector.TrackContract("contract", debugInfo);
        collector.LoadCoverageFiles(COVERAGE_PATH);
        var coverage = collector.CollectCoverage().First();
     
        var coverageWriter = new CoberturaFormat.ContractCoverageWriter(coverage);
        var method = coverage.DebugInfo.Methods.First(m => m.Name == "Register");

        using var stream = new MemoryStream();
        using var xmlWriter = new XmlTextWriter(stream, null);
        coverageWriter.WritePackage(xmlWriter);
        xmlWriter.Flush();
        stream.Flush();
        
        var str = Encoding.UTF8.GetString(stream.ToArray());
        ;
    }

    [Fact]
    public void TestName2()
    {
        const string PATH = @"C:\Users\harry\Source\neo\seattle\samples\registrar\test\bin\Debug\net6.0\test-registrar-contract.dll";
        var logger = new Moq.Mock<ILogger>();
        var collector = new CodeCoverageCollector(logger.Object);
        collector.LoadTestSource(PATH);
    }

    [Fact]
    public void TestName3()
    {

        var doc= new XmlDocument();
        var e = doc.CreateElement("DebugInfo");
        e.InnerText = @"C:\Users\harry\Source\neo\seattle\samples\registrar\src\bin\sc\registrar.nefdbgnfo";
        var logger = new Moq.Mock<ILogger>();
        var collector = new CodeCoverageCollector(logger.Object);
        collector.LoadDebugInfoSetting(e);
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
