﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Zem80.Core.InputOutput;
using Zem80.Core.Memory;
using Stack = Zem80.Core.CPU.Stack;
using System.Runtime.InteropServices;

namespace Zem80.Core.CPU
{
    public partial class Processor : IDisposable
    {
        public const int MAX_MEMORY_SIZE_IN_BYTES = 65536;
        public const float DEFAULT_PROCESSOR_FREQUENCY_IN_MHZ = 4;

        private bool _running;
        private bool _halted;
        private bool _suspended;

        private HaltReason _reasonForLastHalt;
        private int _waitCyclesAdded;

        private Thread _instructionCycle;
        private InstructionDecoder _instructionDecoder;

        private bool _looping;

        public bool EndOnHalt { get; private set; }

        public IRegisters Registers { get; init; }
        public IStack Stack { get; init; }
        public IPorts Ports { get; init; }
        public IIO IO { get; init; }
        public IInterrupts Interrupts { get; init; }
        public ProcessorTiming Timing { get; init; }
        public IDebugProcessor Debug { get; init; }
        public IMemoryBank Memory { get; init; }
        public IClock Clock { get; init; }

        public IReadOnlyFlags Flags => new Flags(Registers.F, true);

        public ProcessorState State => _running ? _halted ? ProcessorState.Halted : ProcessorState.Running : ProcessorState.Stopped;

        public DateTime LastStarted { get; private set; }
        public DateTime LastStopped { get; private set; }
        public long LastRunTimeInMilliseconds => (LastStopped - LastStarted).Milliseconds;

        public InstructionPackage ExecutingInstructionPackage { get; private set; }

        public event EventHandler<InstructionPackage> BeforeExecuteInstruction;
        public event EventHandler<ExecutionResult> AfterExecuteInstruction;
        public event EventHandler<int> BeforeInsertWaitCycles;
        public event EventHandler AfterInitialise;
        public event EventHandler OnStop;
        public event EventHandler OnSuspend;
        public event EventHandler OnResume;
        public event EventHandler<HaltReason> OnHalt;

        public void Dispose()
        {
            _running = false;
            _instructionCycle?.Interrupt(); // just in case
        }

        public void Start(ushort address = 0x0000, bool endOnHalt = false, InterruptMode interruptMode = InterruptMode.IM0)
        {
            EndOnHalt = endOnHalt; // if set, will summarily end execution at the first HALT instruction. This is mostly for test / debug scenarios.
            Interrupts.SetMode(interruptMode);
            Interrupts.Disable();
            Registers.PC = address; // ordinarily, execution will start at 0x0000, but this can be overridden

            AfterInitialise?.Invoke(null, null);

            _running = true;

            IO.Clear();
            _instructionCycle = new Thread(InstructionCycle);
            _instructionCycle.IsBackground = true;
            _instructionCycle.Start();

            LastStarted = DateTime.Now;
        }

        public void Stop()
        {
            _running = false;
            _halted = false;

            Clock.Stop();
            LastStopped = DateTime.Now;

            OnStop?.Invoke(null, null);
        }

        public void Suspend()
        {
            _suspended = true;
            OnSuspend?.Invoke(null, null);
        }

        public void Resume()
        {
            if (_halted)
            {
                _halted = false;
                // if coming back from a HALT instruction (at next interrupt or by API override here), move the Program Counter on to step over the HALT instruction
                // otherwise we'll HALT forever in a loop
                if (_reasonForLastHalt == HaltReason.HaltInstruction) Registers.PC++;
            }

            _suspended = false;
            OnResume?.Invoke(null, null);
        }

        public void RunUntilStopped()
        {
            while (_running) Thread.Sleep(1); // main thread can sleep while instruction thread does its thing
        }

        public void ResetAndClearMemory(bool restartAfterReset = true, ushort startAddress = 0, InterruptMode interruptMode = InterruptMode.IM0)
        {
            IO.SetResetState();
            Stop();
            Memory.Clear();
            Registers.Clear();
            IO.Clear();
            Registers.SP = Stack.Top;
            if (restartAfterReset)
            {
                Start(startAddress, EndOnHalt, interruptMode);
            }
        }

        public void Halt(HaltReason reason = HaltReason.HaltCalledDirectly)
        {
            if (!_halted)
            {
                _halted = true;
                _reasonForLastHalt = reason;
                OnHalt?.Invoke(null, reason);

                if (EndOnHalt)
                {
                    Stop();
                }
            }
        }

