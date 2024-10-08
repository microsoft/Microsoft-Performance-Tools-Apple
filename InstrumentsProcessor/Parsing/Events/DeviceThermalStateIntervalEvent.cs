// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using InstrumentsProcessor.Parsing.DataModels;
using System;
using Boolean = InstrumentsProcessor.Parsing.DataModels.Boolean;
using String = InstrumentsProcessor.Parsing.DataModels.String;

namespace InstrumentsProcessor.Parsing.Events
{
    public class DeviceThermalStateIntervalEvent : Event
    {
        public override Microsoft.Performance.SDK.Timestamp Timestamp => Start.Value;

        public override Type GetKey()
        {
            return typeof(DeviceThermalStateIntervalEvent);
        }

        [Column("Start", "start-time")]
        public Timestamp Start { get; set; }

        [Column("Duration", "duration")]
        public TimestampDelta Duration { get; set; }

        [Column("End", "start-time")]
        public Timestamp End { get; set; }

        [Column("Thermal State", "thermal-state")]
        public String ThermalState { get; set; }

        [Column("Track", "string")]
        public String Track { get; set; }

        [Column("Is Induced", "boolean")]
        public Boolean IsInduced { get; set; }

        [Column("Narrative", "narrative")]
        public String Narrative { get; set; }
    }
}
