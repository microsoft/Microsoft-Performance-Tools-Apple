// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using InstrumentsProcessor.Parsing.DataModels;
using System;
using UInt64 = InstrumentsProcessor.Parsing.DataModels.UInt64;

namespace InstrumentsProcessor.Parsing.Events
{
    public class ActivityMonitorSystemEvent : Event
    {
        public override Microsoft.Performance.SDK.Timestamp Timestamp => StartTime.Value;

        public override Type GetKey() => typeof(ActivityMonitorSystemEvent);

        [Column("Start Time", "start-time")]
        public Timestamp StartTime { get; set; }

        [Column("Duration", "duration")]
        public TimestampDelta Duration { get; set; }

        [Column("System Load %", "percent", Mnemonic = "cpu-system-load")]
        public UInt64 CpuSystemLoad { get; set; }

        [Column("Total Load %", "percent", Mnemonic = "cpu-total-load")]
        public UInt64 CpuTotalLoad { get; set; }

        [Column("User Load %", "percent", Mnemonic = "cpu-user-load")]
        public UInt64 CpuUserLoad { get; set; }

        [Column("Data Read", "size-in-bytes", Mnemonic = "disk-bytes-read")]
        public UInt64 DiskBytesRead { get; set; }

        [Column("Data Read/sec", "bytes-per-second", Mnemonic = "disk-bytes-read-per-second")]
        public UInt64 DiskBytesReadPerSecond { get; set; }

        [Column("Data Written", "size-in-bytes", Mnemonic = "disk-bytes-written")]
        public UInt64 DiskBytesWritten { get; set; }

        [Column("Data Written/sec", "bytes-per-second", Mnemonic = "disk-bytes-written-per-second")]
        public UInt64 DiskBytesWrittenPerSecond { get; set; }

        [Column("Reads In", "uint32", Mnemonic = "disk-read-ops")]
        public UInt64 DiskReadOps { get; set; }

        [Column("Reads In/sec", "uint64", Mnemonic = "disk-read-ops-per-second")]
        public UInt64 DiskReadOpsPerSecond { get; set; }

        [Column("Writes Out", "uint32", Mnemonic = "disk-write-ops")]
        public UInt64 DiskWriteOps { get; set; }

        [Column("Writes Out/sec", "uint64", Mnemonic = "disk-write-ops-per-second")]
        public UInt64 DiskWriteOpsPerSecond { get; set; }

        [Column("App Memory", "size-in-bytes", Mnemonic = "memory-app")]
        public UInt64 MemoryApp { get; set; }

        [Column("Cached Files", "size-in-bytes", Mnemonic = "memory-cached-files")]
        public UInt64 MemoryCachedFiles { get; set; }

        [Column("Compressed", "size-in-bytes", Mnemonic = "memory-compressed")]
        public UInt64 MemoryCompressed { get; set; }

        [Column("Memory Used", "size-in-bytes", Mnemonic = "memory-physical-used")]
        public UInt64 MemoryPhysicalUsed { get; set; }

        [Column("Wired Memory", "size-in-bytes", Mnemonic = "memory-wired")]
        public UInt64 MemoryWired { get; set; }

        [Column("Data Received", "size-in-bytes", Mnemonic = "net-bytes-in")]
        public UInt64 NetBytesIn { get; set; }

        [Column("Data Received/sec", "bytes-per-second", Mnemonic = "net-bytes-in-per-second")]
        public UInt64 NetBytesInPerSecond { get; set; }

        [Column("Data Sent", "size-in-bytes", Mnemonic = "net-bytes-out")]
        public UInt64 NetBytesOut { get; set; }

        [Column("Data Sent/sec", "bytes-per-second", Mnemonic = "net-bytes-out-per-second")]
        public UInt64 NetBytesOutPerSecond { get; set; }

        [Column("Packets In", "uint32", Mnemonic = "net-packets-in")]
        public UInt64 NetPacketsIn { get; set; }

        [Column("Packets In/sec", "uint64", Mnemonic = "net-packets-in-per-second")]
        public UInt64 NetPacketsInPerSecond { get; set; }

        [Column("Packets Out", "uint32", Mnemonic = "net-packets-out")]
        public UInt64 NetPacketsOut { get; set; }

        [Column("Packets Out/sec", "uint64", Mnemonic = "net-packets-out-per-second")]
        public UInt64 NetPacketsOutPerSecond { get; set; }

        [Column("Processes", "uint32", Mnemonic = "total-processes")]
        public UInt64 TotalProcesses { get; set; }

        [Column("Threads", "uint32", Mnemonic = "total-threads")]
        public UInt64 TotalThreads { get; set; }

        [Column("Swap Used", "size-in-bytes", Mnemonic = "vm-swap-used")]
        public UInt64 VmSwapUsed { get; set; }
    }
}
