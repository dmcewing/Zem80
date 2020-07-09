using System;
using System.Collections.Generic;
using System.Text;

namespace Z80.Core
{
    public class SLA : IMicrocode
    {
        public ExecutionResult Execute(Processor cpu, ExecutionPackage package)
        {
            Instruction instruction = package.Instruction;
            InstructionData data = package.Data;
            Flags flags = cpu.Registers.Flags;
            Registers r = cpu.Registers;
            sbyte offset = (sbyte)(data.Argument1);
            ByteRegister register = instruction.Target.AsByteRegister();

            void setFlags(byte original, byte shifted)
            {
                flags = FlagLookup.BitwiseFlags(shifted, BitwiseOperation.ShiftLeft);
                flags.HalfCarry = false;
                flags.Subtract = false;
            }

            byte original, shifted;
            if (register != ByteRegister.None)
            {
                original = r[register];
                shifted = (byte)(original << 1);
                shifted = shifted.SetBit(0, original.GetBit(0));
                setFlags(original, shifted);
                r[register] = shifted;
            }
            else
            {
                ushort address = instruction.Prefix switch
                {
                    InstructionPrefix.CB => r.HL,
                    InstructionPrefix.DDCB => (ushort)(r.IX + offset),
                    InstructionPrefix.FDCB => (ushort)(r.IY + offset),
                    _ => (ushort)0xFFFF
                };
                original = cpu.Memory.ReadByteAt(address, false);
                shifted = (byte)(original << 1);
                shifted = shifted.SetBit(0, original.GetBit(0));
                setFlags(original, shifted);
                if (instruction.IsIndexed) cpu.Timing.InternalOperationCycle(4);
                cpu.Memory.WriteByteAt(address, shifted, false);
            }

            return new ExecutionResult(package, flags);
        }

        public SLA()
        {
        }
    }
}
