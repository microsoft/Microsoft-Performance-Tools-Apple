// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using InstrumentsProcessor.Parsing.DataModels;
using System;
using String = InstrumentsProcessor.Parsing.DataModels.String;
using PerfSDK = Microsoft.Performance.SDK;

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
        public CPU Core { get; set; }

        [Column("State", "thread-state")]
        public String ThreadState { get; set; }

        [Column("Weight", "weight")]
        public TimestampDelta Weight { get; set; }

        [Column("Backtrace", "backtrace")]
        public Backtrace Backtrace { get; set; }

        public Microsoft.Performance.SDK.Timestamp StartTime => SampleTime.Value - Weight.Value;

        private static readonly String RunningState = new String("Running");

        public static TimeProfileEvent MakeIdleEvent(CPU core, PerfSDK.Timestamp sampleTime, PerfSDK.TimestampDelta weight)
        {
            return new TimeProfileEvent()
            {
                // Note start time is sampletime - weight
                Core = core,
                SampleTime = new Timestamp(sampleTime),
                Weight = new TimestampDelta(weight),
                Process = Process.IdleProcess,
                Thread = Thread.IdleThread,
                ThreadState = RunningState,
            };
        }
    }
}
