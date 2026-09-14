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
    public sealed class ActivityMonitorSystemTable
    {
        public static TableDescriptor TableDescriptor =>
            new TableDescriptor(
                Guid.Parse("{6a9bc7f0-9c5b-4d8e-af2d-9c2d0e3f4a51}"),
                "Activity Monitor (System)",
                "System-wide sampled CPU, memory, disk, and network from the Activity Monitor instrument.",
                "Activity Monitor",
                requiredDataCookers: new List<DataCookerPath>
                {
                    ActivityMonitorSystemCooker.DataCookerPath
                });

        private static readonly ColumnConfiguration startTimeColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("1cbb1234-a5b6-4c7d-8e9f-0a1b2c3d4e5f"), "Start Time"),
            new UIHints { IsVisible = true, Width = 100, CellFormat = TimestampFormatter.FormatMillisecondsGrouped });

        private static readonly ColumnConfiguration durationColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("2dcc2345-b6c7-4d8e-9fa0-1b2c3d4e5f60"), "Duration"),
            new UIHints { IsVisible = true, Width = 100, CellFormat = TimestampFormatter.FormatMillisecondsGrouped });

        private static readonly ColumnConfiguration cpuTotalColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("3edd3456-c7d8-4e9f-a0b1-2c3d4e5f6071"), "Total Load %"),
            new UIHints { IsVisible = true, Width = 90, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration cpuSystemColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("4fee4567-d8e9-4fa0-b1c2-3d4e5f607182"), "System Load %"),
            new UIHints { IsVisible = true, Width = 90, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration cpuUserColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("5aff5678-e9fa-40b1-c2d3-4e5f60718293"), "User Load %"),
            new UIHints { IsVisible = true, Width = 90, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration memoryUsedColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("6ba06789-fa0b-41c2-d3e4-5f60718293a4"), "Memory Used"),
            new UIHints { IsVisible = true, Width = 100, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration memoryAppColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("7cb1789a-0b1c-42d3-e4f5-60718293a4b5"), "App Memory"),
            new UIHints { IsVisible = true, Width = 100, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration memoryWiredColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("8dc2890b-1c2d-43e4-f506-718293a4b5c6"), "Wired Memory"),
            new UIHints { IsVisible = true, Width = 100, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration memoryCompressedColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("9ed3901c-2d3e-44f5-0617-8293a4b5c6d7"), "Compressed"),
            new UIHints { IsVisible = true, Width = 100, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration memoryCachedColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("aefa012d-3e4f-4506-1728-93a4b5c6d7e8"), "Cached Files"),
            new UIHints { IsVisible = false, Width = 100, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration swapUsedColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("bf0b123e-4f50-4617-2839-a4b5c6d7e8f9"), "Swap Used"),
            new UIHints { IsVisible = false, Width = 100, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration diskReadColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("c01c234f-5061-4728-394a-b5c6d7e8f90a"), "Data Read"),
            new UIHints { IsVisible = false, Width = 100, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration diskWrittenColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d12d3450-6172-4839-4a5b-c6d7e8f90a1b"), "Data Written"),
            new UIHints { IsVisible = false, Width = 100, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration diskReadSecColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("e23e4561-7283-494a-5b6c-d7e8f90a1b2c"), "Data Read/sec"),
            new UIHints { IsVisible = true, Width = 100, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration diskWrittenSecColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("f34f5672-8394-4a5b-6c7d-e8f90a1b2c3d"), "Data Written/sec"),
            new UIHints { IsVisible = true, Width = 100, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration netInColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("0450678a-94a5-4b6c-7d8e-f90a1b2c3d4e"), "Data Received"),
            new UIHints { IsVisible = false, Width = 100, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration netOutColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("1561789b-a5b6-4c7d-8e9f-0a1b2c3d4e5f"), "Data Sent"),
            new UIHints { IsVisible = false, Width = 100, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration netInSecColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("267289ac-b6c7-4d8e-9fa0-1b2c3d4e5f60"), "Data Received/sec"),
            new UIHints { IsVisible = true, Width = 100, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration netOutSecColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("37838abd-c7d8-4e9f-a0b1-2c3d4e5f6071"), "Data Sent/sec"),
            new UIHints { IsVisible = true, Width = 100, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration processesColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("48949bce-d8e9-4fa0-b1c2-3d4e5f607182"), "Processes"),
            new UIHints { IsVisible = true, Width = 80, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration threadsColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("59a5acdf-e9fa-40b1-c2d3-4e5f60718293"), "Threads"),
            new UIHints { IsVisible = true, Width = 80, AggregationMode = AggregationMode.Max });

        public static bool IsDataAvailable(IDataExtensionRetrieval requiredData)
        {
            var data = requiredData.QueryOutput<List<ActivityMonitorSystemEvent>>(
                new DataOutputPath(ActivityMonitorSystemCooker.DataCookerPath,
                                    nameof(ActivityMonitorSystemCooker.ActivityMonitorSystemEvents)));
            return data != null && data.Count > 0;
        }

        public static void BuildTable(ITableBuilder tableBuilder, IDataExtensionRetrieval requiredData)
        {
            var data = requiredData.QueryOutput<List<ActivityMonitorSystemEvent>>(
                new DataOutputPath(ActivityMonitorSystemCooker.DataCookerPath,
                                    nameof(ActivityMonitorSystemCooker.ActivityMonitorSystemEvents)));

            var builder = tableBuilder.SetRowCount(data.Count);
            var baseProj = Projection.Index(data);

            builder.AddColumn(startTimeColumn, baseProj.Compose(e => e.StartTime.Value));
            builder.AddColumn(durationColumn, baseProj.Compose(e => e.Duration?.Value ?? default));
            builder.AddColumn(cpuTotalColumn, baseProj.Compose(e => (double)(e.CpuTotalLoad?.Value ?? 0UL) / 100.0));
            builder.AddColumn(cpuSystemColumn, baseProj.Compose(e => (double)(e.CpuSystemLoad?.Value ?? 0UL) / 100.0));
            builder.AddColumn(cpuUserColumn, baseProj.Compose(e => (double)(e.CpuUserLoad?.Value ?? 0UL) / 100.0));
            builder.AddColumn(memoryUsedColumn, baseProj.Compose(e => (long)(e.MemoryPhysicalUsed?.Value ?? 0UL)));
            builder.AddColumn(memoryAppColumn, baseProj.Compose(e => (long)(e.MemoryApp?.Value ?? 0UL)));
            builder.AddColumn(memoryWiredColumn, baseProj.Compose(e => (long)(e.MemoryWired?.Value ?? 0UL)));
            builder.AddColumn(memoryCompressedColumn, baseProj.Compose(e => (long)(e.MemoryCompressed?.Value ?? 0UL)));
            builder.AddColumn(memoryCachedColumn, baseProj.Compose(e => (long)(e.MemoryCachedFiles?.Value ?? 0UL)));
            builder.AddColumn(swapUsedColumn, baseProj.Compose(e => (long)(e.VmSwapUsed?.Value ?? 0UL)));
            builder.AddColumn(diskReadColumn, baseProj.Compose(e => (long)(e.DiskBytesRead?.Value ?? 0UL)));
            builder.AddColumn(diskWrittenColumn, baseProj.Compose(e => (long)(e.DiskBytesWritten?.Value ?? 0UL)));
            builder.AddColumn(diskReadSecColumn, baseProj.Compose(e => (long)(e.DiskBytesReadPerSecond?.Value ?? 0UL)));
            builder.AddColumn(diskWrittenSecColumn, baseProj.Compose(e => (long)(e.DiskBytesWrittenPerSecond?.Value ?? 0UL)));
            builder.AddColumn(netInColumn, baseProj.Compose(e => (long)(e.NetBytesIn?.Value ?? 0UL)));
            builder.AddColumn(netOutColumn, baseProj.Compose(e => (long)(e.NetBytesOut?.Value ?? 0UL)));
            builder.AddColumn(netInSecColumn, baseProj.Compose(e => (long)(e.NetBytesInPerSecond?.Value ?? 0UL)));
            builder.AddColumn(netOutSecColumn, baseProj.Compose(e => (long)(e.NetBytesOutPerSecond?.Value ?? 0UL)));
            builder.AddColumn(processesColumn, baseProj.Compose(e => (long)(e.TotalProcesses?.Value ?? 0UL)));
            builder.AddColumn(threadsColumn, baseProj.Compose(e => (long)(e.TotalThreads?.Value ?? 0UL)));

            var cfg = new TableConfiguration("System Overview")
            {
                Columns = new[]
                {
                    startTimeColumn,
                    TableConfiguration.PivotColumn,
                    cpuTotalColumn,
                    cpuUserColumn,
                    cpuSystemColumn,
                    memoryUsedColumn,
                    memoryAppColumn,
                    memoryWiredColumn,
                    memoryCompressedColumn,
                    processesColumn,
                    threadsColumn,
                    TableConfiguration.GraphColumn,
                    durationColumn,
                },
            };
            cfg.AddColumnRole(ColumnRole.StartTime, startTimeColumn);
            cfg.AddColumnRole(ColumnRole.Duration, durationColumn);

            tableBuilder.AddTableConfiguration(cfg);
            tableBuilder.SetDefaultTableConfiguration(cfg);
        }
    }
}
