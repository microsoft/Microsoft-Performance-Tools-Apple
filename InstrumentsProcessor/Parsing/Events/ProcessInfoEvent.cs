// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using InstrumentsProcessor.Parsing.DataModels;
using System;
using String = InstrumentsProcessor.Parsing.DataModels.String;
using UInt64 = InstrumentsProcessor.Parsing.DataModels.UInt64;

namespace InstrumentsProcessor.Parsing.Events
{
    public class ProcessInfoEvent : Event
    {
        public override Microsoft.Performance.SDK.Timestamp Timestamp => TimeStamp.Value;

        public override Type GetKey()
        {
            return typeof(ProcessInfoEvent);
        }

        [Column("Timestamp", "event-time")]
        public Timestamp TimeStamp { get; set; }

        [Column("Process ID", "pid")]
        public Integer ProcessId { get; set; }

        [Column("Unique Process ID", "uint64")]
        public UInt64 UniqueProcessId { get; set; }

        [Column("Process", "process")]
        public Process Process { get; set; }

        [Column("Process Name", "string")]
        public String ProcessName { get; set; }
    }
}
