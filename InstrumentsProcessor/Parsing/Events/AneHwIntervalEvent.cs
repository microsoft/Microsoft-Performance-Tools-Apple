// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using InstrumentsProcessor.Parsing.DataModels;
using System;
using String = InstrumentsProcessor.Parsing.DataModels.String;

namespace InstrumentsProcessor.Parsing.Events
{
    public class AneHwIntervalEvent : Event
    {
        public override Microsoft.Performance.SDK.Timestamp Timestamp => StartTime.Value;

        public override Type GetKey()
        {
            return typeof(AneHwIntervalEvent);
        }

        [Column("Creation", "start-time")]
        public Timestamp StartTime { get; set; }

        [Column("Duration", "duration")]
        public TimestampDelta Duration { get; set; }

        [Column("Channel Name", "ane-event-name")]
        public String ChannelName { get; set; }

        [Column("Depth", "metal-nesting-level")]
        public Integer Depth { get; set; }

        [Column("Label", "formatted-label")]
        public String Label { get; set; }

        [Column("State", "gpu-state")]
        public String State { get; set; }

        [Column("Color", "render-buffer-depth")]
        public Integer Color { get; set; }
    }
}
