﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Schema;
using Zem80.Core.CPU;
using Zem80.Core.Instructions;

namespace Zem80.Core.Memory
{
    public class MemoryBank : IMemoryBank
    {
        internal IMemoryMap _map;
        internal Processor _cpu;
        internal bool _initialised;

        public IMemory Untimed { get; init; }

        public uint SizeInBytes => _map.SizeInBytes;

        public IMemory TimedFor(Instruction instruction)
        {
            return new MemoryWrapper(this, true, instruction);
        }

        public void Clear()
        {
            _map.ClearAllWritableMemory();
        }

        public void Initialise(Processor cpu, IMemoryMap map)
        {
            _cpu = cpu;
            _map = map;
            _initialised = true;
        }

        internal byte ReadByteAt(ushort address, bool timed)
        {
            if (!_initialised) throw new MemoryNotInitialisedException();

            IMemorySegment segment = _map.SegmentFor(address);
            byte output = (segment?.ReadByteAt(AddressOffset(address, segment)) ?? 0x00); // 0x00 if address is unallocated
            if (timed) _cpu.Timing.MemoryReadCycle(address, output, 0);
            return output;
        }

        internal byte[] ReadBytesAt(ushort address, ushort numberOfBytes, bool timed)
        {
            if (!_initialised) throw new MemoryNotInitialisedException();

            uint availableBytes = numberOfBytes;
            if (address + availableBytes >= SizeInBytes) availableBytes = SizeInBytes - address; // if this read overflows the end of memory, we can read only this many bytes

            IMemorySegment segment = _map.SegmentFor(address);
            if (segment == null) return new byte[numberOfBytes]; // if memory is not allocated return all 0x00s

            if (!timed && segment.SizeInBytes - AddressOffset(address, segment) >= numberOfBytes)
            {
                // if the read fits entirely within the memory segment and we aren't generating read timing, then optimise for speed
                return segment.ReadBytesAt(AddressOffset(address, segment), numberOfBytes);
            }
            else
            {
                byte[] bytes = new byte[numberOfBytes];
                for (int i = 0; i < availableBytes; i++)
                {
                    bytes[i] = ReadByteAt((ushort)(address + i), timed);
                }
                return bytes; // bytes beyond the available byte limit (if any) will be 0x00
            }
        }

        internal ushort ReadWordAt(ushort address, bool timed)
        {
            if (!_initialised) throw new MemoryNotInitialisedException();

            byte low = ReadByteAt(address, timed);
            byte high = ReadByteAt(++address, timed);
            return (ushort)((high * 256) + low);
        }

        internal void WriteByteAt(ushort address, byte value, bool timed)
        {
            if (!_initialised) throw new MemoryNotInitialisedException();

            IMemorySegment segment = _map.SegmentFor(address);
            if (segment != null && !segment.ReadOnly)
            {
                segment.WriteByteAt(AddressOffset(address, segment), value);
            }

            if (timed) _cpu.Timing.MemoryWriteCycle(address, value, 0);
        }

        internal void WriteBytesAt(ushort address, byte[] bytes, bool timed)
        {
            // similar optimisation to ReadBytesAt above
            if (!_initialised) throw new MemoryNotInitialisedException();

            IMemorySegment segment = _map.SegmentFor(address);
            if (segment != null && !segment.ReadOnly)
            {
                if (!timed && segment.SizeInBytes - AddressOffset(address, segment) >= bytes.Length)
                {
                    segment.WriteBytesAt(AddressOffset(address, segment), bytes);
                }
                else
                {
                    for (ushort i = 0; i < bytes.Length; i++)
                    {
                        WriteByteAt((ushort)(address + i), bytes[i], timed);
                    }
                }
            }
        }

        internal void WriteWordAt(ushort address, ushort value, bool timed)
        {
            if (!_initialised) throw new MemoryNotInitialisedException();

            WriteByteAt(address, (byte)(value % 256), timed);
            WriteByteAt(++address, (byte)(value / 256), timed);
        }


        internal ushort AddressOffset(ushort address, IMemorySegment segment)
        {
            return (ushort)(address - segment.StartAddress);
        }

        public MemoryBank()
        {
            Untimed = new MemoryWrapper(this, false);
        }
    }
}
