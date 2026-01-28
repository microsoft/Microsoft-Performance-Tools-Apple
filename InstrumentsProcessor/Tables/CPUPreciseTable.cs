// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using InstrumentsProcessor.Cookers;
using InstrumentsProcessor.Parsing.Events;
using Microsoft.Performance.SDK;
using Microsoft.Performance.SDK.Extensibility;
using Microsoft.Performance.SDK.Processing;
using System;
using System.Collections.Generic;
using System.Linq;

namespace InstrumentsProcessor.Tables
{
    [Table]
    public sealed class CPUPreciseTable
    {
        public static TableDescriptor TableDescriptor =>
           new TableDescriptor(
              Guid.Parse("{14ebaa36-32cb-4358-932d-3964d6b348aa}"),
              "CPU Usage (Precise)",
              "Events from Thread State",
              "Thread State",
              requiredDataCookers: new List<DataCookerPath>
              {
              ThreadStateCooker.DataCookerPath
              });

        private static readonly ColumnConfiguration switchInTimeColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("219f0a98-8383-41e5-ae7e-269532537284"), "Switch-In Time") { ShortDescription = "The time at which the new thread is switched in" },
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                CellFormat = TimestampFormatter.FormatSecondsGrouped,
                AggregationMode = AggregationMode.Min
            });

        private static readonly ColumnConfiguration switchOutTimeColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d4a0efb9-1002-4017-ae68-6c77943a582e"), "Switch-Out Time") { ShortDescription = "The next switch-out time of the new thread" },
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                CellFormat = TimestampFormatter.FormatSecondsGrouped,
                AggregationMode = AggregationMode.Max
            });

        private static readonly ColumnConfiguration runTimeColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("e578ce4c-5fc6-4b95-8c43-8aa361440829"), "CPU Time") { ShortDescription = "The time the thread switching in spends switched in" },
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                AggregationMode = AggregationMode.Sum,
                CellFormat = TimestampFormatter.FormatMillisecondsGrouped
            });

        private static readonly ColumnConfiguration threadIdColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("6f332dcc-6a40-484c-95cb-4c30b2193015"), "Thread ID"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration threadNameColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("ce446c79-2986-4133-9151-486fa48f69df"), "Thread Name"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration processIdColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("c94b5117-a5b2-443c-bfcb-1b9d4c7687a4"), "Process ID"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration processNameColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("9c8ee95d-589b-4355-bc95-5b709e1c2e83"), "Process"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration deviceSessionColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("a7fdbac3-c13d-454d-8536-831760857cbe"), "Device Session"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration cpuIdColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("3a78ac08-6126-41e3-a7e5-27b8537613f6"), "CPU ID"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration cpuColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b644001c-4171-4a70-bab1-e0da9f967f18"), "CPU"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration priorityColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("a71cc240-2c24-48bc-aa38-3720600c9ec9"), "Priority"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration readyTimeColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d2be4eba-6714-4c45-bf8d-9cd626729f5f"), "Ready Time") { ShortDescription = "The time the thread switching in spent between waiting and being switched in" },
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                CellFormat = TimestampFormatter.FormatMillisecondsGrouped,
                AggregationMode = AggregationMode.Max
            });

        private static readonly ColumnConfiguration waitTimeColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("2ebc93c7-391d-4ed9-895d-177f5dfc5289"), "Wait Time") { ShortDescription = "The time the thread switching in spent waiting" },
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                CellFormat = TimestampFormatter.FormatMillisecondsGrouped,
                AggregationMode = AggregationMode.Max
            });

        private static readonly ColumnConfiguration noteColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("17846491-9920-46a6-b555-c2de0de9a16d"), "Note"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration summaryColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("ce34ae29-a674-4f6d-87b4-cdaacbfb6a5a"), "Summary"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration countPreset = new ColumnConfiguration(
            new ColumnMetadata(new Guid("7ee73c5b-84b4-440b-83f8-e294ba59a701"), "Count"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                AggregationMode = AggregationMode.Sum
            });

        private static readonly ColumnConfiguration cpuUsageInViewportColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("E008ED7A-15B0-40AB-854B-B5F6392F298B"), "CPU Usage (in view)") { ShortDescription = "The time the thread switching in spends switched in" },
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                CellFormat = TimestampFormatter.FormatMillisecondsGrouped,
                AggregationMode = AggregationMode.Sum
            });

        private static readonly ColumnConfiguration percentCpuUsageColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("FC672588-DA05-4F43-991F-6B644A3F5B3D"), "% CPU Usage") { IsPercent = true, ShortDescription = "Percentage of the total visible processor that was spent by the thread switching in" },
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                SortOrder = SortOrder.Descending,
                CellFormat = ColumnFormats.PercentFormat,
                AggregationMode = AggregationMode.Sum
            });

        //
        // This method, with this exact signature, is required so that the runtime can 
        // build your table once all cookers have processed their data.
        //
        public static void BuildTable(
            ITableBuilder tableBuilder,
            IDataExtensionRetrieval requiredData
        )
        {
            List<ThreadStateEvent> data =
                requiredData.QueryOutput<List<ThreadStateEvent>>(new DataOutputPath(ThreadStateCooker.DataCookerPath, nameof(ThreadStateCooker.ThreadStateEvents)));

            ITableBuilderWithRowCount tableBuilderWithRowCount = tableBuilder.SetRowCount(data.Count);

            var baseProjection = Projection.Index(data);

            var switchInTimeProjection = baseProjection.Compose(Projector.SwitchInTimeProjector);
            var switchOutTimeProjection = baseProjection.Compose(Projector.SwitchOutTimeProjector);
            var runTimeProjection = baseProjection.Compose(Projector.DurationProjector);
            var threadProjection = baseProjection.Compose(Projector.ThreadProjector);
            var threadIdProjection = threadProjection.Compose(Projector.ThreadIdProjector);
            var threadNameProjection = threadProjection.Compose(Projector.ThreadNameProjector);
            var processProjection = baseProjection.Compose(Projector.ProcessProjector);
            var processIdProjection = processProjection.Compose(Projector.ProcessIdProjector);
            var processNameProjection = processProjection.Compose(Projector.ProcessNameProjector);
            var deviceSessionProjection = processProjection.Compose(Projector.DeviceSessionProjector);
            var cpuIdProjection = baseProjection.Compose(Projector.CpuIdProjector);
            var cpuProjection = baseProjection.Compose(Projector.CpuProjector);
            var priorityProjection = baseProjection.Compose(Projector.PriorityProjector);
            var readyTimeProjection = baseProjection.Compose(Projector.ReadyTimeProjector);
            var waitTimeProjection = baseProjection.Compose(Projector.WaitTimeProjector);
            var noteProjection = baseProjection.Compose(Projector.NoteProjector);
            var summaryProjection = baseProjection.Compose(Projector.SummaryProjector);

            TimestampDelta ReduceTimeSinceLastDiff(Timestamp timeSinceLast1, Timestamp timeSinceLast2)
            {
                return timeSinceLast1 - timeSinceLast2;
            }

            var viewportClippedSwitchOutTimeForPreviousOnCpuProjection =
                Projection.ClipTimeToVisibleDomain.Create(switchInTimeProjection);
            var viewportClippedSwitchOutTimeForNextOnCpuProjection =
                Projection.ClipTimeToVisibleDomain.Create(switchOutTimeProjection);
            var cpuUsageProjection = Projection.Project(switchOutTimeProjection, switchInTimeProjection, ReduceTimeSinceLastDiff);
            var cpuUsageInViewportProjection = Projection.Project(
                    viewportClippedSwitchOutTimeForNextOnCpuProjection,
                    viewportClippedSwitchOutTimeForPreviousOnCpuProjection,
                    ReduceTimeSinceLastDiff);
            var percentCpuUsageProjection =
                Projection.VisibleDomainRelativePercent.Create(cpuUsageInViewportProjection);

            tableBuilderWithRowCount.AddColumn(switchInTimeColumn, switchInTimeProjection);
            tableBuilderWithRowCount.AddColumn(switchOutTimeColumn, switchOutTimeProjection);
            tableBuilderWithRowCount.AddColumn(runTimeColumn, runTimeProjection);
            tableBuilderWithRowCount.AddColumn(threadIdColumn, threadIdProjection);
            tableBuilderWithRowCount.AddColumn(threadNameColumn, threadNameProjection);
            tableBuilderWithRowCount.AddColumn(processIdColumn, processIdProjection);
            tableBuilderWithRowCount.AddColumn(processNameColumn, processNameProjection);
            tableBuilderWithRowCount.AddColumn(deviceSessionColumn, deviceSessionProjection);
            tableBuilderWithRowCount.AddColumn(cpuIdColumn, cpuIdProjection);
            tableBuilderWithRowCount.AddColumn(cpuColumn, cpuProjection);
            tableBuilderWithRowCount.AddColumn(priorityColumn, priorityProjection);
            tableBuilderWithRowCount.AddColumn(readyTimeColumn, readyTimeProjection);
            tableBuilderWithRowCount.AddColumn(waitTimeColumn, waitTimeProjection);
            tableBuilderWithRowCount.AddColumn(noteColumn, noteProjection);
            tableBuilderWithRowCount.AddColumn(summaryColumn, summaryProjection);
            tableBuilderWithRowCount.AddColumn(cpuUsageInViewportColumn, cpuUsageInViewportProjection);
            tableBuilderWithRowCount.AddColumn(percentCpuUsageColumn, percentCpuUsageProjection);
            tableBuilderWithRowCount.AddColumn(countPreset, Projection.Constant(1));

            var tableConfig = new TableConfiguration("Utilization by Process, Thread")
            {
                Columns = new[]
                {
                    processNameColumn,
                    threadIdColumn,
                    TableConfiguration.PivotColumn,
                    countPreset,
                    waitTimeColumn,
                    readyTimeColumn,
                    runTimeColumn,
                    cpuUsageInViewportColumn,
                    switchInTimeColumn,
                    TableConfiguration.GraphColumn,
                    percentCpuUsageColumn
                },
            };

            tableConfig.AddColumnRole(ColumnRole.StartTime, switchInTimeColumn);
            tableConfig.AddColumnRole(ColumnRole.EndTime, switchOutTimeColumn);
            tableConfig.AddColumnRole(ColumnRole.ResourceId, cpuIdColumn);
            tableBuilder.AddTableConfiguration(tableConfig);

            var tableConfigTimeLineByCpu = new TableConfiguration("Timeline by CPU")
            {
                Columns = new[]
                {
                    cpuColumn,
                    processNameColumn,
                    TableConfiguration.PivotColumn,
                    countPreset,
                    runTimeColumn,
                    cpuUsageInViewportColumn,
                    TableConfiguration.GraphColumn,
                    switchInTimeColumn,
                    switchOutTimeColumn
                },
            };

            tableConfigTimeLineByCpu.AddColumnRole(ColumnRole.StartTime, switchInTimeColumn);
            tableConfigTimeLineByCpu.AddColumnRole(ColumnRole.EndTime, switchOutTimeColumn);
            tableConfigTimeLineByCpu.AddColumnRole(ColumnRole.ResourceId, cpuIdColumn);
            tableBuilder.AddTableConfiguration(tableConfigTimeLineByCpu);

            var tableConfigTimeLineByProcess = new TableConfiguration("Timeline by Process,Thread")
            {
                Columns = new[]
                {
                    processNameColumn,
                    threadIdColumn,
                    TableConfiguration.PivotColumn,
                    countPreset,
                    runTimeColumn,
                    cpuUsageInViewportColumn,
                    TableConfiguration.GraphColumn,
                    switchInTimeColumn,
                    switchOutTimeColumn
                },
            };

            tableConfigTimeLineByProcess.AddColumnRole(ColumnRole.StartTime, switchInTimeColumn);
            tableConfigTimeLineByProcess.AddColumnRole(ColumnRole.EndTime, switchOutTimeColumn);
            tableConfigTimeLineByProcess.AddColumnRole(ColumnRole.ResourceId, cpuIdColumn);
            tableBuilder.AddTableConfiguration(tableConfigTimeLineByProcess);

            tableBuilder.SetDefaultTableConfiguration(tableConfig);
        }
    }
}