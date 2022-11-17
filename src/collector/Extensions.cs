using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

namespace Neo.Collector
{
    static class Extensions
    {
        public static IDisposable StartDocument(this XmlWriter @this)
        {
            @this.WriteStartDocument();
            return new DelegateDisposable(() => @this.WriteEndDocument());
        }

        public static IDisposable StartElement(this XmlWriter @this, string localName)
        {
            @this.WriteStartElement(localName);
            return new DelegateDisposable(() => @this.WriteEndElement());
        }

        public static bool TryParseScriptHash(this string @this, out string hash)
        {
            if (TryParseHexString(@this, out var buffer)
                && buffer.Length == 20)
            {
                hash = BitConverter.ToString(buffer);
                return true;
            }

            hash = string.Empty;
            return false;
        }

        public static bool TryParseHexString(this string @this, out byte[] buffer)
        {
            buffer = Array.Empty<byte>();
            if (@this.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) @this = @this.Substring(2);
            if (@this.Length % 2 != 0) return false;

            var length = @this.Length / 2;
            buffer = new byte[length];
            for (var i = 0; i < length; i++)
            {
                var str = @this.Substring(i * 2, 2);
                if (!byte.TryParse(str, NumberStyles.AllowHexSpecifier, null, out buffer[i]))
                {
                    return false;
                }
            }
            return true;
        }
    }
}