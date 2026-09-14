// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using InstrumentsProcessor.Parsing.DataModels;
using System;
using String = InstrumentsProcessor.Parsing.DataModels.String;
using UInt64 = InstrumentsProcessor.Parsing.DataModels.UInt64;

namespace InstrumentsProcessor.Parsing.Events
{
    /// <summary>
    /// Corresponds to the "dil-disk-io" schema emitted by the Instruments
    /// Disk I/O instrument.  Each row represents a single completed I/O operation.
    /// </summary>
    public class DiskIoEvent : Event
    {
        public override Microsoft.Performance.SDK.Timestamp Timestamp => StartTime?.Value ?? default;

        public override Type GetKey()
        {
            return typeof(DiskIoEvent);
        }

        [Column("Start", "start-time")]
        public Timestamp StartTime { get; set; }

        [Column("Queue Depth", "uint32", Mnemonic = "queue-depth")]
        public UInt64 QueueDepth { get; set; }

        [Column("Latency", "duration")]
        public TimestampDelta Latency { get; set; }

        [Column("Process", "process")]
        public Process Process { get; set; }

        [Column("Thread", "thread")]
        public Thread Thread { get; set; }

        [Column("Operation", "string", Mnemonic = "operation")]
        public String Operation { get; set; }

        [Column("Sync", "string", Mnemonic = "sync-mode")]
        public String SyncMode { get; set; }

        [Column("Tier", "uint32", Mnemonic = "tier")]
        public UInt64 Tier { get; set; }

        [Column("Flags", "string", Mnemonic = "flags")]
        public String Flags { get; set; }

        [Column("Size", "size-in-bytes", Mnemonic = "size")]
        public Integer Size { get; set; }

        [Column("Throughput (B/s)", "uint64", Mnemonic = "throughput")]
        public UInt64 Throughput { get; set; }

        [Column("Block", "uint64", Mnemonic = "block-number")]
        public UInt64 BlockNumber { get; set; }

        [Column("Device", "string", Mnemonic = "device")]
        public String Device { get; set; }

        [Column("buf_t", "address")]
        public String BufT { get; set; }

        [Column("Error", "errno-value")]
        public String Error { get; set; }

        [Column("Resid", "size-in-bytes", Mnemonic = "resid")]
        public Integer Resid { get; set; }
    }
}
