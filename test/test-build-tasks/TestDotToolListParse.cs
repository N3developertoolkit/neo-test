using System;
using System.Collections.Generic;
using System.Linq;
using Neo.BuildTasks;
using SemVersion;
using Xunit;

namespace build_tasks
{
    public class TestDotToolListParse
    {
        static IReadOnlyList<string> GlobalOutput => GLOBAL_OUTPUT.Split('\n').Select(s => s.Trim()).ToArray();
        static IReadOnlyList<string> LocalOutput => LOCAL_OUTPUT.Split('\n').Select(s => s.Trim()).ToArray();

        [Fact]
        public void test_RunTool_local_success()
        {
            var runner = new MockProcessRunner();
            runner.Add("dotnet", "tool list --local", "workDir", LocalOutput);
            runner.Add("dotnet", "neoxp args", "workDir", new[] { "output1", "output2" });
            var dnt = new DotNetToolRunner(runner.ProcessRunner);

            var actual = dnt.Run("neo.express", "neoxp", "args", "workDir");

            Assert.Collection(actual,
                t => Assert.Equal("output1", t),
                t => Assert.Equal("output2", t));
            runner.AssertEmpty();
        }

        [Fact]
        public void test_RunTool_global_success()
        {
            var runner = new MockProcessRunner();
            runner.Add("dotnet", "tool list --local", "workDir", LocalOutput);
            runner.Add("dotnet", "tool list --global", "", GlobalOutput);
            runner.Add("dumpnef", "args", "", new[] { "output1", "output2" });
            var dnt = new DotNetToolRunner(runner.ProcessRunner);

            var actual = dnt.Run("devhawk.dumpnef", "dumpnef", "args", "workDir");

            Assert.Collection(actual,
                t => Assert.Equal("output1", t),
                t => Assert.Equal("output2", t));
            runner.AssertEmpty();
        }

        [Fact]
        public void test_RunTool_tool_doesnt_exist()
        {
            var runner = new MockProcessRunner();
            runner.Add("dotnet", "tool list --local", "workDir", LocalOutput);
            runner.Add("dotnet", "tool list --global", "", GlobalOutput);
            var dnt = new DotNetToolRunner(runner.ProcessRunner);

            Assert.ThrowsAny<Exception>(() => dnt.Run("fake-package", "dumpnef", "args", "workDir"));

            runner.AssertEmpty();
        }

        [Fact]
        public void test_RunTool_tool_call_fail()
        {
            var runner = new MockProcessRunner();
            runner.Add("dotnet", "tool list --local", "workDir", LocalOutput);
            runner.Add("dotnet", "tool list --global", "", GlobalOutput);
            runner.Add("dumpnef", "args", "", -1);
            var dnt = new DotNetToolRunner(runner.ProcessRunner);

            Assert.ThrowsAny<Exception>(() => dnt.Run("devhawk.dumpnef", "dumpnef", "args", "workDir"));

            runner.AssertEmpty();
        }

        [Fact]
        public void test_TryGetTool_success()
        {
            var dnt = new DotNetToolRunner((c, a, d) => new ProcessRunner.Results(0, GlobalOutput, Array.Empty<string>()));
            Assert.True(dnt.TryGetTool("cmd", "args", "workingDir", "dotnet-outdated-tool", out _));
        }

        [Fact]
        public void test_TryGetTool_fail()
        {
            var dnt = new DotNetToolRunner((c, a, d) => new ProcessRunner.Results(1, GlobalOutput, Array.Empty<string>()));
            Assert.Throws<Exception>(() => dnt.TryGetTool("cmd", "args", "workingDir", "dotnet-outdated-tool", out _));
        }

        [Fact]
        public void test_TryGetTool_local_success_case_insensitive()
        {
            Assert.True(DotNetToolRunner.TryGetTool(LocalOutput, "NEO.TRACE", out var tool));
            Assert.Equal("neo.trace", tool.Name);
            Assert.Equal(new SemanticVersion(3, 1, 38), tool.Version);
        }

        [Fact]
        public void test_TryGetTool_global_success()
        {
            Assert.True(DotNetToolRunner.TryGetTool(GlobalOutput, "dotnet-outdated-tool", out var tool));
            Assert.Equal("dotnet-outdated-tool", tool.Name);
            Assert.Equal(new SemanticVersion(4, 1, 0), tool.Version);
        }

