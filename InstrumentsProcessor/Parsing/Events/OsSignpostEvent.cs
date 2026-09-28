// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using InstrumentsProcessor.Parsing.DataModels;
using System;
using String = InstrumentsProcessor.Parsing.DataModels.String;

namespace InstrumentsProcessor.Parsing.Events
{
    public class OsSignpostEvent : Event
    {
        public override Microsoft.Performance.SDK.Timestamp Timestamp => Time?.Value ?? default;

        public override Type GetKey()
        {
            return typeof(OsSignpostEvent);
        }

        [Column("Timestamp", "event-time")]
        public Timestamp Time { get; set; }

        [Column("Thread", "thread")]
        public Thread Thread { get; set; }

        [Column("Process", "process")]
        public Process Process { get; set; }

        [Column("Event Type", "event-type")]
        public String EventType { get; set; }

        [Column("Scope", "string")]
        public String Scope { get; set; }

        [Column("Signpost identifier", "os-signpost-identifier")]
        public Integer SignpostId { get; set; }

        [Column("Name", "signpost-name")]
        public String SignpostName { get; set; }

        [Column("Format String", "format-string")]
        public String FormatString { get; set; }

        [Column("Backtrace", "text-backtrace")]
        public String TextBacktrace { get; set; }

        [Column("Subsystem", "subsystem")]
        public String Subsystem { get; set; }

        [Column("Category", "category")]
        public String Category { get; set; }

        [Column("Message", "os-log-metadata")]
        public OsLogMetadata Metadata { get; set; }

        [Column("Emit Location", "return-location")]
        public String EmitLocation { get; set; }

        /// <summary>
        /// Timestamp from the paired End event, set by the cooker.
        /// </summary>
        public Microsoft.Performance.SDK.Timestamp? EndTime { get; set; }

        /// <summary>
        /// Timestamp from the paired Begin event, set by the cooker.
        /// </summary>
        public Microsoft.Performance.SDK.Timestamp? BeginTime { get; set; }
    }
}
