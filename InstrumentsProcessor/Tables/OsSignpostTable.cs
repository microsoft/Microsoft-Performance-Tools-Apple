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
    public sealed class OsSignpostTable
    {
        public static TableDescriptor TableDescriptor =>
           new TableDescriptor(
              Guid.Parse("{b7e3f1a2-c4d5-4e6f-8a9b-0c1d2e3f4a5b}"),
              "OS Signpost",
              "Events from OS Signpost (Unified Logging)",
              "OS Signpost",
              requiredDataCookers: new List<DataCookerPath>
              {
              OsSignpostCooker.DataCookerPath
              });

        private static readonly ColumnConfiguration startTimeColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("c1a2b3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d"), "Start Time"),
            new UIHints
            {
                IsVisible = true,
                Width = 120,
                CellFormat = TimestampFormatter.FormatMillisecondsGrouped,
                AggregationMode = AggregationMode.Min,
            });

        private static readonly ColumnConfiguration stopTimeColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d2b3c4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e"), "Stop Time"),
            new UIHints
            {
                IsVisible = true,
                Width = 120,
                CellFormat = TimestampFormatter.FormatMillisecondsGrouped,
                AggregationMode = AggregationMode.Max,
            });

        private static readonly ColumnConfiguration durationColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("a0b1c2d3-e4f5-4a6b-7c8d-9e0f1a2b3c4d"), "Duration"),
            new UIHints
            {
                IsVisible = true,
                Width = 120,
                AggregationMode = AggregationMode.Sum,
                SortOrder = SortOrder.Descending,
                CellFormat = TimestampFormatter.FormatMillisecondsGrouped,
            });

        private static readonly ColumnConfiguration signpostNameColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("e3c4d5f6-a7b8-4c9d-0e1f-2a3b4c5d6e7f"), "Signpost Name"),
            new UIHints
            {
                IsVisible = true,
                Width = 180,
            });

        private static readonly ColumnConfiguration subsystemColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("f4d5e6a7-b8c9-4d0e-1f2a-3b4c5d6e7f80"), "Subsystem"),
            new UIHints
            {
                IsVisible = true,
                Width = 200,
            });

        private static readonly ColumnConfiguration categoryColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("a5e6f7b8-c9d0-4e1f-2a3b-4c5d6e7f8091"), "Category"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration eventNameColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b6f7a8c9-d0e1-4f2a-3b4c-5d6e7f8091a2"), "Event Name"),
            new UIHints
            {
                IsVisible = true,
                Width = 180,
            });

        private static readonly ColumnConfiguration pageColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("c7a8b9d0-e1f2-4a3b-4c5d-6e7f8091a2b3"), "Page"),
            new UIHints
            {
                IsVisible = true,
                Width = 120,
            });

        private static readonly ColumnConfiguration outerColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d8b9c0e1-f2a3-4b4c-5d6e-7f8091a2b3c4"), "Outer"),
            new UIHints
            {
                IsVisible = true,
                Width = 60,
            });

        private static readonly ColumnConfiguration instanceColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("e9c0d1f2-a3b4-4c5d-6e7f-8091a2b3c4d5"), "Instance"),
            new UIHints
            {
                IsVisible = true,
                Width = 70,
            });

        private static readonly ColumnConfiguration valueColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("f0d1e2a3-b4c5-4d6e-7f80-91a2b3c4d5e6"), "Value"),
            new UIHints
            {
                IsVisible = true,
                Width = 120,
                AggregationMode = AggregationMode.Average,
                SortOrder = SortOrder.Descending,
            });

        private static readonly ColumnConfiguration messageColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("a1e2f3b4-c5d6-4e7f-8091-a2b3c4d5e6f7"), "Message"),
            new UIHints
            {
                IsVisible = true,
                Width = 400,
            });

        private static readonly ColumnConfiguration processNameColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2f3a4c5-d6e7-4f80-91a2-b3c4d5e6f7a8"), "Process"),
            new UIHints
            {
                IsVisible = true,
                Width = 120,
            });

        private static readonly ColumnConfiguration processIdColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("c3a4b5d6-e7f8-4091-a2b3-c4d5e6f7a8b9"), "Process ID"),
            new UIHints
            {
                IsVisible = true,
                Width = 80,
            });

        private static readonly ColumnConfiguration threadNameColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d4b5c6e7-f8a9-4192-b3c4-d5e6f7a8b9c0"), "Thread"),
            new UIHints
            {
                IsVisible = true,
                Width = 120,
            });

        private static readonly ColumnConfiguration threadIdColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("e5c6d7f8-a9b0-4293-c4d5-e6f7a8b9c0d1"), "Thread ID"),
            new UIHints
            {
                IsVisible = true,
                Width = 80,
            });

        private static readonly ColumnConfiguration scopeColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("f6d7e8a9-b0c1-4394-d5e6-f7a8b9c0d1e2"), "Scope"),
            new UIHints
            {
                IsVisible = false,
                Width = 80,
            });

        private static readonly ColumnConfiguration signpostIdColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("a7e8f9b0-c1d2-4495-e6f7-a8b9c0d1e2f3"), "Signpost ID"),
            new UIHints
            {
                IsVisible = false,
                Width = 80,
            });

        private static readonly ColumnConfiguration eventTypeColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("c9a0b1d2-e3f4-4697-a8b9-c0d1e2f3a4b5"), "Event Type"),
            new UIHints
            {
                IsVisible = true,
                Width = 80,
            });

        private static readonly ColumnConfiguration countColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b8f9a0c1-d2e3-4596-f7a8-b9c0d1e2f3a4"), "Count"),
            new UIHints
            {
                IsVisible = true,
                Width = 60,
                AggregationMode = AggregationMode.Sum,
            });

        public static bool IsDataAvailable(IDataExtensionRetrieval requiredData)
        {
            var data = requiredData.QueryOutput<List<OsSignpostEvent>>(
                new DataOutputPath(OsSignpostCooker.DataCookerPath, nameof(OsSignpostCooker.OsSignpostEvents)));
            return data != null && data.Count > 0;
        }

        public static void BuildTable(
            ITableBuilder tableBuilder,
            IDataExtensionRetrieval requiredData
        )
        {
            List<OsSignpostEvent> data =
                requiredData.QueryOutput<List<OsSignpostEvent>>(new DataOutputPath(OsSignpostCooker.DataCookerPath, nameof(OsSignpostCooker.OsSignpostEvents)));

            ITableBuilderWithRowCount tableBuilderWithRowCount = tableBuilder.SetRowCount(data.Count);

            var baseProjection = Projection.Index(data);

            var startTimeProjection = baseProjection.Compose(Projector.StartTimeProjector);
            var stopTimeProjection = baseProjection.Compose(Projector.StopTimeProjector);
            var durationProjection = baseProjection.Compose(Projector.DurationProjector);
            var signpostNameProjection = baseProjection.Compose(Projector.SignpostNameProjector);
            var subsystemProjection = baseProjection.Compose(Projector.SubsystemProjector);
            var categoryProjection = baseProjection.Compose(Projector.CategoryProjector);
            var eventNameProjection = baseProjection.Compose(Projector.EventNameProjector);
            var pageProjection = baseProjection.Compose(Projector.PageProjector);
            var outerProjection = baseProjection.Compose(Projector.OuterProjector);
            var instanceProjection = baseProjection.Compose(Projector.InstanceProjector);
            var valueProjection = baseProjection.Compose(Projector.ValueProjector);
            var messageProjection = baseProjection.Compose(Projector.MessageProjector);
            var processProjection = baseProjection.Compose(Projector.ProcessProjector);
            var processNameProjection = processProjection.Compose(Projector.ProcessNameProjector);
            var processIdProjection = processProjection.Compose(Projector.ProcessIdProjector);
            var threadProjection = baseProjection.Compose(Projector.ThreadProjector);
            var threadNameProjection = threadProjection.Compose(Projector.ThreadNameProjector);
            var threadIdProjection = threadProjection.Compose(Projector.ThreadIdProjector);
            var scopeProjection = baseProjection.Compose(Projector.ScopeProjector);
            var signpostIdProjection = baseProjection.Compose(Projector.SignpostIdProjector);
            var eventTypeProjection = baseProjection.Compose(Projector.EventTypeProjector);

            tableBuilderWithRowCount.AddColumn(startTimeColumn, startTimeProjection);
            tableBuilderWithRowCount.AddColumn(stopTimeColumn, stopTimeProjection);
            tableBuilderWithRowCount.AddColumn(durationColumn, durationProjection);
            tableBuilderWithRowCount.AddColumn(signpostNameColumn, signpostNameProjection);
            tableBuilderWithRowCount.AddColumn(subsystemColumn, subsystemProjection);
            tableBuilderWithRowCount.AddColumn(categoryColumn, categoryProjection);
            tableBuilderWithRowCount.AddColumn(eventNameColumn, eventNameProjection);
            tableBuilderWithRowCount.AddColumn(pageColumn, pageProjection);
            tableBuilderWithRowCount.AddColumn(outerColumn, outerProjection);
            tableBuilderWithRowCount.AddColumn(instanceColumn, instanceProjection);
            tableBuilderWithRowCount.AddColumn(valueColumn, valueProjection);
            tableBuilderWithRowCount.AddColumn(messageColumn, messageProjection);
            tableBuilderWithRowCount.AddColumn(processNameColumn, processNameProjection);
            tableBuilderWithRowCount.AddColumn(processIdColumn, processIdProjection);
            tableBuilderWithRowCount.AddColumn(threadNameColumn, threadNameProjection);
            tableBuilderWithRowCount.AddColumn(threadIdColumn, threadIdProjection);
            tableBuilderWithRowCount.AddColumn(scopeColumn, scopeProjection);
            tableBuilderWithRowCount.AddColumn(signpostIdColumn, signpostIdProjection);
            tableBuilderWithRowCount.AddColumn(eventTypeColumn, eventTypeProjection);
            tableBuilderWithRowCount.AddColumn(countColumn, Projection.Constant(1));

            var tableConfig = new TableConfiguration("OS Signpost by Event Name")
            {
                Columns = new[]
                {
                    eventNameColumn,
                    pageColumn,
                    outerColumn,
                    instanceColumn,
                    TableConfiguration.PivotColumn,
                    countColumn,
                    valueColumn,
                    durationColumn,
                    messageColumn,
                    TableConfiguration.GraphColumn,
                    startTimeColumn,
                    stopTimeColumn,
                },
            };

            tableConfig.AddColumnRole(ColumnRole.StartTime, startTimeColumn);
            tableConfig.AddColumnRole(ColumnRole.Duration, durationColumn);

            tableBuilder.AddTableConfiguration(tableConfig);
            tableBuilder.SetDefaultTableConfiguration(tableConfig);
        }
    }
}
