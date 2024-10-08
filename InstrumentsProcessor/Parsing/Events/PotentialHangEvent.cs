// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using InstrumentsProcessor.Parsing.DataModels;
using System;
using String = InstrumentsProcessor.Parsing.DataModels.String;

namespace InstrumentsProcessor.Parsing.Events
{
    public class PotentialHangEvent : Event
    {
        public override Microsoft.Performance.SDK.Timestamp Timestamp => StartTime.Value;

        public override Type GetKey()
        {
            return typeof(PotentialHangEvent);
        }

        [Column("Start", "start-time")]
        public Timestamp StartTime { get; set; }

        [Column("Duration", "duration")]
        public TimestampDelta Duration { get; set; }

        [Column("Hang Type", "hang-type")]
        public String HangType { get; set; }

        [Column("Thread", "thread")]
        public Thread Thread { get; set; }

        [Column("Process", "process")]
        public Process Process { get; set; }
    }
}
