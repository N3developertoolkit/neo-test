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

    class PathFinder
    {
        readonly IReadOnlyDictionary<int, Instruction> instructions;
        readonly int maxAddress;

        public PathFinder(IEnumerable<(int address, Instruction instruction)> instructions)
            : this(instructions.ToDictionary(t => t.address, t => t.instruction))
        {
        }

        public PathFinder(IReadOnlyDictionary<int, Instruction> instructions)
        {
            this.instructions = instructions;
            maxAddress = this.instructions.Keys.Max();
        }

        // public IEnumerable<ImmutableStack<int>> FindPaths(NeoDebugInfo.Method method)
        // {
        //     return FindPaths(ImmutableStack.Create(method.Range.Start));
        // }

        public IEnumerable<ImmutableStack<int>> FindPaths(int address, ImmutableStack<int>? path = null, int methodEnd = int.MaxValue, int nextSPAddress = int.MaxValue)
        {
            path ??= ImmutableStack<int>.Empty;
            while (true)
            {
                var ins = address > maxAddress ? new Instruction(OpCode.RET) : instructions[address];
                if (ins.IsBranchInstruction())
                {
                    path = path.Push(address);
                    var offset = ins.GetBranchOffset();
                    var branchAddress = address + offset;
                    var foo = FindPaths(branchAddress, path.Push(branchAddress), methodEnd, nextSPAddress);
                    foreach (var q in foo)
                    {
                        yield return q;
                    }
                }
                // else if (ins.IsCallInstruction())
                // {
                //     // var offset = ins.GetCallOffset();
                //     // var callPaths = FindPaths(ImmutableStack.Create(address + offset));
                //     // foreach (var p in callPaths)
                //     // {
                //     //     var z = path;
                //     //     foreach (var q in p.Reverse())
                //     //     {
                //     //         z = z.Push(q);
                //     //     }
                //     // }

                // }
                // else if (ins.OpCode == OpCode.RET)
                // {
                //     yield return path;
                //     yield break;
                // }

                address += ins.Size;
                if (address > methodEnd || address >= nextSPAddress)
                {
                    yield return path;
                    yield break;
                }
            }
        }
    }

    [Fact]
    public void TestName()
    {
        var nef = TestFiles.GetResource("0xc8855ad814f63da8551a2e7f021ac58897bdf532.nef", NefFile.Load);
        var debugInfo = TestFiles.GetResource("registrar.debug.json", NeoDebugInfo.Load);
        Assert.Equal(debugInfo.Hash, Hash160.Parse("0xc8855ad814f63da8551a2e7f021ac58897bdf532"));

        var pathFinder = new PathFinder(nef.Script.EnumerateInstructions());
        var paths = pathFinder.FindPaths(4, null, 50, 50).ToArray();
;
    
        // When
    
        // Then
    }

}
