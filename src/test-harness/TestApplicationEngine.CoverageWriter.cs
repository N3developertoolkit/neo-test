using System;
using System.IO;
using Neo;
using Neo.BlockchainToolkit;
using Neo.SmartContract;
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

            public void WriteContext(ExecutionContext? context)
            {
                if (disposed) throw new ObjectDisposedException(nameof(CoverageWriter));

                if (context is null)
                {
                    writer.WriteLine($"{UInt160.Zero}");
                }
                else 
                {
                    var state = context.GetState<ExecutionContextState>();
                    var hash = context.Script.CalculateScriptHash(); 
                    writer.WriteLine($"{hash}");

                    if (state.Contract?.Nef is null)
                    {
                        var scriptPath = Path.Combine(coveragePath, $"{hash}.neo-script");
                        WriteScriptFile(scriptPath, stream => stream.Write(context.Script.AsSpan()));
                    }
                    else
                    {
                        var scriptPath = Path.Combine(coveragePath, $"{hash}.nef");
                        WriteScriptFile(scriptPath, stream => {
                            var writer = new BinaryWriter(stream);
                            state.Contract.Nef.Serialize(writer);
                            writer.Flush();
                        });
                    }
                }
            }

            static void WriteScriptFile(string filename, Action<Stream> writeFileAction)
            {
                if (!File.Exists(filename))
                {
                    try
                    {
                        using var stream = File.Open(filename, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                        writeFileAction(stream);
                        stream.Flush();
                    }
                    // ignore IOException thrown because file already exists
                    catch (IOException) { }
                }
            }

            // WriteAddress and WriteBranch do not need disposed check since writer will be disposed
            public void WriteAddress(int ip) => writer.WriteLine($"{ip}");

            public void WriteBranch(int ip, int offset, int result) => writer.WriteLine($"{ip} {offset} {result}");
        }
    }
}

