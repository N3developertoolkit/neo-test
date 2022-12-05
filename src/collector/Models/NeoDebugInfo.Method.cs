using System;
using System.Collections.Generic;

namespace Neo.Collector.Models
{
    public partial struct NeoDebugInfo
    {
        public struct Method
        {
            public readonly string Id;
            public readonly string Namespace;
            public readonly string Name;
            public readonly (int Start, int End) Range;
            public readonly IReadOnlyList<Parameter> Parameters;
            public readonly IReadOnlyList<SequencePoint> SequencePoints;

            public Method(string id, string @namespace, string name, (int, int) range, IReadOnlyList<Parameter> parameters, IReadOnlyList<SequencePoint> sequencePoints)
            {
                Id = id;
                Namespace = @namespace;
                Name = name;
                Range = range;
                Parameters = parameters;
                SequencePoints = sequencePoints;
            }
        }
    }
}
