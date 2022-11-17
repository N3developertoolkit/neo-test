using System;
using System.IO;
using System.Text;

namespace Neo.Collector
{
    public struct Hash160 : IComparable<Hash160>, IEquatable<Hash160>
    {
        public static readonly Hash160 Zero = new Hash160(0, 0, 0);
        public const int Size = (2 * sizeof(ulong)) + sizeof(uint);

        readonly ulong data1;
        readonly ulong data2;
        readonly uint data3;

        internal Hash160(ulong data1, ulong data2, uint data3)
        {
            this.data1 = data1;
            this.data2 = data2;
            this.data3 = data3;
        }

        public static Hash160 Read(BinaryReader reader)
        {
            var value1 = reader.ReadUInt64();
            var value2 = reader.ReadUInt64();
            var value3 = reader.ReadUInt32();
            return new Hash160(value1, value2, value3);
        }

        public static bool TryParse(string @string, out Hash160 result)
        {
            if (@string.TryParseHexString(out var buffer)
                && buffer.Length == Size)
            {
                using (var stream = new MemoryStream(buffer))
                using (var reader = new BinaryReader(stream))
                {
                    result = Read(reader);
                    return true;
                }
            }

            result = default;
            return false;
        }

        public int CompareTo(Hash160 other)
        {
            var result = data1.CompareTo(other.data1);
            if (result != 0)
                return result;

            result = data2.CompareTo(other.data2);
            if (result != 0)
                return result;

            return data3.CompareTo(other.data3);
        }

        public bool Equals(Hash160 other)
            => (data1 == other.data1)
                && (data2 == other.data2)
                && (data3 == other.data3);

        public override bool Equals(object obj) => (obj is Hash160 value) && Equals(value);

        public override int GetHashCode()
        {
            unchecked // Overflow is fine, just wrap
            {
                int hash = 17;
                // Suitable nullity checks etc, of course :)
                hash = hash * 23 + data1.GetHashCode();
                hash = hash * 23 + data2.GetHashCode();
                hash = hash * 23 + data3.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            var builder = new StringBuilder("0x", 2 + (Size * 2));
            builder.AppendFormat("{0:x}", data1);
            builder.AppendFormat("{0:x}", data2);
            builder.AppendFormat("{0:x}", data3);
            return builder.ToString();
        }
    }
}