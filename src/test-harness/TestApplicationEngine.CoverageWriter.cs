using System;
using System.IO;
using Neo;
using Neo.BlockchainToolkit;
using Neo.VM;

namespace NeoTestHarness
{
    public partial class TestApplicationEngine
    {
        class CoverageWriter : IDisposable
        {
            readonly string coveragePath;
            readonly Stream stream;
            readonly TextWriter writer;
            bool disposed = false;

            public CoverageWriter(string coveragePath)
            {
                this.coveragePath = coveragePath;
                if (!Directory.Exists(coveragePath)) Directory.CreateDirectory(coveragePath);
                var filename = Path.Combine(coveragePath, $"{Guid.NewGuid()}.neo-coverage");

                stream = File.Create(filename);
                writer = new StreamWriter(stream);
            }

            public void WriteContext(ExecutionContext? context)
            {
                if (context is null)
                {
                    writer.WriteLine($"{UInt160.Zero}");
                }
                else 
                {
                    var hash = context.Script.CalculateScriptHash(); 
                    writer.WriteLine($"{hash}");

                    var scriptPath = Path.Combine(coveragePath, $"{hash}.neo-script");
                    if (!File.Exists(scriptPath))
                    {
                        try
                        {
                            using var scriptStream = File.Open(scriptPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                            scriptStream.Write(context.Script.AsSpan());
                            scriptStream.Flush();
                        }
                        catch (IOException)
                        {
                            // ignore IOException thrown because file already exists
                        }
                    }
                }
            }

            public void WriteAddress(int ip) => writer.WriteLine($"{ip}");

            public void WriteBranch(int ip, int offset, int result) => writer.WriteLine($"{ip} {offset} {result}");

            public void Dispose()
            {
                if (!disposed)
                {
                    writer.Flush();
                    stream.Flush();
                    writer.Dispose();
                    stream.Dispose();
                    disposed = true;
                }
            }
        }
    }
}

