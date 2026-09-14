// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using InstrumentsProcessor.Parsing.PcwSnap;
using Microsoft.Performance.SDK;
using System;

namespace InstrumentsProcessor.Parsing.Events
{
    /// <summary>
    /// One row per process per snapshot, carrying disk I/O stats from the
    /// kcdata IoStats (io_stats_snapshot) record inside task containers.
    /// </summary>
    public class PcwSnapDiskEvent : Event
    {
        public PcwSnapDiskEvent(
            int snapshotIndex,
            Timestamp timestamp,
            string processName,
            int pid,
            KcIoStatsSnapshot current,
            KcIoStatsSnapshot previous,
            Timestamp? previousTimestamp = null)
        {
            SnapshotIndex = snapshotIndex;
            SnapshotTimestamp = timestamp;
            ProcessName = processName;
            Pid = pid;
            Current = current;
            Previous = previous;
            PreviousTimestamp = previousTimestamp;
        }

        public override Timestamp Timestamp => SnapshotTimestamp;
        public override Type GetKey() => typeof(PcwSnapDiskEvent);

        public int SnapshotIndex { get; }
        public Timestamp SnapshotTimestamp { get; }
        public string ProcessName { get; }
        public int Pid { get; }
        public KcIoStatsSnapshot Current { get; }
        public KcIoStatsSnapshot Previous { get; }
        public Timestamp? PreviousTimestamp { get; }

        /// <summary>
        /// Interval in seconds between this snapshot and the previous one.
        /// Returns 0 for the first snapshot.
        /// </summary>
        private double DeltaSeconds
        {
            get
            {
                if (PreviousTimestamp == null) return 0;
                double ns = (SnapshotTimestamp - PreviousTimestamp.Value).ToNanoseconds;
                return ns > 0 ? ns / 1_000_000_000.0 : 0;
            }
        }

        // ---- Cumulative values ----
        public long DiskReadsCount => (long)Current.DiskReadsCount;
        public long DiskReadsSize => (long)Current.DiskReadsSize;
        public long DiskWritesCount => (long)Current.DiskWritesCount;
        public long DiskWritesSize => (long)Current.DiskWritesSize;
        public long PagingCount => (long)Current.PagingCount;
        public long PagingSize => (long)Current.PagingSize;
        public long NonPagingCount => (long)Current.NonPagingCount;
        public long NonPagingSize => (long)Current.NonPagingSize;
        public long MetadataCount => (long)Current.MetadataCount;
        public long MetadataSize => (long)Current.MetadataSize;
        public long DataCount => (long)Current.DataCount;
        public long DataSize => (long)Current.DataSize;

        // ---- Deltas ----
        public long DeltaDiskReadsCount => Previous != null ? (long)(Current.DiskReadsCount - Previous.DiskReadsCount) : 0;
        public long DeltaDiskReadsSize => Previous != null ? (long)(Current.DiskReadsSize - Previous.DiskReadsSize) : 0;
        public long DeltaDiskWritesCount => Previous != null ? (long)(Current.DiskWritesCount - Previous.DiskWritesCount) : 0;
        public long DeltaDiskWritesSize => Previous != null ? (long)(Current.DiskWritesSize - Previous.DiskWritesSize) : 0;
        public long DeltaPagingCount => Previous != null ? (long)(Current.PagingCount - Previous.PagingCount) : 0;
        public long DeltaPagingSize => Previous != null ? (long)(Current.PagingSize - Previous.PagingSize) : 0;
        public long DeltaNonPagingCount => Previous != null ? (long)(Current.NonPagingCount - Previous.NonPagingCount) : 0;
        public long DeltaNonPagingSize => Previous != null ? (long)(Current.NonPagingSize - Previous.NonPagingSize) : 0;
        public long DeltaMetadataCount => Previous != null ? (long)(Current.MetadataCount - Previous.MetadataCount) : 0;
        public long DeltaMetadataSize => Previous != null ? (long)(Current.MetadataSize - Previous.MetadataSize) : 0;
        public long DeltaDataCount => Previous != null ? (long)(Current.DataCount - Previous.DataCount) : 0;
        public long DeltaDataSize => Previous != null ? (long)(Current.DataSize - Previous.DataSize) : 0;

        // ---- I/O priority (cumulative) ----
        public long IoPriority0Count => (long)Current.IoPriorityCount[0];
        public long IoPriority0Size => (long)Current.IoPrioritySize[0];
        public long IoPriority1Count => (long)Current.IoPriorityCount[1];
        public long IoPriority1Size => (long)Current.IoPrioritySize[1];
        public long IoPriority2Count => (long)Current.IoPriorityCount[2];
        public long IoPriority2Size => (long)Current.IoPrioritySize[2];
        public long IoPriority3Count => (long)Current.IoPriorityCount[3];
        public long IoPriority3Size => (long)Current.IoPrioritySize[3];

        // ---- I/O priority (deltas) ----
        public long DeltaIoPriority0Count => Previous != null ? (long)(Current.IoPriorityCount[0] - Previous.IoPriorityCount[0]) : 0;
        public long DeltaIoPriority0Size => Previous != null ? (long)(Current.IoPrioritySize[0] - Previous.IoPrioritySize[0]) : 0;
        public long DeltaIoPriority1Count => Previous != null ? (long)(Current.IoPriorityCount[1] - Previous.IoPriorityCount[1]) : 0;
        public long DeltaIoPriority1Size => Previous != null ? (long)(Current.IoPrioritySize[1] - Previous.IoPrioritySize[1]) : 0;
        public long DeltaIoPriority2Count => Previous != null ? (long)(Current.IoPriorityCount[2] - Previous.IoPriorityCount[2]) : 0;
        public long DeltaIoPriority2Size => Previous != null ? (long)(Current.IoPrioritySize[2] - Previous.IoPrioritySize[2]) : 0;
        public long DeltaIoPriority3Count => Previous != null ? (long)(Current.IoPriorityCount[3] - Previous.IoPriorityCount[3]) : 0;
        public long DeltaIoPriority3Size => Previous != null ? (long)(Current.IoPrioritySize[3] - Previous.IoPrioritySize[3]) : 0;

        // ---- Rates ----
        /// <summary>Disk read rate in bytes per second (Δ bytes / Δ time).</summary>
        public double DiskReadRate => DeltaSeconds > 0 ? DeltaDiskReadsSize / DeltaSeconds : 0;
        /// <summary>Disk write rate in bytes per second (Δ bytes / Δ time).</summary>
        public double DiskWriteRate => DeltaSeconds > 0 ? DeltaDiskWritesSize / DeltaSeconds : 0;
        /// <summary>Disk read rate in KB/s.</summary>
        public double DiskReadRateKB => DiskReadRate / 1024.0;
        /// <summary>Disk write rate in KB/s.</summary>
        public double DiskWriteRateKB => DiskWriteRate / 1024.0;
        /// <summary>Disk read rate in MB/s.</summary>
        public double DiskReadRateMB => DiskReadRate / (1024.0 * 1024.0);
        /// <summary>Disk write rate in MB/s.</summary>
        public double DiskWriteRateMB => DiskWriteRate / (1024.0 * 1024.0);
    }
}
