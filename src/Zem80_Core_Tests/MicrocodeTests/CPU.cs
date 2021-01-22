﻿using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;

namespace Zem80.Core.Tests.MicrocodeTests
{
    [TestFixture]
    public class CPU : MicrocodeTestBase
    {
        [Test]
        public void EX_AF_AF()
        {
            CPU.Registers.Direct[WordRegister.AF] = 0x80;
            CPU.Registers.ExchangeAF();
            CPU.Registers.Direct[WordRegister.AF] = 0x90;
            CPU.Registers.ExchangeAF();

            ExecuteInstruction("EX AF,AF'");
            Assert.That(CPU.Registers.AF == 0x90);

            ExecuteInstruction("EX AF,AF'");
            Assert.That(CPU.Registers.AF == 0x80);
        }

        [TestCase(WordRegister.HL)]
        [TestCase(WordRegister.IX)]
        [TestCase(WordRegister.IY)]
        public void EX_xSP_rr(WordRegister wordRegister)
        {
            ushort sp = 0x4000;
            ushort value = 0x8020;
            ushort valueAtSP = 0x2080;

            Registers.SP = sp;
            CPU.Memory.Untimed.WriteWordAt(sp, valueAtSP);
            Registers.Direct[wordRegister] = value;

            ExecuteInstruction($"EX (SP),{ wordRegister.ToString() }");
            ushort newValueAtSP = CPU.Memory.Untimed.ReadWordAt(sp);

            Assert.That(Registers.Direct[wordRegister] == valueAtSP && newValueAtSP == value);
        }
    }
}
