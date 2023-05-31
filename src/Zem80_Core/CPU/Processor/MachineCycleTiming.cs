﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zem80.Core.CPU;
using Zem80.Core.Instructions;

namespace Zem80.Core.CPU
{
    public class MachineCycleTiming : IMachineCycleTiming
    {
        private Processor _cpu;

        public int WaitCyclesAdded { get; private set; }

        void IMachineCycleTiming.OpcodeFetchCycle(ushort address, byte opcode, byte extraTStates)
        {
            _cpu.IO.SetOpcodeFetchState(address);
            _cpu.Clock.WaitForNextClockTick();
            _cpu.IO.AddOpcodeFetchData(opcode);
            _cpu.Clock.WaitForNextClockTick();
            InsertWaitCycles();

            _cpu.IO.EndOpcodeFetchState();
            _cpu.IO.SetAddressBusValue(_cpu.Registers.IR);
            _cpu.IO.SetDataBusValue(0x00);

            _cpu.Clock.WaitForNextClockTick();
            _cpu.Clock.WaitForNextClockTick();

            if (extraTStates > 0) _cpu.Clock.WaitForClockTicks(extraTStates);
        }

        void IMachineCycleTiming.MemoryReadCycle(ushort address, byte data, byte extraTStates)
        {
            _cpu.IO.SetMemoryReadState(address);
            _cpu.Clock.WaitForNextClockTick();

            _cpu.IO.AddMemoryData(data);
            _cpu.Clock.WaitForNextClockTick();
            InsertWaitCycles();

            _cpu.IO.EndMemoryReadState();
            _cpu.Clock.WaitForNextClockTick();

            if (extraTStates > 0) _cpu.Clock.WaitForClockTicks(extraTStates);
        }

        void IMachineCycleTiming.MemoryWriteCycle(ushort address, byte data, byte extraTStates)
        {
            _cpu.IO.SetMemoryWriteState(address, data);
            _cpu.Clock.WaitForNextClockTick();
            _cpu.Clock.WaitForNextClockTick();
            InsertWaitCycles();

            _cpu.IO.EndMemoryWriteState();
            _cpu.Clock.WaitForNextClockTick();

            if (extraTStates > 0) _cpu.Clock.WaitForClockTicks(extraTStates);
        }

        void IMachineCycleTiming.BeginStackReadCycle()
        {
            _cpu.IO.SetMemoryReadState(_cpu.Registers.SP);
            _cpu.Clock.WaitForNextClockTick();
        }


        void IMachineCycleTiming.EndStackReadCycle(bool highByte, byte data)
        {
            _cpu.IO.AddMemoryData(data);
            _cpu.Clock.WaitForNextClockTick();
            InsertWaitCycles();

            _cpu.IO.EndMemoryReadState();
            _cpu.Clock.WaitForNextClockTick();
        }

        void IMachineCycleTiming.BeginStackWriteCycle(bool highByte, byte data)
        {
            _cpu.IO.SetMemoryWriteState(_cpu.Registers.SP, data);
            _cpu.Clock.WaitForNextClockTick();
            _cpu.Clock.WaitForNextClockTick();
            InsertWaitCycles();
        }

        void IMachineCycleTiming.EndStackWriteCycle()
        {
            _cpu.IO.EndMemoryWriteState();
            _cpu.Clock.WaitForNextClockTick();
        }

        void IMachineCycleTiming.BeginPortReadCycle(byte n, bool bc)
        {
            ushort address = bc ? (_cpu.Registers.C, _cpu.Registers.B).ToWord() : (n, _cpu.Registers.A).ToWord();

            _cpu.IO.SetPortReadState(address);
            _cpu.Clock.WaitForNextClockTick();
        }

        void IMachineCycleTiming.EndPortReadCycle(byte data)
        {
            _cpu.IO.AddPortReadData(data);
            _cpu.Clock.WaitForNextClockTick();
            InsertWaitCycles();

            _cpu.Clock.WaitForNextClockTick();
            _cpu.IO.EndPortReadState();
            _cpu.Clock.WaitForNextClockTick();
        }

        void IMachineCycleTiming.BeginPortWriteCycle(byte data, byte n, bool bc)
        {
            ushort address = bc ? (_cpu.Registers.C, _cpu.Registers.B).ToWord() : (n, _cpu.Registers.A).ToWord();

            _cpu.IO.SetPortWriteState(address, data);
            _cpu.Clock.WaitForNextClockTick();
        }

        void IMachineCycleTiming.EndPortWriteCycle()
        {
            _cpu.Clock.WaitForNextClockTick();
            InsertWaitCycles();

            _cpu.Clock.WaitForNextClockTick();
            _cpu.IO.EndPortWriteState();
            _cpu.Clock.WaitForNextClockTick();
        }

        void IMachineCycleTiming.BeginInterruptRequestAcknowledgeCycle(int tStates)
        {
            _cpu.IO.SetInterruptState();
            for (int i = 0; i < tStates; i++)
            {
                _cpu.Clock.WaitForNextClockTick();
            }
        }

        void IMachineCycleTiming.EndInterruptRequestAcknowledgeCycle()
        {
            _cpu.IO.EndInterruptState();
        }

        void IMachineCycleTiming.InternalOperationCycle(int tStates)
        {
            for (int i = 0; i < tStates; i++)
            {
                _cpu.Clock.WaitForNextClockTick();
            }
        }

        private void InsertWaitCycles()
        {
            int cyclesToAdd = _cpu.PendingWaitCycles;
            _cpu.Clock.WaitForClockTicks(cyclesToAdd);
            WaitCyclesAdded = cyclesToAdd;
        }

        public MachineCycleTiming(Processor cpu)
        {
            _cpu = cpu;
        }
    }
}
