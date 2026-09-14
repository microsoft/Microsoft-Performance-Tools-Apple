// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using InstrumentsProcessor.Parsing.DataModels;
using System;
using UInt64 = InstrumentsProcessor.Parsing.DataModels.UInt64;

namespace InstrumentsProcessor.Parsing.Events
{
    public class CswitchIntervalEvent : Event
    {
        public override Microsoft.Performance.SDK.Timestamp Timestamp => StartTime.Value;

        public override Type GetKey()
        {
            return typeof(CswitchIntervalEvent);
        }

        [Column("Start", "start-time")]
        public Timestamp StartTime { get; set; }

        [Column("Duration", "duration")]
        public TimestampDelta Duration { get; set; }

        [Column("Layout ID", "layout-id")]
        public Integer LayoutId { get; set; }

        [Column("Process", "process")]
        public Process Process { get; set; }

        [Column("Thread", "thread")]
        public Thread Thread { get; set; }

        [Column("CPU", "uint32")]
        public UInt64 Cpu { get; set; }

        [Column("Δ Instructions", "uint64")]
        public UInt64 DeltaInstructions { get; set; }

        [Column("Δ Cycles", "uint64")]
        public UInt64 DeltaCycles { get; set; }

        [Column("Δ Time", "duration")]
        public TimestampDelta DeltaTime { get; set; }

        [Column("Switch-In Stack", "backtrace")]
        public Backtrace SwitchInStack { get; set; }

        [Column("Switch-In Kernel Stack", "backtrace")]
        public Backtrace SwitchInKernelStack { get; set; }
    }
}
