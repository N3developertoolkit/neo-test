using System.Collections.Generic;

namespace Neo.Collector
{
    public class SequencePoint
    {
        public string Document { get; }
        public uint Address { get; }
        public (uint Line, uint Column) Start { get; }
        public (uint Line, uint Column) End { get; }

        public SequencePoint(string document, uint address, (uint Line, uint Column) start, (uint Line, uint Column) end)
        {
            Document = document;
            Address = address;
            Start = start;
            End = end;
        }
    }
}