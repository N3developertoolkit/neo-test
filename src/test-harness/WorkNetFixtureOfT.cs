using System;
using System.Linq;

namespace NeoTestHarness
{
    public class WorkNetFixture<T> : WorkNetFixture
    {
        static string[] GetPrefetchContracts()
        {
            var attribs = Attribute.GetCustomAttributes(typeof(T), typeof(PrefetchContractAttribute)) as PrefetchContractAttribute[];

            if (attribs is null || attribs.Length == 0)
            {
                throw new Exception("No prefetch contracts defined, need at least one");
            }

            return attribs?.Select( x => x.Name).ToArray();
        }

        static WorkNetConfigAttribute GetWorkNetConfig()
        {
            var attrib = Attribute.GetCustomAttribute(typeof(T), typeof(WorkNetConfigAttribute)) as WorkNetConfigAttribute;

            return attrib ?? throw new Exception($"Missing {nameof(WorkNetConfigAttribute)} on {typeof(T).Name}");
        }

        public WorkNetFixture() : base(GetPrefetchContracts(), GetWorkNetConfig())
        {
        }
    }
}

