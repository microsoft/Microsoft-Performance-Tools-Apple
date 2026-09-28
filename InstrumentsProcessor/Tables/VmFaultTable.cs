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
    public sealed class VmFaultTable
    {
        public static TableDescriptor TableDescriptor =>
            new TableDescriptor(
                Guid.Parse("{c1d2e3f4-a5b6-47c8-9d0e-1f2a3b4c5d6e}"),
                "Virtual Memory Fault",
                "Virtual memory faults emitted by the Instruments \"vml-vm-fault\" schema",
                "Memory",
                requiredDataCookers: new List<DataCookerPath>
                {
                    VmFaultCooker.DataCookerPath
                });

        private static readonly ColumnConfiguration startTimeColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("{d2e3f4a5-b6c7-48d9-0e1f-2a3b4c5d6e7f}"), "Start Time"),
            new UIHints
            {
                IsVisible = true,
                Width = 120,
                AggregationMode = AggregationMode.Min,
                CellFormat = TimestampFormatter.FormatMicrosecondsGrouped,
            });

        private static readonly ColumnConfiguration stopTimeColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("{e3f4a5b6-c7d8-49e0-1f2a-3b4c5d6e7f80}"), "Stop Time"),
            new UIHints
            {
                IsVisible = true,
                Width = 120,
                AggregationMode = AggregationMode.Max,
                CellFormat = TimestampFormatter.FormatMicrosecondsGrouped,
            });

        private static readonly ColumnConfiguration durationColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("{f4a5b6c7-d8e9-4a01-2f3b-4c5d6e7f8091}"), "Duration"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                AggregationMode = AggregationMode.Sum,
                CellFormat = TimestampFormatter.FormatMillisecondsGrouped,
            });

        private static readonly ColumnConfiguration processNameColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("{a5b6c7d8-e9f0-4b12-3c4d-5e6f70819202}"), "Process"),
            new UIHints
            {
                IsVisible = true,
                Width = 150,
            });

        private static readonly ColumnConfiguration processIdColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("{b6c7d8e9-f0a1-4c23-4d5e-6f70819202a3}"), "Process ID"),
            new UIHints
            {
                IsVisible = true,
                Width = 80,
            });

        private static readonly ColumnConfiguration threadNameColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("{c7d8e9f0-a1b2-4d34-5e6f-70819202a3b4}"), "Thread Name"),
            new UIHints
            {
                IsVisible = true,
                Width = 150,
            });

        private static readonly ColumnConfiguration threadIdColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("{d8e9f0a1-b2c3-4e45-6f70-819202a3b4c5}"), "Thread ID"),
            new UIHints
            {
                IsVisible = true,
                Width = 80,
            });

        private static readonly ColumnConfiguration operationColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("{e9f0a1b2-c3d4-4f56-7081-9202a3b4c5d6}"), "Operation"),
            new UIHints
            {
                IsVisible = true,
                Width = 80,
            });

        private static readonly ColumnConfiguration faultDurationColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("{f0a1b2c3-d4e5-4067-8192-02a3b4c5d6e7}"), "Fault Duration"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                AggregationMode = AggregationMode.Sum,
                CellFormat = TimestampFormatter.FormatMicrosecondsGrouped,
            });

        private static readonly ColumnConfiguration sizeColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("{a1b2c3d4-e5f6-4178-9203-a3b4c5d6e7f8}"), "Size"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                AggregationMode = AggregationMode.Sum,
            });

        private static readonly ColumnConfiguration layoutIdColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("{b2c3d4e5-f6a7-4289-0314-b4c5d6e7f809}"), "Layout ID"),
            new UIHints
            {
                IsVisible = false,
                Width = 80,
            });

        private static readonly ColumnConfiguration cpuColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("{c4d5e6f7-a8b9-43ab-1526-d6e7f8091021}"), "CPU"),
            new UIHints
            {
                IsVisible = true,
                Width = 80,
            });

        private static readonly ColumnConfiguration countColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("{c3d4e5f6-a7b8-439a-1425-c5d6e7f80910}"), "Count"),
            new UIHints
            {
                IsVisible = true,
                Width = 80,
                AggregationMode = AggregationMode.Sum,
            });

        public static bool IsDataAvailable(IDataExtensionRetrieval requiredData)
        {
            var data = requiredData.QueryOutput<List<VmFaultEvent>>(
                new DataOutputPath(VmFaultCooker.DataCookerPath, nameof(VmFaultCooker.VmFaultEvents)));
            return data != null && data.Count > 0;
        }

        public static void BuildTable(
            ITableBuilder tableBuilder,
            IDataExtensionRetrieval requiredData)
        {
            List<VmFaultEvent> data =
                requiredData.QueryOutput<List<VmFaultEvent>>(
                    new DataOutputPath(VmFaultCooker.DataCookerPath, nameof(VmFaultCooker.VmFaultEvents)));

            ITableBuilderWithRowCount tableBuilderWithRowCount = tableBuilder.SetRowCount(data.Count);

            var baseProjection = Projection.Index(data);

            var startTimeProjection = baseProjection.Compose((VmFaultEvent e) => Projector.StartTimeProjector(e));
            var stopTimeProjection = baseProjection.Compose((VmFaultEvent e) => Projector.StopTimeProjector(e));
            var durationProjection = baseProjection.Compose((VmFaultEvent e) => Projector.DurationProjector(e));
            var processProjection = baseProjection.Compose((VmFaultEvent e) => Projector.ProcessProjector(e));
            var processNameProjection = processProjection.Compose(Projector.ProcessNameProjector);
            var processIdProjection = processProjection.Compose(Projector.ProcessIdProjector);
            var threadProjection = baseProjection.Compose((VmFaultEvent e) => Projector.ThreadProjector(e));
            var threadNameProjection = threadProjection.Compose(Projector.ThreadNameProjector);
            var threadIdProjection = threadProjection.Compose(Projector.ThreadIdProjector);
            var operationProjection = baseProjection.Compose((VmFaultEvent e) => Projector.OperationProjector(e));
            var faultDurationProjection = baseProjection.Compose((VmFaultEvent e) => Projector.FaultDurationProjector(e));
            var sizeProjection = baseProjection.Compose((VmFaultEvent e) => Projector.SizeProjector(e));
            var layoutIdProjection = baseProjection.Compose((VmFaultEvent e) => Projector.LayoutIdProjector(e));

            tableBuilderWithRowCount.AddColumn(startTimeColumn, startTimeProjection);
            tableBuilderWithRowCount.AddColumn(stopTimeColumn, stopTimeProjection);
            tableBuilderWithRowCount.AddColumn(durationColumn, durationProjection);
            tableBuilderWithRowCount.AddColumn(processNameColumn, processNameProjection);
            tableBuilderWithRowCount.AddColumn(processIdColumn, processIdProjection);
            tableBuilderWithRowCount.AddColumn(threadNameColumn, threadNameProjection);
            tableBuilderWithRowCount.AddColumn(threadIdColumn, threadIdProjection);
            tableBuilderWithRowCount.AddColumn(operationColumn, operationProjection);
            tableBuilderWithRowCount.AddColumn(faultDurationColumn, faultDurationProjection);
            tableBuilderWithRowCount.AddColumn(sizeColumn, sizeProjection);
            tableBuilderWithRowCount.AddColumn(layoutIdColumn, layoutIdProjection);
            tableBuilderWithRowCount.AddColumn(cpuColumn, layoutIdProjection);
            tableBuilderWithRowCount.AddColumn(countColumn, Projection.Constant(1));

            var tableConfig = new TableConfiguration("VM Faults by Process, Thread")
            {
                Columns = new[]
                {
                    processNameColumn,
                    threadNameColumn,
                    threadIdColumn,
                    operationColumn,
                    cpuColumn,
                    TableConfiguration.PivotColumn,
                    countColumn,
                    sizeColumn,
                    faultDurationColumn,
                    durationColumn,
                    startTimeColumn,
                    TableConfiguration.GraphColumn,
                    stopTimeColumn,
                },
            };

            tableConfig.AddColumnRole(ColumnRole.StartTime, startTimeColumn);
            tableConfig.AddColumnRole(ColumnRole.EndTime, stopTimeColumn);
            tableConfig.AddColumnRole(ColumnRole.Duration, durationColumn);

            tableBuilder.AddTableConfiguration(tableConfig);
            tableBuilder.SetDefaultTableConfiguration(tableConfig);
        }
    }
}
