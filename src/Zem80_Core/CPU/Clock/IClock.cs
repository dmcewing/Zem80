﻿using System;

namespace Zem80.Core.CPU
{
    public interface IClock
    {
        float FrequencyInMHz { get; }
        long Ticks { get; }

        event EventHandler<long> OnTick;

        void Initialise(Processor cpu);
        void Start();
        void Stop();

        void WaitForNextClockTick();
        void WaitForClockTicks(int ticks);

    }
}
