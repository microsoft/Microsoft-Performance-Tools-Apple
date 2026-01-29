// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using InstrumentsProcessor.Parsing.DataModels;
using Thread = InstrumentsProcessor.Parsing.DataModels.Thread;
using Process = InstrumentsProcessor.Parsing.DataModels.Process;
using System;
using String = InstrumentsProcessor.Parsing.DataModels.String;
using PerfSDK = Microsoft.Performance.SDK;

namespace InstrumentsProcessor.Parsing.Events
{
    public class ThreadStateEvent : Event
    {
        public override PerfSDK.Timestamp Timestamp => StartTime.Value;

        public override Type GetKey()
        {
            return typeof(ThreadStateEvent);
        }

        [Column("Start Time", "start-time")]
        public Timestamp StartTime { get; set; }

        [Column("Duration", "duration")]
        public TimestampDelta Duration { get; set; }

        [Column("Thread", "thread")]
        public Thread Thread { get; set; }

        [Column("State", "thread-state")]
        public String State { get; set; }

        [Column("Process", "process")]
        public Process Process { get; set; }

        [Column("Core", "core")]
        public CPU Core { get; set; }

        [Column("Running Time", "duration-on-core")]
        public TimestampDelta RunningTime { get; set; }

        // Computed
        public TimestampDelta Ready { get; set; }

        [Column("Wait Time", "duration-waiting")]
        public TimestampDelta WaitTime { get; set; }

        // Computed, duration-waiting is never present
        public TimestampDelta Waiting { get; set; }

        [Column("Priority", "sched-priority")]
        public Integer Priority { get; set; }

        [Column("Note", "narrative")]
        public String Note { get; set; }

        [Column("Summary", "narrative")]
        public String Summary { get; set; }

        private static readonly String RunningState = new String("Running");

        public static ThreadStateEvent MakeIdleEvent(CPU core, PerfSDK.Timestamp start, PerfSDK.TimestampDelta duration)
        {
            return new ThreadStateEvent()
            {
                Core = core,
                StartTime = new Timestamp(start),
                Duration = new TimestampDelta(duration),
                Process = Process.IdleProcess,
                Thread = Thread.IdleThread,
                State = RunningState
            };
        }
    }
}
