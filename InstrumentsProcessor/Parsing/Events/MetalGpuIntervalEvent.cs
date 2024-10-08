// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using InstrumentsProcessor.Parsing.DataModels;
using System;
using String = InstrumentsProcessor.Parsing.DataModels.String;
using UInt64 = InstrumentsProcessor.Parsing.DataModels.UInt64;

namespace InstrumentsProcessor.Parsing.Events
{
    public class MetalGpuIntervalEvent : Event
    {
        public override Microsoft.Performance.SDK.Timestamp Timestamp => StartTime.Value;

        public override Type GetKey()
        {
            return typeof(MetalGpuIntervalEvent);
        }

        [Column("Creation", "start-time")]
        public Timestamp StartTime { get; set; }

        [Column("Duration", "duration")]
        public TimestampDelta Duration { get; set; }

        [Column("Channel Name", "gpu-channel-name")]
        public String ChannelName { get; set; }

        [Column("Frame", "gpu-frame-number")]
        public Integer Frame { get; set; }

        [Column("CPU to GPU Latency", "duration")]
        public TimestampDelta CpuToGpuLatency { get; set; }

        [Column("Depth", "metal-nesting-level")]
        public Integer Depth { get; set; }

        [Column("Label", "formatted-label")]
        public String Label { get; set; }

        [Column("State", "gpu-state")]
        public String State { get; set; }

        [Column("Connection UUID", "connection-uuid64")]
        public String ConnectionUUID { get; set; }

        [Column("Color", "render-buffer-depth")]
        public Integer Color { get; set; }

        [Column("Process", "process")]
        public Process Process { get; set; }

        [Column("Metal Device", "metal-device-name")]
        public String MetalDevice { get; set; }

        [Column("Channel Subtitle", "metal-object-label")]
        public String ChannnelSubtitle { get; set; }

        [Column("IOSurface Accesses", "formatted-label")]
        public String IOSurfaceAccesses { get; set; }

        [Column("Bytes", "size-in-bytes")]
        public Integer Bytes { get; set; }

        [Column("Commmand Buffer Id", "metal-command-buffer-id")]
        public UInt64 CommandBufferId { get; set; }

        [Column("Encoder Id", "metal-command-buffer-id")]
        public UInt64 EncoderID { get; set; }

        [Column("GPU Submission Id", "uint64")]
        public UInt64 GpuSubmissionId { get; set; }
    }
}
