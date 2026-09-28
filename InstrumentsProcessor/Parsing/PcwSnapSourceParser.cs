// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Performance.SDK.Extensibility.SourceParsing;
using Microsoft.Performance.SDK.Processing;
using System.Collections.Generic;
using System;
using System.Threading;
using Microsoft.Performance.SDK;
using InstrumentsProcessor.Parsing.Events;
using InstrumentsProcessor.Parsing.PcwSnap;

namespace InstrumentsProcessor.Parsing
{
    public sealed class PcwSnapSourceParser
        : SourceParser<Event, ParsingContext, Type>
    {
        private ParsingContext context;
        private IEnumerable<IDataSource> dataSources;
        private DataSourceInfo dataSourceInfo;

        public PcwSnapFile PcwSnapData { get; private set; }

        public PcwSnapSourceParser(IEnumerable<IDataSource> dataSources)
        {
            context = new ParsingContext();
            this.dataSources = dataSources;
        }

        public override string Id => nameof(PcwSnapSourceParser);

        public override DataSourceInfo DataSourceInfo => this.dataSourceInfo;

        public override void ProcessSource(
            ISourceDataProcessor<Event, ParsingContext, Type> dataProcessor,
            ILogger logger,
            IProgress<int> progress,
            CancellationToken cancellationToken)
        {
            Timestamp? firstEventTimestamp = null;
            Timestamp? lastEventTimestamp = null;
            DateTime? recordingStartUtc = null;

            foreach (IDataSource dataSource in dataSources)
            {
                if (!(dataSource is FileDataSource fileDataSource))
                    continue;

                if (!fileDataSource.FullPath.EndsWith(".pcwsnap", StringComparison.OrdinalIgnoreCase))
                    continue;

                ProcessPcwSnapDataSource(fileDataSource, ref firstEventTimestamp, ref lastEventTimestamp, ref recordingStartUtc, dataProcessor, cancellationToken);
            }

            long firstNs = firstEventTimestamp.HasValue ? firstEventTimestamp.Value.ToNanoseconds : 0;
            long lastNs = lastEventTimestamp.HasValue ? lastEventTimestamp.Value.ToNanoseconds : firstNs + 1;
            DateTime wallClock = recordingStartUtc ?? DateTime.UtcNow;
            dataSourceInfo = new DataSourceInfo(firstNs, lastNs, wallClock);
        }

        private void ProcessPcwSnapDataSource(
            FileDataSource fileDataSource,
            ref Timestamp? firstEventTimestamp,
            ref Timestamp? lastEventTimestamp,
            ref DateTime? recordingStartUtc,
            ISourceDataProcessor<Event, ParsingContext, Type> dataProcessor,
            CancellationToken cancellationToken)
        {
            PcwSnapData = PcwSnapReader.Parse(fileDataSource.FullPath);

            recordingStartUtc = PcwSnapData.Header.StartTime.UtcDateTime;

            var prevProcInfo = new Dictionary<uint, ProcInfoEntry>();
            var prevIoStats = new Dictionary<int, KcIoStatsSnapshot>();
            Timestamp? prevSnapshotTimestamp = null;

            for (int i = 0; i < PcwSnapData.Snapshots.Count; i++)
            {
                var snap = PcwSnapData.Snapshots[i];
                var ts = new Timestamp((long)snap.TimestampNs);

                // Snapshot-level event
                var snapEvent = new PcwSnapSnapshotEvent(i, snap);
                dataProcessor.ProcessDataElement(snapEvent, context, cancellationToken);

                // Decode kcdata blob for PID → name map
                KcdataDecoded decoded = null;
                if (snap.KcdataBlob != null && snap.KcdataBlob.Length > 0)
                {
                    try
                    {
                        decoded = KcdataDecoder.Decode(snap.KcdataBlob);
                    }
                    catch
                    {
                        decoded = null;
                    }
                }

                // Build PID → name map from kcdata
                var pidNames = new Dictionary<uint, string>();
                if (decoded != null)
                {
                    foreach (var t in decoded.Tasks)
                        pidNames[(uint)t.Pid] = t.ProcessName;
                }

                // Emit per-process events from ProcInfo
                if (snap.ProcInfo?.Entries != null)
                {
                    foreach (var entry in snap.ProcInfo.Entries)
                    {
                        pidNames.TryGetValue(entry.Pid, out var name);
                        prevProcInfo.TryGetValue(entry.Pid, out var prev);
                        var procEvent = new PcwSnapProcessEvent(i, ts, name ?? $"pid:{entry.Pid}", entry, prev);
                        dataProcessor.ProcessDataElement(procEvent, context, cancellationToken);
                        prevProcInfo[entry.Pid] = entry;
                    }
                }

                // Emit per-process disk I/O events from kcdata IoStats
                if (decoded != null)
                {
                    foreach (var task in decoded.Tasks)
                    {
                        if (task.IoStats == null) continue;
                        prevIoStats.TryGetValue(task.Pid, out var prevIo);
                        var name = pidNames.TryGetValue((uint)task.Pid, out var n) ? n : $"pid:{task.Pid}";
                        var diskEvent = new PcwSnapDiskEvent(i, ts, name, task.Pid, task.IoStats, prevIo, prevSnapshotTimestamp);
                        dataProcessor.ProcessDataElement(diskEvent, context, cancellationToken);
                        prevIoStats[task.Pid] = task.IoStats;
                    }
                }

                prevSnapshotTimestamp = ts;

                if (firstEventTimestamp == null || firstEventTimestamp.Value > ts)
                    firstEventTimestamp = ts;

                if (lastEventTimestamp == null || lastEventTimestamp.Value < ts)
                    lastEventTimestamp = ts;
            }

            if (firstEventTimestamp == null)
            {
                long headerNs = (long)PcwSnapData.Header.Timestamp;
                firstEventTimestamp = new Timestamp(headerNs);
                lastEventTimestamp = firstEventTimestamp;
            }
        }
    }
}
