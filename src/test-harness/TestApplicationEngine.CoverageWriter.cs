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

            public void WriteScript(ExecutionContext? context)
            {
                var hash = context?.Script.CalculateScriptHash() ?? UInt160.Zero; 
                WriteLine($"{hash}");

                if (context is null) return;
                var scriptPath = Path.Combine(coveragePath, $"{hash}.neo-script");
                if (!File.Exists(scriptPath))
                {
                    try
                    {
                        using var scriptStream = File.Open(scriptPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                        scriptStream.Write(context.Script.AsSpan());
                    }
                    catch (System.IO.IOException)
                    {
                        // ignore IOException thrown because file already exists
                    }
                }
            }

            public void WriteLine(string? value) => Writer.WriteLine(value);
            public void Write(string? value) => Writer.Write(value);

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

