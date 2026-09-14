// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using InstrumentsProcessor.Parsing.DataModels;
using System;
using UInt64 = InstrumentsProcessor.Parsing.DataModels.UInt64;

namespace InstrumentsProcessor.Parsing.Events
{
    // Corresponds to the "vml-vm-fault" schema emitted by the Virtual Memory
    // Log instrument. Two of the schema's columns share the display name
    // "Duration" (mnemonics "duration" and "fault-duration"); the Mnemonic
    // attribute is used to disambiguate them.
    public class VmFaultEvent : Event
    {
        public override Microsoft.Performance.SDK.Timestamp Timestamp => StartTime?.Value ?? default;

        public override Type GetKey()
        {
            return typeof(VmFaultEvent);
        }

        [Column("Start", "start-time")]
        public Timestamp StartTime { get; set; }

        [Column("Duration", "duration", Mnemonic = "duration")]
        public TimestampDelta Duration { get; set; }

        [Column("Layout ID", "layout-id")]
        public Integer LayoutId { get; set; }

        [Column("Process", "process")]
        public Process Process { get; set; }

        [Column("Thread", "thread")]
        public Thread Thread { get; set; }

        [Column("Operation", "uint32")]
        public UInt64 Operation { get; set; }

        [Column("Duration", "duration", Mnemonic = "fault-duration")]
        public TimestampDelta FaultDuration { get; set; }

        [Column("Size", "uint64")]
        public UInt64 Size { get; set; }
    }
}
