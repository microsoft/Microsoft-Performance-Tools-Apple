// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using InstrumentsProcessor.Cookers;
using InstrumentsProcessor.Parsing.DataModels;
using InstrumentsProcessor.Parsing.Events;
using Microsoft.Performance.SDK;
using Microsoft.Performance.SDK.Extensibility;
using Microsoft.Performance.SDK.Processing;
using System;
using System.Collections.Generic;

namespace InstrumentsProcessor.Tables
{
    [Table]
    public sealed class ActivityMonitorProcessTable
    {
        public static TableDescriptor TableDescriptor =>
            new TableDescriptor(
                Guid.Parse("{5f8ab6ef-8b4a-4b7c-9c1c-8b1c9d2e3f40}"),
                "Activity Monitor (Processes)",
                "Per-process sampled resource usage from the Activity Monitor instrument.",
                "Activity Monitor",
                requiredDataCookers: new List<DataCookerPath>
                {
                    ActivityMonitorProcessCooker.DataCookerPath
                });

        private static readonly ColumnConfiguration startTimeColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("6a5b4c3d-2e1f-4a0b-9c8d-7e6f5a4b3c2d"), "Start Time"),
            new UIHints { IsVisible = true, Width = 100, CellFormat = TimestampFormatter.FormatMillisecondsGrouped });

        private static readonly ColumnConfiguration durationColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("7b6c5d4e-3f2a-4b1c-a09d-8e7f6a5b4c3d"), "Duration"),
            new UIHints { IsVisible = true, Width = 100, CellFormat = TimestampFormatter.FormatMillisecondsGrouped });

        private static readonly ColumnConfiguration processNameColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("8c7d6e5f-4a3b-4c2d-b1ae-9f8a7b6c5d4e"), "Process"),
            new UIHints { IsVisible = true, Width = 200 });

        private static readonly ColumnConfiguration responsibleProcessColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("9d8e7f6a-5b4c-4d3e-c2bf-a0987b6c5d4e"), "Responsible Process"),
            new UIHints { IsVisible = false, Width = 200 });

        private static readonly ColumnConfiguration processIdColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("aebf8091-6c5d-4e3f-d4c0-b1a9887c6d5e"), "PID"),
            new UIHints { IsVisible = true, Width = 60 });

        private static readonly ColumnConfiguration userIdColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("bfa09182-7d6e-4f40-e5d1-c2ba99887d6e"), "UID"),
            new UIHints { IsVisible = false, Width = 60 });

        private static readonly ColumnConfiguration cpuPercentColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("c0b1a293-8e7f-4051-f6e2-d3cbaa998e7f"), "% CPU"),
            new UIHints { IsVisible = true, Width = 70, AggregationMode = AggregationMode.Sum, SortOrder = SortOrder.Descending, SortPriority = 0 });

        private static readonly ColumnConfiguration cpuTimeColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d1c2b3a4-9f80-4162-a7f3-e4dcbb0a9f80"), "CPU Time"),
            new UIHints { IsVisible = true, Width = 100, AggregationMode = AggregationMode.Max, CellFormat = TimestampFormatter.FormatMillisecondsGrouped });

        private static readonly ColumnConfiguration threadCountColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("e2d3c4b5-a091-4273-b804-f5edcc1ba091"), "# Threads"),
            new UIHints { IsVisible = true, Width = 70, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration machPortsColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("f3e4d5c6-b1a2-4384-c915-a6fedd2cb1a2"), "# Ports"),
            new UIHints { IsVisible = false, Width = 70, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration memoryColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("a4f5e6d7-c2b3-4495-da26-b70fee3dc2b3"), "Memory"),
            new UIHints { IsVisible = true, Width = 90, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration realMemColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b506f7e8-d3c4-45a6-eb37-c810ff4ed3c4"), "Real Mem"),
            new UIHints { IsVisible = false, Width = 90, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration realPrivateMemColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("c61708f9-e4d5-46b7-fc48-d921004fe4d5"), "Real Private Mem"),
            new UIHints { IsVisible = false, Width = 100, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration realSharedMemColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d728190a-f5e6-47c8-0d59-ea32115ff5e6"), "Real Shared Mem"),
            new UIHints { IsVisible = false, Width = 100, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration purgeableMemColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("e83920ab-06f7-48d9-1e6a-fb432260006f"), "Purgeable Mem"),
            new UIHints { IsVisible = false, Width = 90, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration compressedMemColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("f94a31bc-1708-49ea-2f7b-0c543371117a"), "Compressed Mem"),
            new UIHints { IsVisible = false, Width = 90, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration archColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("0a5b42cd-2819-4a0b-308c-1d65448222c8"), "Kind"),
            new UIHints { IsVisible = true, Width = 70 });

        private static readonly ColumnConfiguration idleWakeupsColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("1b6c53de-2920-4b1c-419d-2e76559333d9"), "Idle Wake Ups"),
            new UIHints { IsVisible = true, Width = 80, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration diskReadsColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("2c7d64ef-3a31-4c2d-52ae-3f87660444ea"), "Disk Reads"),
            new UIHints { IsVisible = false, Width = 90, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration diskWritesColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("3d8e75f0-4b42-4d3e-63bf-40987715550b"), "Disk Writes"),
            new UIHints { IsVisible = false, Width = 90, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration diskReadsPerSecColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("4e9f8601-5c53-4e4f-74c0-51a988266f1c"), "Data Read/sec"),
            new UIHints { IsVisible = false, Width = 100, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration diskWritesPerSecColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("5faa9712-6d64-4f50-85d1-62ba99377a2d"), "Data Written/sec"),
            new UIHints { IsVisible = false, Width = 100, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration sandboxColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("60bba823-7e75-4061-96e2-73cbaa488b3e"), "Sandbox"),
            new UIHints { IsVisible = false, Width = 70 });

        private static readonly ColumnConfiguration restrictedColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("71ccb934-8f86-4172-a7f3-84dcbb599c4f"), "Restricted"),
            new UIHints { IsVisible = false, Width = 70 });

        private static readonly ColumnConfiguration appNapColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("82ddca45-a097-4283-b804-95edcc6aad50"), "App Nap"),
            new UIHints { IsVisible = false, Width = 70 });

        private static readonly ColumnConfiguration suddenTerminationColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("93eedb56-b1a8-4394-c915-a6fedd7bbe61"), "Sudden Termination"),
            new UIHints { IsVisible = false, Width = 90 });

        private static readonly ColumnConfiguration preventingSleepColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("a4ffec67-c2b9-44a5-da26-b70fee8ccf72"), "Preventing Sleep"),
            new UIHints { IsVisible = false, Width = 90 });

        public static bool IsDataAvailable(IDataExtensionRetrieval requiredData)
        {
            var data = requiredData.QueryOutput<List<ActivityMonitorProcessEvent>>(
                new DataOutputPath(ActivityMonitorProcessCooker.DataCookerPath,
                                    nameof(ActivityMonitorProcessCooker.ActivityMonitorProcessEvents)));
            return data != null && data.Count > 0;
        }

        public static void BuildTable(ITableBuilder tableBuilder, IDataExtensionRetrieval requiredData)
        {
            var data = requiredData.QueryOutput<List<ActivityMonitorProcessEvent>>(
                new DataOutputPath(ActivityMonitorProcessCooker.DataCookerPath,
                                    nameof(ActivityMonitorProcessCooker.ActivityMonitorProcessEvents)));

            var builder = tableBuilder.SetRowCount(data.Count);
            var baseProj = Projection.Index(data);

            builder.AddColumn(startTimeColumn, baseProj.Compose(e => e.StartTime.Value));
            builder.AddColumn(durationColumn, baseProj.Compose(e => e.Duration?.Value ?? default));
            builder.AddColumn(processNameColumn, baseProj.Compose(e => e.Process?.Name ?? "Unknown"));
            builder.AddColumn(responsibleProcessColumn, baseProj.Compose(e => e.ResponsibleProcess?.Name ?? string.Empty));
            builder.AddColumn(processIdColumn, baseProj.Compose(e => (long)(e.ProcessId?.Value ?? 0UL)));
            builder.AddColumn(userIdColumn, baseProj.Compose(e => (long)(e.UserId?.Value ?? 0UL)));
            builder.AddColumn(cpuPercentColumn, baseProj.Compose(e => (double)(e.CpuPercent?.Value ?? 0UL) / 100.0));
            builder.AddColumn(cpuTimeColumn, baseProj.Compose(e => e.CpuTime?.Value ?? default));
            builder.AddColumn(threadCountColumn, baseProj.Compose(e => (long)(e.ThreadCount?.Value ?? 0UL)));
            builder.AddColumn(machPortsColumn, baseProj.Compose(e => (long)(e.MachPortCount?.Value ?? 0UL)));
            builder.AddColumn(memoryColumn, baseProj.Compose(e => (long)(e.MemoryPhysicalFootprint?.Value ?? 0UL)));
            builder.AddColumn(realMemColumn, baseProj.Compose(e => (long)(e.MemoryReal?.Value ?? 0UL)));
            builder.AddColumn(realPrivateMemColumn, baseProj.Compose(e => (long)(e.MemoryRealPrivate?.Value ?? 0UL)));
            builder.AddColumn(realSharedMemColumn, baseProj.Compose(e => (long)(e.MemoryRealShared?.Value ?? 0UL)));
            builder.AddColumn(purgeableMemColumn, baseProj.Compose(e => (long)(e.MemoryPurgeable?.Value ?? 0UL)));
            builder.AddColumn(compressedMemColumn, baseProj.Compose(e => (long)(e.MemoryCompressed?.Value ?? 0UL)));
            builder.AddColumn(archColumn, baseProj.Compose(e => e.ArchKind?.Value ?? string.Empty));
            builder.AddColumn(idleWakeupsColumn, baseProj.Compose(e => (long)(e.IdleWakeups?.Value ?? 0UL)));
            builder.AddColumn(diskReadsColumn, baseProj.Compose(e => (long)(e.DiskBytesRead?.Value ?? 0UL)));
            builder.AddColumn(diskWritesColumn, baseProj.Compose(e => (long)(e.DiskBytesWritten?.Value ?? 0UL)));
            builder.AddColumn(diskReadsPerSecColumn, baseProj.Compose(e => (long)(e.DiskBytesReadPerSecond?.Value ?? 0UL)));
            builder.AddColumn(diskWritesPerSecColumn, baseProj.Compose(e => (long)(e.DiskBytesWrittenPerSecond?.Value ?? 0UL)));
            builder.AddColumn(sandboxColumn, baseProj.Compose(e => e.Sandbox?.Value ?? false));
            builder.AddColumn(restrictedColumn, baseProj.Compose(e => e.Restricted?.Value ?? false));
            builder.AddColumn(appNapColumn, baseProj.Compose(e => e.AppNap?.Value ?? false));
            builder.AddColumn(suddenTerminationColumn, baseProj.Compose(e => e.SuddenTermination?.Value ?? false));
            builder.AddColumn(preventingSleepColumn, baseProj.Compose(e => e.PreventingSleep?.Value ?? false));

            var byProcess = new TableConfiguration("Per Process (sum over trace)")
            {
                Columns = new[]
                {
                    processNameColumn,
                    processIdColumn,
                    archColumn,
                    TableConfiguration.PivotColumn,
                    cpuPercentColumn,
                    cpuTimeColumn,
                    threadCountColumn,
                    memoryColumn,
                    idleWakeupsColumn,
                    TableConfiguration.GraphColumn,
                    startTimeColumn,
                    durationColumn,
                },
            };
            byProcess.AddColumnRole(ColumnRole.StartTime, startTimeColumn);
            byProcess.AddColumnRole(ColumnRole.Duration, durationColumn);

            var timeline = new TableConfiguration("Timeline")
            {
                Columns = new[]
                {
                    processNameColumn,
                    processIdColumn,
                    TableConfiguration.PivotColumn,
                    cpuPercentColumn,
                    memoryColumn,
                    threadCountColumn,
                    TableConfiguration.GraphColumn,
                    startTimeColumn,
                    durationColumn,
                },
            };
            timeline.AddColumnRole(ColumnRole.StartTime, startTimeColumn);
            timeline.AddColumnRole(ColumnRole.Duration, durationColumn);

            tableBuilder.AddTableConfiguration(byProcess);
            tableBuilder.AddTableConfiguration(timeline);
            tableBuilder.SetDefaultTableConfiguration(byProcess);
        }
    }
}
