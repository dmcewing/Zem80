﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Z80.Core
{
    public class Memory : IMemory
    {
        private IMemoryMap _map;

        public uint SizeInBytes => _map.SizeInBytes;

        public byte ReadByteAt(ushort address)
        {
            IMemorySegment memory = _map.MemoryFor(address);
            return memory?.ReadByteAt(address) ?? 0x00; // default value if address is unallocated
        }

        public byte[] ReadBytesAt(ushort address, ushort numberOfBytes)
        {
            uint availableBytes = numberOfBytes;
            if (address + availableBytes >= SizeInBytes) availableBytes = SizeInBytes - address; // if this read overflows the end of memory, we can read only this many bytes

            byte[] bytes = new byte[availableBytes];
            for (ushort i = 0; i < availableBytes; i++)
            {
                bytes[i] = ReadByteAt((ushort)(address + i));
            }
            return bytes;
        }

        public ushort ReadWordAt(ushort address)
        {
            byte[] bytes = ReadBytesAt(address, 2);
            return (ushort)((bytes[1] * 256) + bytes[0]);
        }

        public void WriteByteAt(ushort address, byte value)
        {
            IMemorySegment memory = _map.MemoryFor(address);
            if (memory == null || memory.ReadOnly)
            {
                throw new Exception("Readonly or unmapped"); // TODO: custom exception type
            }

            memory.WriteByteAt(address, value);
        }

        public void WriteBytesAt(ushort address, params byte[] bytes)
        {
            for (ushort i = 0; i < bytes.Length; i++)
            {
                WriteByteAt((ushort)(address + i), bytes[i]);
            }
        }

        public void WriteWordAt(ushort address, ushort value)
        {
            byte[] bytes = new byte[2];
            bytes[1] = (byte)(value / 256);
            bytes[0] = (byte)(value % 256);

            WriteBytesAt(address, bytes);
        }

        public Memory(IMemoryMap map)
        {
            _map = map;
        }
    }
}