        private void InstructionCycle()
        {
            Clock.Start();

            while (_running)
            {
                if (_suspended)
                {
                    Thread.Sleep(1);
                }
                else
                {
                    bool skipNextByte = false;

                    do
                    {
                        ushort address = Registers.PC;
                        byte[] instructionBytes;

                        if (_halted || skipNextByte)
                        {
                            instructionBytes = new byte[4]; // when halted, we run NOP until resumed
                        }
                        else
                        {
                            instructionBytes = Memory.Untimed.ReadBytesAt(address, 4); // read 4 bytes, but instruction length could be 1-4
                        }

                        InstructionPackage package = _instructionDecoder.DecodeInstruction(instructionBytes, address, out skipNextByte, out bool isOpcodeErrorNOP);
                        Timing.OpcodeFetchTiming(package.Instruction, address);
                        Timing.OperandReadTiming(package.Instruction, address, package.Data.Argument1, package.Data.Argument2);

                        // all the rest of the timing happens during the execution of the instruction microcode, and the microcode is responsible for it

                        Registers.PC += (ushort)package.Instruction.SizeInBytes;
                        ExecuteInstruction(package);

                        if (!isOpcodeErrorNOP) Interrupts.Handle(package, ExecuteInstruction);
                        RefreshMemory();
                    }
                    while (skipNextByte);
                }
            }
        }

        private void ExecuteInstruction(InstructionPackage package)
        {
            ExecutingInstructionPackage = package;
            BeforeExecuteInstruction?.Invoke(this, package);

            // check for breakpoints
            Debug.EvaluateAndRunBreakpoint(package.InstructionAddress, package);

            // set the internal WZ register to an initial value based on whether this is an indexed instruction or not; the instruction that runs may alter/set WZ itself
            // the value in WZ (sometimes known as MEMPTR in Z80 enthusiast circles) is only ever used to control the behavior of the BIT instruction
            if (!_looping && package.Instruction.IsIndexed)
            {
                Registers.WZ = (ushort)(Registers[package.Instruction.IndexedRegister] + package.Data.Argument1);
            }
            else
            {
                Registers.WZ = 0x0000;
            }

            ExecutionResult result = package.Instruction.Microcode.Execute(this, package);
            if (result.Flags != null) Registers.F = result.Flags.Value;
            result.WaitCyclesAdded = _waitCyclesAdded;
            AfterExecuteInstruction?.Invoke(this, result);
            ExecutingInstructionPackage = null;

            _looping = (package.Instruction.IsLoopingInstruction && Registers.PC == package.InstructionAddress);
        }

        private void RefreshMemory()
        {
            Registers.R = (byte)(Registers.R + 1 & 0x7F | Registers.R & 0x80); // bits 0-6 of R are incremented as part of the memory refresh - bit 7 is preserved 
        }

        public Processor(IMemoryBank memory = null, IMemoryMap map = null, IStack stack = null, IClock clock = null, IRegisters registers = null, IPorts ports = null,
            ProcessorTiming cycleTiming = null, IIO io = null, IInterrupts interrupts = null, IDebugProcessor debug = null, ushort topOfStackAddress = 0xFFFF)
        {
            // Default clock is the FastClock which, well, isn't really a clock. It'll run as fast as possible on the hardware and in .NET
            // but it'll *say* that it's running at 4MHz. It's a lying liar that lies. You may want a different clock - luckily there are several.
            // Clocks and timing are a thing, too much to go into here, so check the docs (one day, there will be docs!).
            Clock = clock ?? ClockMaker.FastClock(DEFAULT_PROCESSOR_FREQUENCY_IN_MHZ);
            Clock.Initialise(this);

            Timing = cycleTiming ?? new ProcessorTiming(this);
            Registers = registers ?? new Registers();
            Ports = ports ?? new Ports(Timing);
            IO = io ?? new IO(this);
            Interrupts = interrupts ?? new Interrupts(this);
            Debug = debug ?? new DebugProcessor(this, ExecuteInstruction);

            // You can supply your own memory implementations, for example if you need to do RAM paging for >64K implementations.
            // Since there are several different methods for doing this and no 'official' method, there is no paged RAM implementation in the core code.
            Memory = memory ?? new MemoryBank();
            Memory.Initialise(this, map ?? new MemoryMap(MAX_MEMORY_SIZE_IN_BYTES, true));

            // stack pointer defaults to 0xFFFF - this is undocumented but verified behaviour of the Z80
            Stack = stack ?? new Stack(topOfStackAddress, this);
            Registers.SP = Stack.Top;

            // The Z80 instruction set needs to be built (all Instruction objects are created, bound to the microcode instances, and indexed into a hashtable - undocumented 'overloads' are built here too)
            InstructionSet.Build();
            _instructionDecoder = new InstructionDecoder(this);
        }
    }
}
