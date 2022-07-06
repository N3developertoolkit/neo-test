using System;
using System.IO;

namespace build_tasks
{
    public partial class TestBuild
    {
        internal class TestRootPath : IDisposable
        {
            readonly string Value;

            public TestRootPath(string root = "")
            {
                root = string.IsNullOrEmpty(root)
                    ? Path.GetTempPath()
                    : root;
                Value = Path.Combine(root, Path.GetRandomFileName());
                Directory.CreateDirectory(Value);
            }

            public void Dispose()
            {
                // if (Directory.Exists(Value)) Directory.Delete(Value, true);
            }

            public static implicit operator string(TestRootPath p) => p.Value;
        }

    }
}
