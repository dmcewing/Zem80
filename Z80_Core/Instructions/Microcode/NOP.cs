using System;
using System.Collections.Generic;
using System.Text;

namespace Z80.Core
{
    public class NOP : IInstructionImplementation
    {
        public ExecutionResult Execute(Processor cpu, InstructionPackage package)
        {
            return new ExecutionResult(new Flags(), 0);
        }

        public NOP()
        {
        }
    }
}
