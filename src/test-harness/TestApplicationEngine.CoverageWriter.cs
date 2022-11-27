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
            bool disposed = false;

            public TextWriter Writer { get; }

            public CoverageWriter(string coveragePath)
            {
                this.coveragePath = coveragePath;
                if (!Directory.Exists(coveragePath)) Directory.CreateDirectory(coveragePath);
                var filename = Path.Combine(coveragePath, $"{Guid.NewGuid()}.neo-coverage");

                stream = File.Create(filename);
                Writer = new StreamWriter(stream);
            }

            public void WriteContext(ExecutionContext? context)
            {
                var hash = context?.Script.CalculateScriptHash() ?? UInt160.Zero; 
                Writer.WriteLine($"{hash}");

                if (context is null) return;
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

            public void WriteAddress(int ip) => Writer.WriteLine($"{ip}");

            public void WriteBranch(int ip, int offset, int? result) => Writer.WriteLine($"{ip} {offset} {result ?? 0}");

            public void Dispose()
            {
                if (!disposed)
                {
                    Writer.Flush();
                    stream.Flush();
                    Writer.Dispose();
                    stream.Dispose();
                    disposed = true;
                }
            }
        }
    }
}

