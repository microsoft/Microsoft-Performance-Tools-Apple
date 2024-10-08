// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using InstrumentsProcessor.Parsing.DataModels;
using System;
using String = InstrumentsProcessor.Parsing.DataModels.String;

namespace InstrumentsProcessor.Parsing.Events
{
    public class SyscallEvent : Event
    {
        public override Microsoft.Performance.SDK.Timestamp Timestamp => StartTime.Value;

        public override Type GetKey()
        {
            return typeof(SyscallEvent);
        }

        [Column("Start Time", "start-time")]
        public Timestamp StartTime { get; set; }

        [Column("Duration", "duration")]
        public TimestampDelta Duration { get; set; }

        [Column("Thread", "thread")]
        public Thread Thread { get; set; }

        [Column("Call", "syscall")]
        public String Call { get; set; }

        [Column("Process", "process")]
        public Process Process { get; set; }

        [Column("CPU Time", "duration-on-core")]
        public TimestampDelta CPUTime { get; set; }

        [Column("Wait Time", "duration-waiting")]
        public TimestampDelta WaitTime { get; set; }

        [Column("Arg1", "syscall-arg")]
        public String SysCallArg1 { get; set; }

        [Column("Arg2", "syscall-arg")]
        public String SysCallArg2 { get; set; }

        [Column("Arg3", "syscall-arg")]
        public String SysCallArg3 { get; set; }

        [Column("Arg4", "syscall-arg")]
        public String SysCallArg4 { get; set; }

        [Column("Return", "syscall-return")]
        public String Return { get; set; }

        [Column("errno", "syscall-return")]
        public String Errno { get; set; }

        [Column("Stack", "backtrace")]
        public Backtrace Stack { get; set; }

        [Column("Note", "narrative")]
        public String Note { get; set; }

        [Column("Signature", "formatted-label")]
        public String Signature { get; set; }
    }
}
