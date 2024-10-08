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
    public sealed class PotentialHangTable
    {
        public static TableDescriptor TableDescriptor =>
           new TableDescriptor(
              Guid.Parse("{afd19aae-72ed-41ae-b963-f06bca70b2e0}"),
              "Potential Hangs",
              "Events from Potential Hangs",
              "Potential Hang Events",
              requiredDataCookers: new List<DataCookerPath>
              {
              PotentialHangCooker.DataCookerPath
              });

        private static readonly ColumnConfiguration startTimeColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("583000f1-9282-4ac5-93f4-d19f5a2130b0"), "Start Time"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                CellFormat = TimestampFormatter.FormatMicrosecondsGrouped,
                AggregationMode = AggregationMode.Min,
            });

        private static readonly ColumnConfiguration stopTimeColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("f6414f21-27b4-44c7-ad30-4c41026670cc"), "Stop Time"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                CellFormat = TimestampFormatter.FormatMicrosecondsGrouped,
                AggregationMode = AggregationMode.Min,
            });

        private static readonly ColumnConfiguration durationColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("c1a29c53-c252-4123-8a79-bbd375dda0e1"), "Duration"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                AggregationMode = AggregationMode.Sum,
                SortOrder = SortOrder.Descending,
                CellFormat = TimestampFormatter.FormatMillisecondsGrouped
            });

        private static readonly ColumnConfiguration hangTypeColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("7643192d-c115-41fc-9dff-7e894910c1d2"), "Hang Type"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration threadIdColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("26ffa309-bf14-40e0-b0ec-1ad19af1bdba"), "Thread ID"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration processIdColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("6cd9ea2a-e2ec-418f-b084-9b38fec15224"), "Process ID"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration processNameColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("a7d421f6-fb16-492f-b9c1-62aca05ce69b"), "Process"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration deviceSessionColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("28288907-b697-45da-af1e-738f39b0d1f7"), "Device Session"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration countPreset = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b218e29e-c397-4774-bb2f-b543029cc883"), "Count"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
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
            List<PotentialHangEvent> data =
                requiredData.QueryOutput<List<PotentialHangEvent>>(new DataOutputPath(PotentialHangCooker.DataCookerPath, nameof(PotentialHangCooker.PotentialHangEvents)));

            ITableBuilderWithRowCount tableBuilderWithRowCount = tableBuilder.SetRowCount(data.Count);

            var baseProjection = Projection.Index(data);

            var startTimeProjection = baseProjection.Compose(Projector.StartTimeProjector);
            var stopTimeProjection = baseProjection.Compose(Projector.StopTimeProjector);
            var durationProjection = baseProjection.Compose(Projector.DurationProjector);
            var hangTypeProjection = baseProjection.Compose(Projector.HangTypeProjector);
            var threadProjection = baseProjection.Compose(Projector.ThreadProjector);
            var threadIdProjection = threadProjection.Compose(Projector.ThreadIdProjector);
            var processProjection = baseProjection.Compose(Projector.ProcessProjector);
            var processIdProjection = processProjection.Compose(Projector.ProcessIdProjector);
            var processNameProjection = processProjection.Compose(Projector.ProcessNameProjector);
            var deviceSessionProjection = processProjection.Compose(Projector.DeviceSessionProjector);

            tableBuilderWithRowCount.AddColumn(startTimeColumn, startTimeProjection);
            tableBuilderWithRowCount.AddColumn(stopTimeColumn, stopTimeProjection);
            tableBuilderWithRowCount.AddColumn(durationColumn, durationProjection);
            tableBuilderWithRowCount.AddColumn(hangTypeColumn, hangTypeProjection);
            tableBuilderWithRowCount.AddColumn(threadIdColumn, threadIdProjection);
            tableBuilderWithRowCount.AddColumn(processIdColumn, processIdProjection);
            tableBuilderWithRowCount.AddColumn(processNameColumn, processNameProjection);
            tableBuilderWithRowCount.AddColumn(deviceSessionColumn, deviceSessionProjection);
            tableBuilderWithRowCount.AddColumn(countPreset, Projection.Constant(1));

            var tableConfig = new TableConfiguration("Potential Hangs by Process, Thread")
            {
                Columns = new[]
                {
                    processNameColumn,
                    threadIdColumn,
                    TableConfiguration.PivotColumn,
                    countPreset,
                    hangTypeColumn,
                    durationColumn,
                    TableConfiguration.GraphColumn,
                    startTimeColumn,
                    stopTimeColumn
                },
            };

            tableConfig.AddColumnRole(ColumnRole.StartTime, startTimeColumn);
            tableConfig.AddColumnRole(ColumnRole.Duration, durationColumn);

            tableBuilder.AddTableConfiguration(tableConfig);
            tableBuilder.SetDefaultTableConfiguration(tableConfig);
        }
    }
}