// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using InstrumentsProcessor.Parsing.DataModels;
using System;
using String = InstrumentsProcessor.Parsing.DataModels.String;

namespace InstrumentsProcessor.Parsing.Events
{
    public class LifeCyclePeriodEvent : Event
    {
        public override Microsoft.Performance.SDK.Timestamp Timestamp => Start.Value;

        public override Type GetKey()
        {
            return typeof(LifeCyclePeriodEvent);
        }

        [Column("Start", "start-time")]
        public Timestamp Start { get; set; }

        [Column("Group", "string")]
        public String Group { get; set; }

        [Column("Layout ID", "layout-id")]
        public Integer LayoutId { get; set; }

        [Column("Duration", "duration")]
        public TimestampDelta Duration { get; set; }

        [Column("process", "process")]
        public Process Process { get; set; }

        [Column("Lifecycle Period", "app-period")]
        public String LifecyclePeriod { get; set; }

        [Column("Narrative", "narrative")]
        public String Narrative { get; set; }
    }
}
