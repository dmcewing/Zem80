using System;
using System.Collections.Generic;
using System.Text;

namespace Zem80.Core.CPU
{
    public class SRA : IMicrocode
    {
        public ExecutionResult Execute(Processor cpu, InstructionPackage package)
        {
            Instruction instruction = package.Instruction;
            InstructionData data = package.Data;
            Flags flags = cpu.Flags.Clone();
            IRegisters r = cpu.Registers;
            sbyte offset = (sbyte)(data.Argument1);
            ByteRegister register = instruction.Target.AsByteRegister();

            void setFlags(byte original)
            {
                flags = FlagLookup.BitwiseFlags(original, BitwiseOperation.ShiftRightPreserveBit7, flags.Carry);
                flags.Carry = original.GetBit(0);
                flags.HalfCarry = false;
                flags.Subtract = false;
            }

            byte original, shifted;
            if (register != ByteRegister.None)
            {
                original = r[register];
                shifted = (byte)(original >> 1);
                shifted = shifted.SetBit(7, original.GetBit(7));
                setFlags(original);
                r[register] = shifted;
            }
            else
            {
                ushort address = instruction.Prefix switch
                {
                    0xCB => r.HL,
                    0xDDCB => (ushort)(r.IX + offset),
                    0xFDCB => (ushort)(r.IY + offset),
                    _ => (ushort)0xFFFF
                };
                original = cpu.Memory.ReadByteAt(address, 4);
                shifted = (byte)(original >> 1);
                shifted = shifted.SetBit(7, original.GetBit(7));
                setFlags(original);
                if (instruction.IsIndexed) cpu.Timing.InternalOperationCycle(4);
                cpu.Memory.WriteByteAt(address, shifted, 3);
                if (instruction.CopiesResultToRegister)
                {
                    r[instruction.CopyResultTo] = shifted;
                }
            }

            return new ExecutionResult(package, flags);
        }

        public SRA()
        {
        }
    }
}
