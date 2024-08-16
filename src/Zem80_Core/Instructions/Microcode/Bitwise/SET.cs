using System;
using System.Collections.Generic;
using System.Text;

namespace Zem80.Core.CPU
{
    public class SET : IMicrocode
    {
        public ExecutionResult Execute(Processor cpu, InstructionPackage package)
        {
            Instruction instruction = package.Instruction;
            InstructionData data = package.Data;
            IRegisters r = cpu.Registers;
            byte bitIndex = instruction.GetBitIndex();
            sbyte offset = (sbyte)(data.Argument1);
            
            ByteRegister register = instruction.Source.AsByteRegister();
            if (register != ByteRegister.None)
            {
                byte value = r[register].SetBit(bitIndex, true);
                r[register] = value;
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
                if (instruction.IsIndexed) cpu.Timing.InternalOperationCycle(5);
                
                byte value = cpu.Memory.ReadByteAt(address, 4);
                value = value.SetBit(bitIndex, true);
                cpu.Memory.WriteByteAt(address, value, 3);
                if (instruction.CopiesResultToRegister)
                {
                    r[instruction.CopyResultTo] = value;
                }
            }

            return new ExecutionResult(package, null);
        }

        public SET()
        {
        }
    }
}
