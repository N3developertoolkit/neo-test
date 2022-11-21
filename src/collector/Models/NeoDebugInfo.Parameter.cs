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
    }
}
