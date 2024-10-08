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
    public sealed class DisplayVsyncIntervalTable
    {
        public static TableDescriptor TableDescriptor =>
            new TableDescriptor(
                Guid.Parse("b3dc54b1-292d-46a9-9b59-796bdf73e372"),
                "Display Vsync Interval",
                "Events from Display Vsync Interval",
                "Display Vsync Interval Events",
                requiredDataCookers: new List<DataCookerPath>
                {
                    DisplayVsyncIntervalCooker.DataCookerPath
                });

        private static readonly ColumnConfiguration startTimeColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("56b2af73-c53e-4d76-9389-782049b0d6fc"), "Start Time"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                AggregationMode = AggregationMode.Min,
                CellFormat = TimestampFormatter.FormatMicrosecondsGrouped,
            });

        private static readonly ColumnConfiguration stopTimeColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("6c8b7b02-d3a9-4fc5-99ac-47b6eeb111c9"), "Stop Time"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                AggregationMode = AggregationMode.Max,
                CellFormat = TimestampFormatter.FormatMicrosecondsGrouped,
            });

        private static readonly ColumnConfiguration durationColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b03efb70-18e9-4058-a3bc-145f63380c18"), "Duration"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                AggregationMode = AggregationMode.Sum,
                CellFormat = TimestampFormatter.FormatMillisecondsGrouped
            });

        private static readonly ColumnConfiguration displayNameColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("6b7ef2fa-c627-4660-9ea0-1e7ea9de81f9"), "Display Name"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration colorColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("ed7d0fde-582f-47ca-a5ce-f80838ae3f33"), "Color"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration labelColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("94e07b5c-1d16-4d9b-9282-8bd79d6328cb"), "Label"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration eventColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("037c771f-e7a8-4c9e-8ee6-09e3cf3d195f"), "Event"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration countColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("05ab7008-971f-41ee-a8f6-9eb32c6b4031"), "Count"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                AggregationMode = AggregationMode.Sum
            });

        public static void BuildTable(
            ITableBuilder tableBuilder,
            IDataExtensionRetrieval requiredData
        )
        {
            List<DisplayVsyncIntervalEvent> data =
                requiredData.QueryOutput<List<DisplayVsyncIntervalEvent>>(new DataOutputPath(DisplayVsyncIntervalCooker.DataCookerPath, nameof(DisplayVsyncIntervalCooker.DisplayVsyncIntervalEvents)));
            ITableBuilderWithRowCount tableBuilderWithRowCount = tableBuilder.SetRowCount(data.Count);
            var baseProjection = Projection.Index(data);
            var startTimeProjection = baseProjection.Compose(Projector.StartTimeProjector);
            var stopTimeProjection = baseProjection.Compose(Projector.StopTimeProjector);
            var durationProjection = baseProjection.Compose(Projector.DurationProjector);
            var displayNameProjection = baseProjection.Compose(Projector.DisplayNameProjector);
            var colorProjection = baseProjection.Compose(Projector.ColorProjector);
            var labelProjection = baseProjection.Compose(Projector.LabelProjector);
            var eventProjection = baseProjection.Compose(Projector.EventProjector);

            tableBuilderWithRowCount.AddColumn(startTimeColumn, startTimeProjection);
            tableBuilderWithRowCount.AddColumn(stopTimeColumn, stopTimeProjection);
            tableBuilderWithRowCount.AddColumn(durationColumn, durationProjection);
            tableBuilderWithRowCount.AddColumn(displayNameColumn, displayNameProjection);
            tableBuilderWithRowCount.AddColumn(colorColumn, colorProjection);
            tableBuilderWithRowCount.AddColumn(labelColumn, labelProjection);
            tableBuilderWithRowCount.AddColumn(eventColumn, eventProjection);
            tableBuilderWithRowCount.AddColumn(countColumn, Projection.Constant(1));

            var tableConfig = new TableConfiguration("VSync Interval by Display Name")
            {
                Columns = new[]
                {
                    displayNameColumn,
                    TableConfiguration.PivotColumn,
                    countColumn,
                    eventColumn,
                    colorColumn,
                    durationColumn,
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