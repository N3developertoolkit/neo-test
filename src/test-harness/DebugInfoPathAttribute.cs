using System;

namespace NeoTestHarness
{
    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = false)]
    public class DebugInfoPathAttribute : Attribute
    {
        public string Path { get; private set; } = string.Empty;

        public DebugInfoPathAttribute(string path)
        {
            Path = path;
        }
    }
}
