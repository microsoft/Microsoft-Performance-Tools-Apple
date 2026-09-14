// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using InstrumentsProcessor.Cookers;
using InstrumentsProcessor.Parsing.Events;
using Microsoft.Performance.SDK;
using Microsoft.Performance.SDK.Extensibility;
using Microsoft.Performance.SDK.Processing;
using System;
using System.Collections.Generic;

namespace InstrumentsProcessor.Tables
{
    [Table]
    public sealed class DiskIoTable
    {
        public static TableDescriptor TableDescriptor =>
            new TableDescriptor(
                Guid.Parse("{d15c20e0-a1b2-4c3d-8e4f-506172839405}"),
                "Disk I/O",
                "Per-event disk I/O operations from the Instruments dil-disk-io schema",
                "Disk",
                requiredDataCookers: new List<DataCookerPath>
                {
                    DiskIoCooker.DataCookerPath
                });

        // ---- Identity columns ----
        private static readonly ColumnConfiguration startTimeColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c20e0-0001-4000-8000-000000000001"), "Start Time"),
            new UIHints
            {
                IsVisible = true,
                Width = 120,
                AggregationMode = AggregationMode.Min,
                CellFormat = TimestampFormatter.FormatMicrosecondsGrouped,
            });

        private static readonly ColumnConfiguration stopTimeColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c20e0-0001-4000-8000-000000000002"), "Stop Time"),
            new UIHints
            {
                IsVisible = true,
                Width = 120,
                AggregationMode = AggregationMode.Max,
                CellFormat = TimestampFormatter.FormatMicrosecondsGrouped,
            });

        private static readonly ColumnConfiguration latencyColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c20e0-0001-4000-8000-000000000003"), "Latency"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                AggregationMode = AggregationMode.Sum,
                CellFormat = TimestampFormatter.FormatMicrosecondsGrouped,
            });

        private static readonly ColumnConfiguration processNameColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c20e0-0001-4000-8000-000000000004"), "Process"),
            new UIHints { IsVisible = true, Width = 150 });

        private static readonly ColumnConfiguration processIdColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c20e0-0001-4000-8000-000000000005"), "Process ID"),
            new UIHints { IsVisible = true, Width = 80 });

        private static readonly ColumnConfiguration threadNameColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c20e0-0001-4000-8000-000000000006"), "Thread Name"),
            new UIHints { IsVisible = true, Width = 150 });

        private static readonly ColumnConfiguration threadIdColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c20e0-0001-4000-8000-000000000007"), "Thread ID"),
            new UIHints { IsVisible = true, Width = 80 });

        // ---- Operation details ----
        private static readonly ColumnConfiguration operationColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c20e0-0001-4000-8000-000000000010"), "Operation"),
            new UIHints { IsVisible = true, Width = 120 });

        private static readonly ColumnConfiguration syncColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c20e0-0001-4000-8000-000000000011"), "Sync"),
            new UIHints { IsVisible = true, Width = 60 });

        private static readonly ColumnConfiguration tierColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c20e0-0001-4000-8000-000000000012"), "Tier"),
            new UIHints { IsVisible = true, Width = 50 });

        private static readonly ColumnConfiguration flagsColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c20e0-0001-4000-8000-000000000013"), "Flags"),
            new UIHints { IsVisible = true, Width = 80 });

        // ---- Size / throughput ----
        private static readonly ColumnConfiguration sizeColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c20e0-0001-4000-8000-000000000020"), "Size"),
            new UIHints { IsVisible = true, Width = 90, AggregationMode = AggregationMode.Sum });

        private static readonly ColumnConfiguration throughputColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c20e0-0001-4000-8000-000000000021"), "Throughput (B/s)"),
            new UIHints { IsVisible = true, Width = 120, AggregationMode = AggregationMode.Average });

        // ---- Block / device ----
        private static readonly ColumnConfiguration blockColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c20e0-0001-4000-8000-000000000030"), "Block"),
            new UIHints { IsVisible = true, Width = 110 });

        private static readonly ColumnConfiguration deviceColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c20e0-0001-4000-8000-000000000031"), "Device"),
            new UIHints { IsVisible = true, Width = 80 });

        // ---- Queue / buf / error ----
        private static readonly ColumnConfiguration queueDepthColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c20e0-0001-4000-8000-000000000040"), "Queue Depth"),
            new UIHints { IsVisible = true, Width = 90, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration bufTColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c20e0-0001-4000-8000-000000000041"), "buf_t"),
            new UIHints { IsVisible = false, Width = 140 });

        private static readonly ColumnConfiguration errorColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c20e0-0001-4000-8000-000000000042"), "Error"),
            new UIHints { IsVisible = true, Width = 60 });

        private static readonly ColumnConfiguration residColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c20e0-0001-4000-8000-000000000043"), "Resid"),
            new UIHints { IsVisible = true, Width = 80, AggregationMode = AggregationMode.Sum });

        private static readonly ColumnConfiguration countColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c20e0-0001-4000-8000-000000000050"), "Count"),
            new UIHints { IsVisible = true, Width = 60, AggregationMode = AggregationMode.Sum });

        public static bool IsDataAvailable(IDataExtensionRetrieval requiredData)
        {
            var data = requiredData.QueryOutput<List<DiskIoEvent>>(
                new DataOutputPath(DiskIoCooker.DataCookerPath, nameof(DiskIoCooker.DiskIoEvents)));
            return data != null && data.Count > 0;
        }

        public static void BuildTable(
            ITableBuilder tableBuilder,
            IDataExtensionRetrieval requiredData)
        {
            List<DiskIoEvent> data =
                requiredData.QueryOutput<List<DiskIoEvent>>(
                    new DataOutputPath(DiskIoCooker.DataCookerPath, nameof(DiskIoCooker.DiskIoEvents)));

            var table = tableBuilder.SetRowCount(data.Count);
            var bp = Projection.Index(data);

            table.AddColumn(startTimeColumn, bp.Compose(Projector.StartTimeProjector));
            table.AddColumn(stopTimeColumn, bp.Compose(Projector.StopTimeProjector));
            table.AddColumn(latencyColumn, bp.Compose(Projector.LatencyProjector));
            table.AddColumn(processNameColumn, bp.Compose((DiskIoEvent e) => Projector.ProcessProjector(e)).Compose(Projector.ProcessNameProjector));
            table.AddColumn(processIdColumn, bp.Compose((DiskIoEvent e) => Projector.ProcessProjector(e)).Compose(Projector.ProcessIdProjector));
            table.AddColumn(threadNameColumn, bp.Compose((DiskIoEvent e) => Projector.ThreadProjector(e)).Compose(Projector.ThreadNameProjector));
            table.AddColumn(threadIdColumn, bp.Compose((DiskIoEvent e) => Projector.ThreadProjector(e)).Compose(Projector.ThreadIdProjector));
            table.AddColumn(operationColumn, bp.Compose(Projector.OperationProjector));
            table.AddColumn(syncColumn, bp.Compose(Projector.SyncModeProjector));
            table.AddColumn(tierColumn, bp.Compose(Projector.TierProjector));
            table.AddColumn(flagsColumn, bp.Compose(Projector.FlagsProjector));
            table.AddColumn(sizeColumn, bp.Compose(Projector.SizeProjector));
            table.AddColumn(throughputColumn, bp.Compose(Projector.ThroughputProjector));
            table.AddColumn(blockColumn, bp.Compose(Projector.BlockNumberProjector));
            table.AddColumn(deviceColumn, bp.Compose(Projector.DeviceProjector));
            table.AddColumn(queueDepthColumn, bp.Compose(Projector.QueueDepthProjector));
            table.AddColumn(bufTColumn, bp.Compose(Projector.BufTProjector));
            table.AddColumn(errorColumn, bp.Compose(Projector.ErrorProjector));
            table.AddColumn(residColumn, bp.Compose(Projector.ResidProjector));
            table.AddColumn(countColumn, Projection.Constant(1));

            var ioByProcess = new TableConfiguration("Disk I/O by Process")
            {
                Columns = new[]
                {
                    processNameColumn,
                    operationColumn,
                    deviceColumn,
                    TableConfiguration.PivotColumn,
                    countColumn,
                    sizeColumn,
                    latencyColumn,
                    throughputColumn,
                    queueDepthColumn,
                    syncColumn,
                    tierColumn,
                    flagsColumn,
                    errorColumn,
                    TableConfiguration.GraphColumn,
                    startTimeColumn,
                    stopTimeColumn,
                },
            };
            ioByProcess.AddColumnRole(ColumnRole.StartTime, startTimeColumn);
            ioByProcess.AddColumnRole(ColumnRole.EndTime, stopTimeColumn);
            ioByProcess.AddColumnRole(ColumnRole.Duration, latencyColumn);
            tableBuilder.AddTableConfiguration(ioByProcess);

            var ioTimeline = new TableConfiguration("Disk I/O Timeline")
            {
                Columns = new[]
                {
                    processNameColumn,
                    threadIdColumn,
                    operationColumn,
                    syncColumn,
                    flagsColumn,
                    TableConfiguration.PivotColumn,
                    countColumn,
                    sizeColumn,
                    latencyColumn,
                    throughputColumn,
                    queueDepthColumn,
                    tierColumn,
                    blockColumn,
                    deviceColumn,
                    errorColumn,
                    residColumn,
                    TableConfiguration.GraphColumn,
                    startTimeColumn,
                    stopTimeColumn,
                },
            };
            ioTimeline.AddColumnRole(ColumnRole.StartTime, startTimeColumn);
            ioTimeline.AddColumnRole(ColumnRole.EndTime, stopTimeColumn);
            ioTimeline.AddColumnRole(ColumnRole.Duration, latencyColumn);
            tableBuilder.AddTableConfiguration(ioTimeline);

            tableBuilder.SetDefaultTableConfiguration(ioByProcess);
        }
    }
}
