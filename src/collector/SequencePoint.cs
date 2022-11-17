using System.Collections.Generic;

namespace Neo.Collector
{
    public class SequencePoint
    {
        public string Document { get; }
        public string Namespace { get; }
        public string Name { get; }
        public uint Address { get; }
        public (uint Line, uint Column) Start { get; }
        public (uint Line, uint Column) End { get; }

        public SequencePoint(string document, string @namespace, string name, uint address, (uint Line, uint Column) start, (uint Line, uint Column) end)
        {
            Document = document;
            Namespace = @namespace;
            Name = name;
            Address = address;
            Start = start;
            End = end;
        }
    }
}