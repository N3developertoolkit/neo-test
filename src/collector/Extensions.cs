using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using Neo.Collector.Models;

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

        public static ulong ReadVarInt(this BinaryReader @this, ulong max = ulong.MaxValue)
        {
            byte b = @this.ReadByte();
            ulong value;
            switch (b)
            {
                case 0xfd:
                    value = @this.ReadUInt16();
                    break;
                case 0xfe:
                    value = @this.ReadUInt32();
                    break;
                case 0xff:
                    value = @this.ReadUInt64();
                    break;
                default:
                    value = b;
                    break;
            }
            if (value > max) throw new FormatException();
            return value;
        }

        public static byte[] ReadVarMemory(this BinaryReader @this, int max = 0x1000000)
        {
            var length = (int)@this.ReadVarInt((ulong)max);
            return @this.ReadBytes(length);
        }

        public static string ReadVarString(this BinaryReader @this, int max = 0x1000000)
        {
            return Encoding.UTF8.GetString(@this.ReadVarMemory(max));
        }

        public static IEnumerable<(int address, Instruction instruction)> EnumerateInstructions(this byte[] script)
        {
            int address = 0;
            while (address < script.Length)
            {
                var instruction = Instruction.Parse(script, address);
                yield return (address, instruction);
                address += instruction.Size;
            }
        }

        public static IEnumerable<(int address, Instruction instruction)> EnumerateInstructions(this Stream stream)
        {
            int address = 0;
            var reader = new BinaryReader(stream);
            while (reader.TryReadOpCode(out var opCode))
            {
                var instruction = Instruction.Parse(opCode, reader);
                yield return (address, instruction);
                address += instruction.Size;
            }
        }

        static bool TryReadOpCode(this BinaryReader @this, out OpCode value)
        {
            try
            {
                value = (OpCode)@this.ReadByte();
                return true;
            }
            catch (EndOfStreamException)
            {
                value = default;
                return false;
            }
        }

        public static bool IsBranchInstruction(this Instruction instruction)
            => instruction.OpCode >= OpCode.JMPIF
                && instruction.OpCode <= OpCode.JMPLE_L;

        public static int GetCallOffset(this Instruction instruction)
        {
            switch (instruction.OpCode)
            {
                case OpCode.CALL_L:
                    return BinaryPrimitives.ReadInt32LittleEndian(instruction.Operand.AsSpan());
                case OpCode.CALL:
                    return (sbyte)instruction.Operand.AsSpan()[0];
                default:
                    return 0;
            }
        }

        public static int GetBranchOffset(this Instruction instruction)
        {
            switch (instruction.OpCode)
            {
                case OpCode.JMPIF_L:
                case OpCode.JMPIFNOT_L:
                case OpCode.JMPEQ_L:
                case OpCode.JMPNE_L:
                case OpCode.JMPGT_L:
                case OpCode.JMPGE_L:
                case OpCode.JMPLT_L:
                case OpCode.JMPLE_L:
                    return BinaryPrimitives.ReadInt32LittleEndian(instruction.Operand.AsSpan());
                case OpCode.JMPIF:
                case OpCode.JMPIFNOT:
                case OpCode.JMPEQ:
                case OpCode.JMPNE:
                case OpCode.JMPGT:
                case OpCode.JMPGE:
                case OpCode.JMPLT:
                case OpCode.JMPLE:
                    return (sbyte)instruction.Operand.AsSpan()[0];
                default:
                    return 0;
            }
        }
    }
}