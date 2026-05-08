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
    public sealed class LifeCyclePeriodTable
    {
        public static TableDescriptor TableDescriptor =>
           new TableDescriptor(
              Guid.Parse("{f3a1b2c4-d5e6-47f8-9a0b-c1d2e3f4a5b6}"),
              "App Lifecycle Periods",
              "Identifies where an application is in its lifecycle",
              "App Lifecycle",
              requiredDataCookers: new List<DataCookerPath>
              {
              LifeCyclePeriodCooker.DataCookerPath
              });

        private static readonly ColumnConfiguration startTimeColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("a1b2c3d4-e5f6-4718-8a9b-0c1d2e3f4a5b"), "Start Time"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                CellFormat = TimestampFormatter.FormatMicrosecondsGrouped,
                AggregationMode = AggregationMode.Min,
            });

        private static readonly ColumnConfiguration stopTimeColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2c3d4e5-f6a7-4829-9b0c-1d2e3f4a5b6c"), "Stop Time"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                CellFormat = TimestampFormatter.FormatMicrosecondsGrouped,
                AggregationMode = AggregationMode.Max,
            });

        private static readonly ColumnConfiguration durationColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("c3d4e5f6-a7b8-493a-0b1c-2d3e4f5a6b7c"), "Duration"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                AggregationMode = AggregationMode.Sum,
                SortOrder = SortOrder.Descending,
                CellFormat = TimestampFormatter.FormatMillisecondsGrouped
            });

        private static readonly ColumnConfiguration groupColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d4e5f6a7-b8c9-4a4b-1c2d-3e4f5a6b7c8d"), "Group"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration layoutIdColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("e5f6a7b8-c9d0-4b5c-2d3e-4f5a6b7c8d9e"), "Layout ID"),
            new UIHints
            {
                IsVisible = true,
                Width = 80,
            });

        private static readonly ColumnConfiguration processIdColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("f6a7b8c9-d0e1-4c6d-3e4f-5a6b7c8d9e0f"), "Process ID"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration processNameColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("a7b8c9d0-e1f2-4d7e-4f5a-6b7c8d9e0f1a"), "Process"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration deviceSessionColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b8c9d0e1-f2a3-4e8f-5a6b-7c8d9e0f1a2b"), "Device Session"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration lifecyclePeriodColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("c9d0e1f2-a3b4-4f90-6b7c-8d9e0f1a2b3c"), "Lifecycle Period"),
            new UIHints
            {
                IsVisible = true,
                Width = 200,
            });

        private static readonly ColumnConfiguration narrativeColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d0e1f2a3-b4c5-4a01-7c8d-9e0f1a2b3c4d"), "Narrative"),
            new UIHints
            {
                IsVisible = true,
                Width = 300,
            });

        private static readonly ColumnConfiguration countColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("e1f2a3b4-c5d6-4b12-8d9e-0f1a2b3c4d5e"), "Count"),
            new UIHints
            {
                IsVisible = true,
                Width = 80,
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
            List<LifeCyclePeriodEvent> data =
                requiredData.QueryOutput<List<LifeCyclePeriodEvent>>(new DataOutputPath(LifeCyclePeriodCooker.DataCookerPath, nameof(LifeCyclePeriodCooker.LifeCyclePeriodEvents)));

            ITableBuilderWithRowCount tableBuilderWithRowCount = tableBuilder.SetRowCount(data.Count);

            var baseProjection = Projection.Index(data);

            var startTimeProjection = baseProjection.Compose(Projector.StartTimeProjector);
            var stopTimeProjection = baseProjection.Compose(Projector.StopTimeProjector);
            var durationProjection = baseProjection.Compose(Projector.DurationProjector);
            var groupProjection = baseProjection.Compose(Projector.GroupProjector);
            var layoutIdProjection = baseProjection.Compose(Projector.LayoutIdProjector);
            var processProjection = baseProjection.Compose(Projector.ProcessProjector);
            var processIdProjection = processProjection.Compose(Projector.ProcessIdProjector);
            var processNameProjection = processProjection.Compose(Projector.ProcessNameProjector);
            var deviceSessionProjection = processProjection.Compose(Projector.DeviceSessionProjector);
            var lifecyclePeriodProjection = baseProjection.Compose(Projector.LifecyclePeriodProjector);
            var narrativeProjection = baseProjection.Compose(Projector.NarrativeProjector);

            tableBuilderWithRowCount.AddColumn(startTimeColumn, startTimeProjection);
            tableBuilderWithRowCount.AddColumn(stopTimeColumn, stopTimeProjection);
            tableBuilderWithRowCount.AddColumn(durationColumn, durationProjection);
            tableBuilderWithRowCount.AddColumn(groupColumn, groupProjection);
            tableBuilderWithRowCount.AddColumn(layoutIdColumn, layoutIdProjection);
            tableBuilderWithRowCount.AddColumn(processIdColumn, processIdProjection);
            tableBuilderWithRowCount.AddColumn(processNameColumn, processNameProjection);
            tableBuilderWithRowCount.AddColumn(deviceSessionColumn, deviceSessionProjection);
            tableBuilderWithRowCount.AddColumn(lifecyclePeriodColumn, lifecyclePeriodProjection);
            tableBuilderWithRowCount.AddColumn(narrativeColumn, narrativeProjection);
            tableBuilderWithRowCount.AddColumn(countColumn, Projection.Constant(1));

            var tableConfig = new TableConfiguration("Lifecycle Periods by Process")
            {
                Columns = new[]
                {
                    processNameColumn,
                    lifecyclePeriodColumn,
                    groupColumn,
                    TableConfiguration.PivotColumn,
                    countColumn,
                    narrativeColumn,
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
