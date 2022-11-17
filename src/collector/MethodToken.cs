using System.IO;

namespace Neo.Collector
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

        public static MethodToken Read(BinaryReader reader)
        {
            var hash = Hash160.Read(reader);
            var method = reader.ReadVarString(32);
            var parametersCount = reader.ReadUInt16();
            var hasReturnValue = reader.ReadBoolean();
            var callFlags = reader.ReadByte();

            return new MethodToken(hash, method, parametersCount, hasReturnValue, callFlags);
        }
    }
}