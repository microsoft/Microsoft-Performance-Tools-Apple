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
    public sealed class PcwSnapSnapshotTable
    {
        public static TableDescriptor TableDescriptor =>
           new TableDescriptor(
              Guid.Parse("{d4a1e7c3-8f2b-4c6d-9e0a-5b3f1d7c2e94}"),
              "PcwSnap Snapshots",
              "One row per pcwsnap snapshot interval",
              "PcwSnap",
              requiredDataCookers: new List<DataCookerPath>
              {
                  PcwSnapSnapshotCooker.DataCookerPath
              });

        private static readonly ColumnConfiguration indexColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("e5b2f8d4-9a3c-4d7e-0f1b-6c4a2e8d3f05"), "Snapshot #") { ShortDescription = "Sequential index of the pcwsnap snapshot" },
            new UIHints
            {
                IsVisible = true,
                Width = 80,
            });

        private static readonly ColumnConfiguration timeColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("f6c3a9e5-0b4d-4e8f-1a2c-7d5b3f9e4016"), "Timestamp") { ShortDescription = "Wall-clock time of the snapshot" },
            new UIHints
            {
                IsVisible = true,
                Width = 140,
                CellFormat = TimestampFormatter.FormatMicrosecondsGrouped,
                AggregationMode = AggregationMode.Min
            });

        private static readonly ColumnConfiguration processCountColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("a7d4b0f6-1c5e-4f9a-2b3d-8e6c4a0f5127"), "Process Count") { ShortDescription = "Number of processes captured in this snapshot" },
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                AggregationMode = AggregationMode.Max
            });

        private static readonly ColumnConfiguration kcdataSizeColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b8e5c1a7-2d6f-4a0b-3c4e-9f7d5b1a6238"), "Kcdata Size (bytes)") { ShortDescription = "Size of the raw kcdata buffer for this snapshot in bytes" },
            new UIHints
            {
                IsVisible = true,
                Width = 130,
                AggregationMode = AggregationMode.Max
            });

        private static readonly ColumnConfiguration hasPowerColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("c9f6d2b8-3e7a-4b1c-4d5f-0a8e6c2b7349"), "Has Power") { ShortDescription = "Whether this snapshot contains power/energy data (task_energy fields)" },
            new UIHints
            {
                IsVisible = true,
                Width = 80,
            });

        private static readonly ColumnConfiguration hasMarkerColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d0a7e3c9-4f8b-4c2d-5e6a-1b9f7d3c845a"), "Has Marker") { ShortDescription = "Whether this snapshot contains a user-defined marker annotation" },
            new UIHints
            {
                IsVisible = true,
                Width = 80,
            });

        private static readonly ColumnConfiguration hasFreqColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("e1b8f4d0-5a9c-4d3e-6f7b-2c0a8e4d956b"), "Has Freq") { ShortDescription = "Whether this snapshot contains CPU frequency data" },
            new UIHints
            {
                IsVisible = true,
                Width = 80,
            });

        private static readonly ColumnConfiguration freqChannelCountColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("f2c9a5e1-6b0d-4e4f-7a8c-3d1b9f5ea67c"), "Freq Channels") { ShortDescription = "Number of CPU frequency channels (cluster types) in this snapshot" },
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                AggregationMode = AggregationMode.Max
            });

        private static readonly ColumnConfiguration countColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("a3d0b6f2-7c1e-4f5a-8b9d-4e2c0a6fb78d"), "Count") { ShortDescription = "Row count (always 1), useful for aggregation" },
            new UIHints
            {
                IsVisible = true,
                Width = 60,
                AggregationMode = AggregationMode.Sum
            });

        public static bool IsDataAvailable(IDataExtensionRetrieval requiredData)
        {
            var data = requiredData.QueryOutput<List<PcwSnapSnapshotEvent>>(
                new DataOutputPath(PcwSnapSnapshotCooker.DataCookerPath, nameof(PcwSnapSnapshotCooker.Snapshots)));
            return data != null && data.Count > 0;
        }

        public static void BuildTable(
            ITableBuilder tableBuilder,
            IDataExtensionRetrieval requiredData)
        {
            List<PcwSnapSnapshotEvent> data =
                requiredData.QueryOutput<List<PcwSnapSnapshotEvent>>(
                    new DataOutputPath(PcwSnapSnapshotCooker.DataCookerPath, nameof(PcwSnapSnapshotCooker.Snapshots)));

            ITableBuilderWithRowCount tableBuilderWithRowCount = tableBuilder.SetRowCount(data.Count);

            var baseProjection = Projection.Index(data);

            var indexProjection = baseProjection.Compose(e => e.Index);
            var timeProjection = baseProjection.Compose(e => e.Timestamp);
            var processCountProjection = baseProjection.Compose(e => e.ProcessCount);
            var kcdataSizeProjection = baseProjection.Compose(e => e.KcdataSizeBytes);
            var hasPowerProjection = baseProjection.Compose(e => e.HasPowerData);
            var hasMarkerProjection = baseProjection.Compose(e => e.HasMarkerData);
            var hasFreqProjection = baseProjection.Compose(e => e.HasFreqData);
            var freqChannelCountProjection = baseProjection.Compose(e => e.FreqChannelCount);

            tableBuilderWithRowCount.AddColumn(indexColumn, indexProjection);
            tableBuilderWithRowCount.AddColumn(timeColumn, timeProjection);
            tableBuilderWithRowCount.AddColumn(processCountColumn, processCountProjection);
            tableBuilderWithRowCount.AddColumn(kcdataSizeColumn, kcdataSizeProjection);
            tableBuilderWithRowCount.AddColumn(hasPowerColumn, hasPowerProjection);
            tableBuilderWithRowCount.AddColumn(hasMarkerColumn, hasMarkerProjection);
            tableBuilderWithRowCount.AddColumn(hasFreqColumn, hasFreqProjection);
            tableBuilderWithRowCount.AddColumn(freqChannelCountColumn, freqChannelCountProjection);
            tableBuilderWithRowCount.AddColumn(countColumn, Projection.Constant(1));

            // Default: Timeline
            var tableConfig = new TableConfiguration("Timeline")
            {
                Columns = new[]
                {
                    indexColumn,
                    TableConfiguration.PivotColumn,
                    processCountColumn,
                    kcdataSizeColumn,
                    hasPowerColumn,
                    hasMarkerColumn,
                    hasFreqColumn,
                    freqChannelCountColumn,
                    countColumn,
                    TableConfiguration.GraphColumn,
                    timeColumn,
                },
            };

            tableConfig.AddColumnRole(ColumnRole.StartTime, timeColumn);
            tableConfig.AddColumnRole(ColumnRole.EndTime, timeColumn);
            tableBuilder.AddTableConfiguration(tableConfig);

            tableBuilder.SetDefaultTableConfiguration(tableConfig);
        }
    }
}
