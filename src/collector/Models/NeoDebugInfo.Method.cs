using System;
using System.Collections.Generic;

namespace Neo.Collector.Models
{
    public partial class NeoDebugInfo
    {
        public class Parameter
        {
            public string Name { get; set; } = "";
            public string Type { get; set; } = "";
            public int Index { get; set; }
        }

        public class Method
        {
            public string Id { get; set; } = "";
            public string Namespace { get; set; } = "";
            public string Name { get; set; } = "";
            public (int Start, int End) Range { get; set; }
            public IReadOnlyList<Parameter> Parameters { get; set; } = Array.Empty<Parameter>();
            public IReadOnlyList<SequencePoint> SequencePoints { get; set; } = Array.Empty<SequencePoint>();
        }
    }
}
