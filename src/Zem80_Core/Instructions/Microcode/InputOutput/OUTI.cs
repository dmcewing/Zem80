using System;
using System.Collections.Generic;
using System.Text;
using Zem80.Core.IO;

namespace Zem80.Core.Instructions
{
    public class OUTI : IMicrocode
    {
        public ExecutionResult Execute(Processor cpu, InstructionPackage package)
        {
            Instruction instruction = package.Instruction;
            InstructionData data = package.Data;
            Flags flags = cpu.Registers.Flags;
            Registers r = cpu.Registers;

            Port port = cpu.Ports[r.C];
            byte output = cpu.Memory.ReadByteAt(r.HL, false);
            r.WZ = r.BC;
            r.B--;
            port.SignalWrite();
            port.WriteByte(output, true);
            r.HL++;

            flags.Zero = (r.B == 0);
            flags.Subtract = true;
            flags.X = (output & 0x08) > 0; // copy bit 3
            flags.Y = (output & 0x20) > 0; // copy bit 5

            return new ExecutionResult(package, flags);
        }

        public OUTI()
        {
        }
    }
}
