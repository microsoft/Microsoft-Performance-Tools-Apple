// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using InstrumentsProcessor.Parsing.DataModels;
using System;
using Boolean = InstrumentsProcessor.Parsing.DataModels.Boolean;
using String = InstrumentsProcessor.Parsing.DataModels.String;
using UInt64 = InstrumentsProcessor.Parsing.DataModels.UInt64;

namespace InstrumentsProcessor.Parsing.Events
{
    public class ActivityMonitorProcessEvent : Event
    {
        public override Microsoft.Performance.SDK.Timestamp Timestamp => StartTime.Value;

        public override Type GetKey() => typeof(ActivityMonitorProcessEvent);

        [Column("Start Time", "start-time")]
        public Timestamp StartTime { get; set; }

        [Column("Duration", "duration")]
        public TimestampDelta Duration { get; set; }

        [Column("Process Name", "process")]
        public Process Process { get; set; }

        [Column("Responsible Process", "process")]
        public Process ResponsibleProcess { get; set; }

        [Column("Process ID", "pid", Mnemonic = "pid")]
        public UInt64 ProcessId { get; set; }

        [Column("User Name", "uid", Mnemonic = "uid")]
        public UInt64 UserId { get; set; }

        [Column("% CPU", "cpu-percent", Mnemonic = "cpu-percent")]
        public UInt64 CpuPercent { get; set; }

        [Column("CPU Time", "cpu-total", Mnemonic = "cpu-total")]
        public TimestampDelta CpuTime { get; set; }

        [Column("# Threads", "uint32", Mnemonic = "thread-count")]
        public UInt64 ThreadCount { get; set; }

        [Column("# Ports", "uint32", Mnemonic = "mach-port-count")]
        public UInt64 MachPortCount { get; set; }

        [Column("Memory", "size-in-bytes", Mnemonic = "memory-physical-footprint")]
        public UInt64 MemoryPhysicalFootprint { get; set; }

        [Column("Real Mem", "size-in-bytes", Mnemonic = "memory-real")]
        public UInt64 MemoryReal { get; set; }

        [Column("Real Private Mem", "size-in-bytes", Mnemonic = "memory-real-private")]
        public UInt64 MemoryRealPrivate { get; set; }

        [Column("Real Shared Mem", "size-in-bytes", Mnemonic = "memory-real-shared")]
        public UInt64 MemoryRealShared { get; set; }

        [Column("Kind", "string", Mnemonic = "arch-kind")]
        public String ArchKind { get; set; }

        [Column("Sudden Termination", "boolean", Mnemonic = "sudden-termination")]
        public Boolean SuddenTermination { get; set; }

        [Column("Sandbox", "boolean", Mnemonic = "sandbox")]
        public Boolean Sandbox { get; set; }

        [Column("Restricted", "boolean", Mnemonic = "restricted")]
        public Boolean Restricted { get; set; }

        [Column("Idle Wake Ups", "uint32", Mnemonic = "idle-wakeups")]
        public UInt64 IdleWakeups { get; set; }

        [Column("App Nap", "boolean", Mnemonic = "app-nap")]
        public Boolean AppNap { get; set; }

        [Column("Purgeable Mem", "size-in-bytes", Mnemonic = "memory-purgeable")]
        public UInt64 MemoryPurgeable { get; set; }

        [Column("Compressed Mem", "size-in-bytes", Mnemonic = "memory-compressed")]
        public UInt64 MemoryCompressed { get; set; }

        [Column("Disk Writes", "size-in-bytes", Mnemonic = "disk-bytes-written")]
        public UInt64 DiskBytesWritten { get; set; }

        [Column("Disk Reads", "size-in-bytes", Mnemonic = "disk-bytes-read")]
        public UInt64 DiskBytesRead { get; set; }

        [Column("Data Written/sec", "bytes-per-second", Mnemonic = "disk-bytes-written-per-second")]
        public UInt64 DiskBytesWrittenPerSecond { get; set; }

        [Column("Data Read/sec", "bytes-per-second", Mnemonic = "disk-bytes-read-per-second")]
        public UInt64 DiskBytesReadPerSecond { get; set; }

        [Column("Preventing Sleep", "boolean", Mnemonic = "preventing-sleep")]
        public Boolean PreventingSleep { get; set; }
    }
}
