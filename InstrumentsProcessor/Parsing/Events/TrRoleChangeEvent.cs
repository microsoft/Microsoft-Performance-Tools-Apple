// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using InstrumentsProcessor.Parsing.DataModels;
using System;
using UInt64 = InstrumentsProcessor.Parsing.DataModels.UInt64;

namespace InstrumentsProcessor.Parsing.Events
{
    public class TrRoleChangeEvent : Event
    {
        public override Microsoft.Performance.SDK.Timestamp Timestamp => StartTime.Value;

        public override Type GetKey()
        {
            return typeof(TrRoleChangeEvent);
        }

        [Column("Start", "start-time")]
        public Timestamp StartTime { get; set; }

        [Column("Duration", "duration", Mnemonic = "duration")]
        public TimestampDelta Duration { get; set; }

        [Column("Layout ID", "layout-id")]
        public Integer LayoutId { get; set; }

        [Column("Caller", "process")]
        public Process Caller { get; set; }

        [Column("Target PID", "pid")]
        public Integer TargetPid { get; set; }

        [Column("New Role", "uint32")]
        public UInt64 NewRole { get; set; }

        [Column("Duration", "duration", Mnemonic = "role-duration")]
        public TimestampDelta RoleDuration { get; set; }
    }
}
