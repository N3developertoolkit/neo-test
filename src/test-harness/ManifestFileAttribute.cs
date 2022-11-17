using System;

namespace NeoTestHarness
{
    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = false)]
    public class ManifestFileAttribute : Attribute
    {
        public string FileName { get; private set; } = string.Empty;

        public ManifestFileAttribute(string fileName)
        {
            FileName = fileName;
        }
    }
}
