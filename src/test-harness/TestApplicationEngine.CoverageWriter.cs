using System;
using System.IO;

namespace NeoTestHarness
{
    public partial class TestApplicationEngine
    {
        class CoverageWriter : IDisposable
        {
            readonly Stream stream;
            bool disposed = false;

            public TextWriter Writer { get; }

            public CoverageWriter(string coveragePath)
            {
                if (!Directory.Exists(coveragePath)) Directory.CreateDirectory(coveragePath);
                var filename = Path.Combine(coveragePath, $"{Guid.NewGuid()}.neo.coverage");

                stream = File.Create(filename);
                Writer = new StreamWriter(stream);
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

