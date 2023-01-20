using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
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

        public static bool IsCallInstruction(this Instruction instruction)
            => instruction.OpCode == OpCode.CALL
                || instruction.OpCode == OpCode.CALL_L;

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

        // public static (int methodEnd, int nextLineAddress) GetBoundary(this NeoDebugInfo.SequencePoint point, NeoDebugInfo.Method method)
        // {
        //     var spIndex = method.SequencePoints.IndexOf(sp => sp.Address == point.Address);
        //     if (spIndex < 0) throw new ArgumentException("Sequence point not in Method", nameof(method));
        //     var nextLineAddress = spIndex + 1 < method.SequencePoints.Count 
        //             ? method.SequencePoints[spIndex + 1].Address
        //             : int.MaxValue;

        //     return (method.Range.End, nextLineAddress);
        // }

        public static IEnumerable<ImmutableList<(int, bool)>> GetBranchPaths(this IReadOnlyDictionary<int, Instruction> instructionMap, NeoDebugInfo.Method method, int index)
        {
            var point = method.SequencePoints[index];
            var last = method.GetLastLineAddress(index, instructionMap);
            return instructionMap.GetBranchPaths(point.Address, last);
        }
        
        public static IEnumerable<ImmutableList<(int, bool)>> GetBranchPaths(this IReadOnlyDictionary<int, Instruction> instructionMap, int address, int lastAddress, ImmutableList<(int, bool)> path = null)
        {
            path = path is null ? ImmutableList<(int, bool)>.Empty : path;

            while (address <= lastAddress)
            {
                var ins = instructionMap[address];
                if (ins.OpCode == OpCode.RET)
                {
                    break;
                }
                else if (ins.IsBranchInstruction())
                {
                    var offset = ins.GetBranchOffset();
                    var branchAddress = address + offset;
                    var continueAddress = address + ins.Size;
                    var branchPaths = instructionMap.GetBranchPaths(branchAddress, lastAddress, path.Add((address, true)));
                    var continuePaths = instructionMap.GetBranchPaths(continueAddress, lastAddress, path.Add((address, false)));
                    return branchPaths.Concat(continuePaths);
                }
                else
                {
                    address += ins.Size;
                }
            }

            return Enumerable.Repeat(path, 1);
        }

        // public static void FindPaths(this NeoDebugInfo.SequencePoint point, NeoDebugInfo.Method method, IReadOnlyDictionary<int, Instruction> instructionMap)
        // {
        //     var (methodEnd, nextLineAddress) = point.GetBoundary(method);

        // ImmutableLiost
        //     var address = point.Address;
        //     while (true)
        //     {
        //         var instruction = instructionMap[address];

        //         if (instruction.IsBranchInstruction())
        //         {

        //         }
        //         else if (instruction.IsCallInstruction())
        //         {

        //         }
        //         else
        //         {

        //         }


        //     }

        // }

        // public static IEnumerable<(int, Instruction)> GetInstructions(this NeoDebugInfo.SequencePoint point, NeoDebugInfo.Method method, IReadOnlyDictionary<int, Instruction> instructionMap)
        // {
        //     var (methodEnd, nextLineAddress) = point.GetBoundary(method);

        //     var address = point.Address;
        //     while (address <= method.Range.End && address < nextLineAddress)
        //     {
        //         var instruction = instructionMap[address];
        //         yield return (address, instruction);
        //         address += instruction.Size;
        //     }
        // }

        public static int IndexOf<T>(this IReadOnlyList<T> @this, Func<T, bool> predicate)
        {
            for (int i = 0; i < @this.Count; i++)
            {
                if (predicate(@this[i])) return i;
            }
            return -1;
        }

        public static int GetLastAddress(this NeoDebugInfo.SequencePoint point, NeoDebugInfo.Method method, IReadOnlyDictionary<int, Instruction> instructionMap)
        {
            var index = method.SequencePoints.IndexOf(sp => sp.Address == point.Address);
            if (index < 0) throw new ArgumentException(nameof(point));
            return method.GetLastLineAddress(index, instructionMap);
        }

        public static int GetLastLineAddress(this NeoDebugInfo.Method method, int index, IReadOnlyDictionary<int, Instruction> instructionMap)
        {
            var point = method.SequencePoints[index];
            var nextIndex = index + 1;
            if (nextIndex >= method.SequencePoints.Count)
            {
                // if we're on the last SP of the method, return the method end address
                return method.Range.End;
            }
            else
            {
                var nextSPAddress = method.SequencePoints[index + 1].Address;
                var address = point.Address;
                while (true)
                {
                    var ins = instructionMap[address];
                    var newAddress = address + ins.Size;
                    if (newAddress >= nextSPAddress)
                    {
                        return address;
                    }
                    else
                    {
                        address = newAddress;
                    }
                }
            }
        }
    }
}