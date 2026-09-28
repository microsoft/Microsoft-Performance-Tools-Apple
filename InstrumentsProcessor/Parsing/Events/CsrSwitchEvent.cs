// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using InstrumentsProcessor.Parsing.DataModels;
using System;
using UInt64 = InstrumentsProcessor.Parsing.DataModels.UInt64;

namespace InstrumentsProcessor.Parsing.Events
{
    /// <summary>
    /// Represents a raw csr-switch-on or csr-switch-off point event.
    /// Both schemas share the same columns; use <see cref="Event.SchemaName"/>
    /// ("csr-switch-on" / "csr-switch-off") to distinguish them.
    /// </summary>
    public class CsrSwitchEvent : Event
    {
        public override Microsoft.Performance.SDK.Timestamp Timestamp => Time?.Value ?? default;

        public override Type GetKey()
        {
            return typeof(CsrSwitchEvent);
        }

        [Column("Time", "event-time")]
        public Timestamp Time { get; set; }

        [Column("Process", "process")]
        public Process Process { get; set; }

        [Column("Thread", "thread")]
        public Thread Thread { get; set; }

        [Column("CPU", "uint32")]
        public UInt64 Cpu { get; set; }

        [Column("Instructions (Fixed)", "uint64", Mnemonic = "instructions")]
        public UInt64 Instructions { get; set; }

        [Column("Cycles (Fixed)", "uint64", Mnemonic = "cycles")]
        public UInt64 Cycles { get; set; }

        public bool IsSwitchOn => SchemaName == "csr-switch-on";
        public bool IsSwitchOff => SchemaName == "csr-switch-off";
    }
}
