using System;
using System.Collections.Generic;
using System.Text;

namespace Zem80.Core.CPU
{
    public class NOP : IMicrocode
    {
        public ExecutionResult Execute(Processor cpu, InstructionPackage package)
        {
            return new ExecutionResult(package, null);
        }

        public NOP()
        {
        }
    }
}
