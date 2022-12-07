using System;

namespace NeoTestHarness
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class PrefetchContractAttribute : Attribute
    {
        public string Name { get; init; } = string.Empty;

        public PrefetchContractAttribute(string name)
        {
            Name = name;
        }
    }
}

