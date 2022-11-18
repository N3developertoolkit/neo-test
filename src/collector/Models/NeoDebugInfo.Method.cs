using System;
using System.Collections.Generic;

namespace Neo.Collector.Models
{
    public partial class NeoDebugInfo
    {
        public class Method
        {
            public string Id { get; set; } = "";
            public string Namespace { get; set; } = "";
            public string Name { get; set; } = "";
            public (int Start, int End) Range { get; set; }
            public IReadOnlyList<SequencePoint> SequencePoints { get; set; } = Array.Empty<SequencePoint>();
        }
    }
}
