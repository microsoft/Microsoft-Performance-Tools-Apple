// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.Performance.SDK;

namespace InstrumentsProcessor.Cookers
{
    internal class IdleTimeTracker
    {
        private Timestamp[] CpuTimes = new Timestamp[10];

        public TimestampDelta Advance(int coreId, Timestamp nextStart, TimestampDelta duration)
        {
            TimestampDelta gap = TimestampDelta.Zero;

            if (coreId >= 0)
            {
                if (CpuTimes.Length <= coreId)
                {
                    Array.Resize(ref CpuTimes, coreId + 1);
                }

                Timestamp last = CpuTimes[coreId];
                if (last != Timestamp.Zero)
                {
                    // ThreadState should not, but TimeProfile does have overlapping events
                    // so we cannot in general make this assertion.
                    // Debug.Assert(nextStart >= last, "Overlapping threads on same processor");
                    if (nextStart > last)
                    {
                        gap = nextStart - last;
                    }
                }

                CpuTimes[coreId] = nextStart + duration;
            }

            return gap;
        }
    }
}
