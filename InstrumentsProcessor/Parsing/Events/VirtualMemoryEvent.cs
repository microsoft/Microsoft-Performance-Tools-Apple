// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using InstrumentsProcessor.Parsing.DataModels;
using System;
using String = InstrumentsProcessor.Parsing.DataModels.String;

namespace InstrumentsProcessor.Parsing.Events
{
    public class VirtualMemoryEvent : Event
    {
        public override Microsoft.Performance.SDK.Timestamp Timestamp => StartTime.Value;

        public override Type GetKey()
        {
            return typeof(VirtualMemoryEvent);
        }

        [Column("Start Time", "start-time")]
        public Timestamp StartTime { get; set; }

        [Column("Duration", "duration")]
        public TimestampDelta Duration { get; set; }

        [Column("Thread", "thread")]
        public Thread Thread { get; set; }

        [Column("Operation", "vm-op")]
        public String Operation { get; set; }

        [Column("Process", "process")]
        public Process Process { get; set; }

        [Column("CPU Time", "duration-on-core")]
        public TimestampDelta CPUTime { get; set; }

        [Column("Wait Time", "duration-waiting")]
        public TimestampDelta WaitTime { get; set; }

        [Column("Address", "address")]
        public String Address { get; set; }

        [Column("Size", "size-in-bytes")]
        public Integer Size { get; set; }

        [Column("Stack", "backtrace")]
        public Backtrace Stack { get; set; }
    }
}
