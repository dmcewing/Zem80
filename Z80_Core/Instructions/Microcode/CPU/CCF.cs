using System;
using System.Collections.Generic;
using System.Text;

namespace Z80.Core
{
    public class CCF : IMicrocode
    {
        public ExecutionResult Execute(Processor cpu, ExecutionPackage package)
        {
            Flags flags = cpu.Registers.Flags;
            bool carry = flags.Carry;
            flags.Carry = !carry;
            flags.HalfCarry = carry;
            flags.Subtract = false;
            flags.X = (cpu.Registers.A & 0x08) > 0; // copy bit 3
            flags.Y = (cpu.Registers.A & 0x20) > 0; // copy bit 5
            return new ExecutionResult(package, flags);
        }

        public CCF()
        {
        }
    }
}
