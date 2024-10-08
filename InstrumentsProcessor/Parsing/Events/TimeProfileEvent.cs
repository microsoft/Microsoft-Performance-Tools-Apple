// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using InstrumentsProcessor.Parsing.DataModels;
using System;
using String = InstrumentsProcessor.Parsing.DataModels.String;

namespace InstrumentsProcessor.Parsing.Events
{
    public class TimeProfileEvent : Event
    {
        public override Microsoft.Performance.SDK.Timestamp Timestamp => SampleTime.Value;

        public override Type GetKey()
        {
            return typeof(TimeProfileEvent);
        }

        [Column("Sample Time", "sample-time")]
        public Timestamp SampleTime { get; set; }

        [Column("Thread", "thread")]
        public Thread Thread { get; set; }

        [Column("Process", "process")]
        public Process Process { get; set; }

        [Column("Core", "core")]
        public String Core { get; set; }

        [Column("State", "thread-state")]
        public String ThreadState { get; set; }

        [Column("Weight", "weight")]
        public TimestampDelta Weight { get; set; }

        [Column("Backtrace", "backtrace")]
        public Backtrace Backtrace { get; set; }
    }
}
