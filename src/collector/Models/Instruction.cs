using System;

namespace Neo.Collector.Models
{
    class Instruction
    {
        public OpCode OpCode { get; }
        public ArraySegment<byte> Operand { get; }
        public int Size { get; }

        public Instruction(byte[] script, int address)
        {
            if (address >= script.Length) throw new ArgumentOutOfRangeException(nameof(address));

            OpCode = (OpCode)script[address];
            switch (OpCode)
            {
                case OpCode.PUSHDATA1:
                    {
                        int opSize = script[address + 1];
                        Size = 1 + 1 + opSize;
                        Operand = new ArraySegment<byte>(script, address + 2, opSize);
                    }
                    break;
                case OpCode.PUSHDATA2:
                    {
                        int opSize = BitConverter.ToUInt16(script, address + 1);
                        Size = 1 + 2 + opSize;
                        Operand = new ArraySegment<byte>(script, address + 3, opSize);
                    }
                    break;
                case OpCode.PUSHDATA4:
                    {
                        int opSize = BitConverter.ToInt32(script, address + 1);
                        Size = 1 + 4 + opSize;
                        Operand = new ArraySegment<byte>(script, address + 1 + 4, opSize);
                    }
                    break;
                default:
                    {
                        var opSize = GetOperandSize(OpCode);
                        Size = 1 + opSize;
                        Operand = new ArraySegment<byte>(script, address + 1, opSize);
                    }
                    break;
            }
        }

        static int GetOperandSize(OpCode opCode)
        {
            switch (opCode)
            {
                case OpCode.PUSHDATA1:
                case OpCode.PUSHDATA2:
                case OpCode.PUSHDATA4:
                    throw new ArgumentException(nameof(opCode));
                case OpCode.PUSHINT8:
                case OpCode.JMP:
                case OpCode.JMPEQ:
                case OpCode.JMPGE:
                case OpCode.JMPGT:
                case OpCode.JMPIF:
                case OpCode.JMPIFNOT:
                case OpCode.JMPLE:
                case OpCode.JMPLT:
                case OpCode.JMPNE:
                case OpCode.CALL:
                case OpCode.ENDTRY:
                case OpCode.INITSSLOT:
                case OpCode.LDSFLD:
                case OpCode.STSFLD:
                case OpCode.LDLOC:
                case OpCode.STLOC:
                case OpCode.LDARG:
                case OpCode.STARG:
                case OpCode.NEWARRAY_T:
                case OpCode.ISTYPE:
                case OpCode.CONVERT:
                    return 1;
                case OpCode.PUSHINT16:
                case OpCode.CALLT:
                case OpCode.TRY:
                case OpCode.INITSLOT:
                    return 2;
                case OpCode.PUSHINT32:
                case OpCode.PUSHA:
                case OpCode.JMP_L:
                case OpCode.JMPEQ_L:
                case OpCode.JMPGE_L:
                case OpCode.JMPGT_L:
                case OpCode.JMPIF_L:
                case OpCode.JMPIFNOT_L:
                case OpCode.JMPLE_L:
                case OpCode.JMPLT_L:
                case OpCode.JMPNE_L:
                case OpCode.CALL_L:
                case OpCode.ENDTRY_L:
                case OpCode.SYSCALL:
                    return 4;
                case OpCode.PUSHINT64:
                case OpCode.TRY_L:
                    return 8;
                case OpCode.PUSHINT128:
                    return 16;
                case OpCode.PUSHINT256:
                    return 32;
                default:
                    return 0;
            }
        }
    }
}