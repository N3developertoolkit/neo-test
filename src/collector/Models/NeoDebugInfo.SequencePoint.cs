namespace Neo.Collector.Models
{
    public partial class NeoDebugInfo
    {
        public struct SequencePoint
        {
            public readonly int Address;
            public readonly int Document;
            public readonly (int Line, int Column) Start;
            public readonly (int Line, int Column) End;

            public SequencePoint(int address, int document, (int, int) start, (int, int) end)
            {
                Address = address;
                Document = document;
                Start = start;
                End = end;
            }
        }
    }
}
