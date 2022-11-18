namespace Neo.Collector.Models
{
    public partial class NeoDebugInfo
    {
        public class SequencePoint
        {
            public int Address { get; set; }
            public int Document { get; set; }
            public (int Line, int Column) Start { get; set; }
            public (int Line, int Column) End { get; set; }
        }
    }
}
