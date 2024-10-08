// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using InstrumentsProcessor.Parsing.DataModels;
using System;
using String = InstrumentsProcessor.Parsing.DataModels.String;

namespace InstrumentsProcessor.Parsing.Events
{
    public class DisplayVsyncIntervalEvent : Event
    {
        public override Microsoft.Performance.SDK.Timestamp Timestamp => TimeStamp.Value;

        public override Type GetKey()
        {
            return typeof(DisplayVsyncIntervalEvent);
        }

        [Column("Timestamp", "start-time")]
        public Timestamp TimeStamp { get; set; }

        [Column("Duration", "duration")]
        public TimestampDelta Duration { get; set; }

        [Column("Display Name", "display-name")]
        public String DisplayName { get; set; }

        [Column("Color", "render-buffer-depth")]
        public Integer Color { get; set; }

        [Column("Label", "narrative")]
        public String Label { get; set; }

        [Column("Event", "vsync-event")]
        public String Event { get; set; }
    }
}