        [Fact]
        public void test_TryGetTool_global_fail()
        {
            Assert.False(DotNetToolRunner.TryGetTool(GlobalOutput, "this-tool-doesnt-exist", out var tool));
        }

        [Fact]
        public void test_TryGetTool_local_success()
        {
            Assert.True(DotNetToolRunner.TryGetTool(LocalOutput, "neo.trace", out var tool));
            Assert.Equal("neo.trace", tool.Name);
            Assert.Equal(new SemanticVersion(3, 1, 38), tool.Version);
        }

        [Fact]
        public void test_TryGetTool_local_fail()
        {
            Assert.False(DotNetToolRunner.TryGetTool(LocalOutput, "this-tool-doesnt-exist", out var tool));
        }

        [Fact]
        public void test_TryGetTool_local()
        {
            var table = DotNetToolRunner.ParseTable(LocalOutput);
            Assert.Equal(5, table.Count());
            Assert.True(table.All(r => r.Count == 4));
        }

        [Fact]
        public void test_ParseTable_global()
        {
            var table = DotNetToolRunner.ParseTable(GlobalOutput);
            Assert.Equal(5, table.Count());
            Assert.True(table.All(r => r.Count == 3));
        }

        [Fact]
        public void test_ParseTable_local()
        {
            var table = DotNetToolRunner.ParseTable(LocalOutput);
            Assert.Equal(5, table.Count());
            Assert.True(table.All(r => r.Count == 4));
        }

        [Fact]
        public void test_ParseToolPackageTable_global()
        {
            var table = DotNetToolRunner.ParseToolPackageTable(GlobalOutput).ToArray();
            Assert.Equal(4, table.Length);
        }

        [Fact]
        public void test_ParseToolPackageTable_local()
        {
            var table = DotNetToolRunner.ParseToolPackageTable(LocalOutput).ToArray();
            Assert.Equal(4, table.Length);
        }

        const string GLOBAL_OUTPUT = @"        Package Id                Version         Commands       
---------------------------------------------------------
devhawk.dumpnef           1.0.19          dumpnef
dotnet-format             5.1.250801      dotnet-format
dotnet-outdated-tool      4.1.0           dotnet-outdated
nbgv                      3.4.255         nbgv";

        const string LOCAL_OUTPUT = @"Package Id               Version      Commands             Manifest
-----------------------------------------------------------------------------------------------------------------------------------------------
neo.express              3.1.38       neoxp                C:\Users\harry\Source\neo\seattle\samples\registrar-sample\.config\dotnet-tools.json
neo.compiler.csharp      3.1.0        nccs                 C:\Users\harry\Source\neo\seattle\samples\registrar-sample\.config\dotnet-tools.json
neo.trace                3.1.38       neotrace             C:\Users\harry\Source\neo\seattle\samples\registrar-sample\.config\dotnet-tools.json
neo.test.runner          3.1.10       neo-test-runner      C:\Users\harry\Source\neo\seattle\samples\registrar-sample\.config\dotnet-tools.json";
    }

    class MockProcessRunner
    {
        Queue<(string cmd, string args, string workDir, ProcessRunner.Results results)> calls = new Queue<(string cmd, string args, string workDir, ProcessRunner.Results results)>();

        public void Add(string cmd, string args, string workDir, IReadOnlyList<string> output)
        {
            calls.Enqueue((cmd, args, workDir, new ProcessRunner.Results(0, output, Array.Empty<string>())));
        }

        public void Add(string cmd, string args, string workDir, int exitCode)
        {
            Assert.NotEqual(0, exitCode);
            calls.Enqueue((cmd, args, workDir, new ProcessRunner.Results(exitCode, Array.Empty<string>(), Array.Empty<string>())));
        }

        public void AssertEmpty() => Assert.True(calls.Count == 0);

        public DotNetToolRunner.ProcessRunnerFunc ProcessRunner
        {
            get => (cmd, args, workDir) =>
            {
                var call = calls.Dequeue();
                Assert.Equal(call.cmd, cmd);
                Assert.Equal(call.args, args);
                Assert.Equal(call.workDir, workDir);
                return call.results;
            };
        }
    }

}
