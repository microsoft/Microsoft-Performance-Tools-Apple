// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using InstrumentsProcessor.Parsing.DataModels;
using System;
using String = InstrumentsProcessor.Parsing.DataModels.String;

namespace InstrumentsProcessor.Parsing.Events
{
    public class SyscallNameMapEvent : Event
    {
        public override Microsoft.Performance.SDK.Timestamp Timestamp => TimeStamp.Value;

        public override Type GetKey()
        {
            return typeof(SyscallNameMapEvent);
        }

        [Column("Timestamp", "event-time")]
        public Timestamp TimeStamp { get; set; }

        [Column("Name", "syscall")]
        public String Name { get; set; }

        [Column("Class", "kdebug-class")]
        public String Class { get; set; }

        [Column("Subclass", "kdebug-subclass")]
        public String Subclass { get; set; }

        [Column("Code", "kdebug-code")]
        public Integer Code { get; set; }
    }
}
