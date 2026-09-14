// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using InstrumentsProcessor.Parsing.PcwSnap;
using Microsoft.Performance.SDK;
using System;

namespace InstrumentsProcessor.Parsing.Events
{
    /// <summary>
    /// Wraps a single <see cref="Snapshot"/> from a pcwsnap trace as an SDK event.
    /// </summary>
    public class PcwSnapSnapshotEvent : Event
    {
        public PcwSnapSnapshotEvent(int index, Snapshot snapshot)
        {
            Index = index;
            Snapshot = snapshot;
        }

        public override Timestamp Timestamp => new Timestamp((long)Snapshot.TimestampNs);

        public override Type GetKey() => typeof(PcwSnapSnapshotEvent);

        /// <summary>Zero-based snapshot ordinal within the trace.</summary>
        public int Index { get; }

        /// <summary>The underlying parsed snapshot data.</summary>
        public Snapshot Snapshot { get; }

        // ----- convenience accessors -----

        public int ProcessCount => Snapshot.ProcInfo?.EntryCount > 0 ? (int)Snapshot.ProcInfo.EntryCount : 0;

        public int KcdataSizeBytes => Snapshot.KcdataBlob?.Length ?? 0;

        public bool HasPowerData => Snapshot.Power != null;

        public bool HasMarkerData => Snapshot.Marker != null;

        public bool HasFreqData => Snapshot.FreqDelta != null;

        public int FreqChannelCount => Snapshot.FreqDelta?.ChannelCount > 0 ? (int)Snapshot.FreqDelta.ChannelCount : 0;
    }
}
