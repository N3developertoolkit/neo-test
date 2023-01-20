namespace Neo.Collector.Models
{
    public partial class NeoDebugInfo
    {
        public struct Parameter
        {
            public readonly string Name;
            public readonly string Type;
            public readonly int Index;

            public Parameter(string name, string type, int index)
            {
                Name = name;
                Type = type;
                Index = index;
            }
        }
    }
}
