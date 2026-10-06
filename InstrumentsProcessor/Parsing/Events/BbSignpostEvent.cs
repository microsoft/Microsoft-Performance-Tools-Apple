// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using InstrumentsProcessor.Parsing.DataModels;
using System;
using String = InstrumentsProcessor.Parsing.DataModels.String;

namespace InstrumentsProcessor.Parsing.Events
{
    /// <summary>
    /// Represents a "bb-signpost-event" (Browser Benchmark signpost) from
    /// the bb-signpost instrument. Each event carries a metric-info string
    /// describing a browser measurement such as TabSwitchPaint or WindowRestore.
    /// </summary>
    public class BbSignpostEvent : Event
    {
        public override Microsoft.Performance.SDK.Timestamp Timestamp => Time?.Value ?? default;

        public override Type GetKey()
        {
            return typeof(BbSignpostEvent);
        }

        [Column("Time", "event-time")]
        public Timestamp Time { get; set; }

        [Column("Metric", "string")]
        public String Metric { get; set; }

        [Column("Thread ID", "thread")]
        public Thread Thread { get; set; }
    }
}
