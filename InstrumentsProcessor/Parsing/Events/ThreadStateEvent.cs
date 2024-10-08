// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using InstrumentsProcessor.Parsing.DataModels;
using Thread = InstrumentsProcessor.Parsing.DataModels.Thread;
using Process = InstrumentsProcessor.Parsing.DataModels.Process;
using System;
using String = InstrumentsProcessor.Parsing.DataModels.String;

namespace InstrumentsProcessor.Parsing.Events
{
    public class ThreadStateEvent : Event
    {
        public override Microsoft.Performance.SDK.Timestamp Timestamp => StartTime.Value;

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
        public String Core { get; set; }

        [Column("Running Time", "duration-on-core")]
        public TimestampDelta RunningTime { get; set; }

        [Column("Wait Time", "duration-waiting")]
        public TimestampDelta WaitTime { get; set; }

        [Column("Priority", "sched-priority")]
        public Integer Priority { get; set; }

        [Column("Note", "narrative")]
        public String Note { get; set; }

        [Column("Summary", "narrative")]
        public String Summary { get; set; }
    }
}
