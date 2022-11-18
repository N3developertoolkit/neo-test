using System.IO;

namespace Neo.Collector.Models
{
    public partial class NefFile
    {
        public class MethodToken
        {
            public Hash160 Hash { get; }
            public string Method { get; }
            public ushort ParametersCount { get; }
            public bool HasReturnValue { get; }
            public byte CallFlags { get; }

            public MethodToken(Hash160 hash, string method, ushort parametersCount, bool hasReturnValue, byte callFlags)
            {
                Hash = hash;
                Method = method;
                ParametersCount = parametersCount;
                HasReturnValue = hasReturnValue;
                CallFlags = callFlags;
            }
        }
    }
}