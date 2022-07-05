using System;
using System.IO;
using Microsoft.Build.Utilities.ProjectCreation;
using Neo.BuildTasks;
using Xunit;

namespace build_tasks
{
    public class TestBuild : MSBuildTestBase
    {
        static void InstallNccs(string path, string version = "3.3.0")
        {
            var runner = new ProcessRunner();
            runner.RunThrow("dotnet", "new tool-manifest", path);
            runner.RunThrow("dotnet", $"tool install neo.compiler.csharp --version {version}", path);
        }

        static void WriteSource(string path, string source, string filename = "contract.cs")
        {
            var fullPath = Path.Combine(path, filename);
            File.WriteAllText(fullPath, source);
        }

        static ProjectCreator CreateContractProject(string rootPath, string source)
        {
            WriteSource(rootPath, source);
            return ProjectCreator.Templates.SdkCsproj(
                path: Path.Combine(rootPath, "test.csproj"),
                targetFramework: "net6.0")
                .Property("NeoContractName", "$(AssemblyName)")
                .ImportNeoBuildTools()
                .ItemPackageReference("Neo.SmartContract.Framework", version: "3.3.0");
        }

        static void TestBuildContract(string source)
        {
            var testRootPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            try
            {
                Directory.CreateDirectory(testRootPath);
                InstallNccs(testRootPath);

                WriteSource(testRootPath, source);

                var creator = CreateContractProject(testRootPath, source);
                creator.TryBuild(restore: true, out bool result, out BuildOutput buildOutput);

                Assert.True(result, string.Join('\n', buildOutput.Errors));
            }
            finally
            {
                if (Directory.Exists(testRootPath)) Directory.Delete(testRootPath, true);
            }
        }

        [Fact]
        public void can_build_contract_that_calls_assert_with_message()
        {
            const string source = @"
using Neo.SmartContract.Framework;

namespace BuildToolsTestClasses
{
    public class TestContract : SmartContract
    {
        public static void TestAssert() { ExecutionEngine.Assert(false, ""message""); }
    }
}";
            TestBuildContract(source);
        }

        [Fact]
        public void can_build_TokenContract()
        {
            const string source = @"
using Neo.SmartContract.Framework;

    public class TestContract : TokenContract
    {
        public override byte Decimals() => 0;
        public override string Symbol() => ""TEST"";
    }
";
            TestBuildContract(source);
        }

    }
}
