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
    public sealed class AneHwIntervalTable
    {
        public static TableDescriptor TableDescriptor =>
            new TableDescriptor(
                Guid.Parse("{8f3a2b1c-4d5e-6f7a-8b9c-0d1e2f3a4b5c}"),
                "Neural Engine",
                "Apple Neural Engine Hardware Interval Events",
                "Neural Engine",
                requiredDataCookers: new List<DataCookerPath>
                {
                    AneHwIntervalCooker.DataCookerPath
                });

        private static readonly ColumnConfiguration startTimeColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("{a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d}"), "Start Time"),
            new UIHints
            {
                IsVisible = true,
                Width = 120,
                AggregationMode = AggregationMode.Min,
                CellFormat = TimestampFormatter.FormatMicrosecondsGrouped,
            });

        private static readonly ColumnConfiguration stopTimeColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("{b2c3d4e5-f6a7-8b9c-0d1e-2f3a4b5c6d7e}"), "Stop Time"),
            new UIHints
            {
                IsVisible = true,
                Width = 120,
                AggregationMode = AggregationMode.Max,
                CellFormat = TimestampFormatter.FormatMicrosecondsGrouped,
            });

        private static readonly ColumnConfiguration durationColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("{c3d4e5f6-a7b8-9c0d-1e2f-3a4b5c6d7e8f}"), "Duration"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                AggregationMode = AggregationMode.Sum,
                CellFormat = TimestampFormatter.FormatMillisecondsGrouped,
            });

        private static readonly ColumnConfiguration channelNameColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("{d4e5f6a7-b8c9-0d1e-2f3a-4b5c6d7e8f9a}"), "Channel Name"),
            new UIHints
            {
                IsVisible = true,
                Width = 150,
            });

        private static readonly ColumnConfiguration depthColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("{e5f6a7b8-c9d0-1e2f-3a4b-5c6d7e8f9a0b}"), "Depth"),
            new UIHints
            {
                IsVisible = true,
                Width = 80,
            });

        private static readonly ColumnConfiguration labelColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("{f6a7b8c9-d0e1-2f3a-4b5c-6d7e8f9a0b1c}"), "Label"),
            new UIHints
            {
                IsVisible = true,
                Width = 250,
            });

        private static readonly ColumnConfiguration stateColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("{a7b8c9d0-e1f2-3a4b-5c6d-7e8f9a0b1c2d}"), "State"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration colorColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("{b8c9d0e1-f2a3-4b5c-6d7e-8f9a0b1c2d3e}"), "Color"),
            new UIHints
            {
                IsVisible = true,
                Width = 80,
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
            List<AneHwIntervalEvent> data =
                requiredData.QueryOutput<List<AneHwIntervalEvent>>(new DataOutputPath(AneHwIntervalCooker.DataCookerPath,
                nameof(AneHwIntervalCooker.AneHwIntervalEvents)));

            ITableBuilderWithRowCount tableBuilderWithRowCount = tableBuilder.SetRowCount(data.Count);

            var baseProjection = Projection.Index(data);
            var startTimeProjection = baseProjection.Compose((AneHwIntervalEvent e) => Projector.StartTimeProjector(e));
            var stopTimeProjection = baseProjection.Compose((AneHwIntervalEvent e) => Projector.StopTimeProjector(e));
            var durationProjection = baseProjection.Compose((AneHwIntervalEvent e) => Projector.DurationProjector(e));
            var channelNameProjection = baseProjection.Compose((AneHwIntervalEvent e) => Projector.ChannelNameProjector(e));
            var depthProjection = baseProjection.Compose((AneHwIntervalEvent e) => Projector.DepthProjector(e));
            var labelProjection = baseProjection.Compose((AneHwIntervalEvent e) => Projector.LabelProjector(e));
            var stateProjection = baseProjection.Compose((AneHwIntervalEvent e) => Projector.StateProjector(e));
            var colorProjection = baseProjection.Compose((AneHwIntervalEvent e) => Projector.ColorProjector(e));

            tableBuilderWithRowCount.AddColumn(startTimeColumn, startTimeProjection);
            tableBuilderWithRowCount.AddColumn(stopTimeColumn, stopTimeProjection);
            tableBuilderWithRowCount.AddColumn(durationColumn, durationProjection);
            tableBuilderWithRowCount.AddColumn(channelNameColumn, channelNameProjection);
            tableBuilderWithRowCount.AddColumn(depthColumn, depthProjection);
            tableBuilderWithRowCount.AddColumn(labelColumn, labelProjection);
            tableBuilderWithRowCount.AddColumn(stateColumn, stateProjection);
            tableBuilderWithRowCount.AddColumn(colorColumn, colorProjection);

            var tableConfig = new TableConfiguration("ANE Activity")
            {
                Columns = new[]
                {
                    channelNameColumn,
                    labelColumn,
                    stateColumn,
                    TableConfiguration.PivotColumn,
                    durationColumn,
                    TableConfiguration.GraphColumn,
                    startTimeColumn,
                    stopTimeColumn,
                },
            };

            tableConfig.AddColumnRole(ColumnRole.StartTime, startTimeColumn);
            tableConfig.AddColumnRole(ColumnRole.EndTime, stopTimeColumn);

            tableBuilder.AddTableConfiguration(tableConfig);
            tableBuilder.SetDefaultTableConfiguration(tableConfig);
        }
    }
}
