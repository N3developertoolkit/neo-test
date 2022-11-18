using System;

namespace NeoTestHarness
{
    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = false)]
    public class ContractAttribute : Attribute
    {
        public string Name { get; private set; } = string.Empty;
        public string ManifestPath { get; private set; } = string.Empty;

        public ContractAttribute(string name, string manifestPath)
        {
            Name = name;
            ManifestPath = manifestPath;
        }
    }
}
