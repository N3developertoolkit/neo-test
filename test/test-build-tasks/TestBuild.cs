using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Utilities.ProjectCreation;
using Neo.BuildTasks;
using Xunit;

namespace build_tasks
{
    public partial class TestBuild : MSBuildTestBase
    {
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

        [Fact]
        public void can_generate_contract_from_NeoContractGeneration()
        {
            using var testRootPath = new TestRootPath();
            var manifestPath = Path.Combine(testRootPath, "contract.manifest.json");
            TestFiles.CopyTo("registrar.manifest", manifestPath);

            var creator = ProjectCreator.Templates.SdkCsproj(
                path: Path.Combine(testRootPath, "tests.csproj"),
                targetFramework: "net6.0")
                .ImportNeoBuildTools()
                .ReferenceNeo()
                .ItemInclude("NeoContractGeneration", "registrar", 
                    metadata: new Dictionary<string, string?>() { { "ManifestPath", manifestPath } })
                .AssertBuild();
            Assert.True(File.Exists(Path.Combine(testRootPath, @"obj\Debug\net6.0\registrar.contract-interface.cs")),
                "contract interface not generated");
        }

        [Fact]
        public void can_generate_contract_from_NeoContractReference()
        {
            using var testRootPath = new TestRootPath();
            InstallNccs(testRootPath);

            var srcDir = Path.Combine(testRootPath, "src");
            TestFiles.CopyTo("registrar.source", Path.Combine(srcDir, "contract.cs"));
            var srcCreator = testRootPath.CreateNeoProject("src/registrar.csproj")
                .Property("NeoContractName", "$(AssemblyName)")
                .ImportNeoBuildTools()
                .ReferenceNeoScFx()
                .Save();

            var testDir = Path.Combine(testRootPath, "test");
            var testCreator = testRootPath.CreateNeoProject("test/registrarTests.csproj")
                .ImportNeoBuildTools()
                .ReferenceNeo()
                .ItemInclude("NeoContractReference", srcCreator.FullPath)
                .AssertBuild();

            Assert.True(File.Exists(Path.Combine(testDir, @"obj\Debug\net6.0\registrar.contract-interface.cs")),
                "contract interface not generated");
        }

        static void TestBuildContract(string source, string sourceName = "contract.cs")
        {
            using var testRootPath = new TestRootPath();
            InstallNccs(testRootPath);

            var sourcePath = Path.Combine(testRootPath, sourceName);
            File.WriteAllText(sourcePath, source);

            var creator = testRootPath.CreateNeoProject()
                .Property("NeoContractName", "$(AssemblyName)")
                .ImportNeoBuildTools()
                .ItemPackageReference("Neo.SmartContract.Framework", version: "3.3.0")
                .AssertBuild();

        }

        static void InstallNccs(string path, string version = "3.3.0")
        {
            var runner = new ProcessRunner();
            runner.RunThrow("dotnet", "new tool-manifest", path);
            runner.RunThrow("dotnet", $"tool install neo.compiler.csharp --version {version}", path);
        }
    }
}
