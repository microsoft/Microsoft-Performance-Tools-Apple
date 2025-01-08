// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using InstrumentsProcessor.AccessProviders;
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
    public sealed class CountersProfileTable
    {
        public static TableDescriptor TableDescriptor =>
           new TableDescriptor(
              Guid.Parse("{1BBF6974-CA72-4177-B950-68F282B04BE2}"),
              "Counters Profile",
              "Events from Counters Profile",
              "Counters Profile Events",
              requiredDataCookers: new List<DataCookerPath>
              {
              CountersProfileCooker.DataCookerPath
              });

        private static readonly ColumnConfiguration timeStampColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("39D4952C-B77E-4724-9126-D707A8FA7C5E"), "TimeStamp"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration stackColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("9CCE7BA2-8C45-4CA0-BFDE-C1A55EF94FEA"), "Stack"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration moduleColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("3F67A997-5A8E-45F2-B05F-349E4DC001B3"), "Module"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration functionColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("66EE77C1-94A8-4AA4-88A4-D73D7CFF3CB1"), "Function"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration threadIdColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("42A0AD3A-B6B7-4B14-9D61-CE8202E49EBE"), "Thread ID"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration processIdColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("A9B312FC-7E20-4122-9A94-3E6157A9CBF2"), "Process ID"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration processNameColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("FE8513A1-252B-4BBE-8154-F07A39178CFF"), "Process"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration deviceSessionColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("6E1D538D-CAFC-4C12-8D65-7EEF9DB8F703"), "Device Session"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration cpuColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("7E2C3825-A088-4166-8E5E-FFFCC1CD8560"), "CPU"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration weightColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("E955D14D-C1FC-4741-9419-F9FD5314567B"), "Weight"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                AggregationMode = AggregationMode.Sum,
                CellFormat = TimestampFormatter.FormatMillisecondsGrouped
            });

        private static readonly ColumnConfiguration columnOneColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("1B5EAEB9-0ADF-49E9-8850-FF676DDF4DFD"), "Col1"),
            new UIHints
            {
                IsVisible = true,
                AggregationMode = AggregationMode.Sum,
                Width = 100,
            });

        private static readonly ColumnConfiguration columnTwoColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("84D78E73-54F6-4735-AFC8-49BF646B4BE1"), "Col2"),
            new UIHints
            {
                IsVisible = true,
                AggregationMode = AggregationMode.Sum,
                Width = 100,
            });

        private static readonly ColumnConfiguration countPreset = new ColumnConfiguration(
            new ColumnMetadata(new Guid("CA3DB955-E194-4790-8EBB-A5B630376189"), "Count"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                AggregationMode = AggregationMode.Sum
            });

        private static readonly ColumnConfiguration weightViewportPreset = new ColumnConfiguration(
            new ColumnMetadata(new Guid("4B5A0F97-4F03-46B1-9CBC-97C9B92B8C88"), "Weight (in view)"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                AggregationMode = AggregationMode.Sum,
                CellFormat = "mN",
            });

        private static readonly ColumnConfiguration weightPercentPreset = new ColumnConfiguration(
            new ColumnMetadata(new Guid("0C0FCE58-1904-40A5-B48D-D9CA74ADF163"), "% Weight") { IsPercent = true, ShortDescription = "Weight is expressed as a percentage of total CPU time that is spent over the currently visible time range" },
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                AggregationMode = AggregationMode.Sum,
                SortOrder = SortOrder.Descending,
                CellFormat = "N2",
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
            List<CountersProfileEvent> data =
                requiredData.QueryOutput<List<CountersProfileEvent>>(new DataOutputPath(CountersProfileCooker.DataCookerPath, nameof(CountersProfileCooker.CountersProfileEvent)));

            ITableBuilderWithRowCount tableBuilderWithRowCount = tableBuilder.SetRowCount(data.Count);

            var baseProjection = Projection.Index(data);

            var timeStampProjection = baseProjection.Compose(Projector.TimeStampProjector);
            var threadProjection = baseProjection.Compose(Projector.ThreadProjector);
            var threadIdProjection = threadProjection.Compose(Projector.ThreadIdProjector);
            var processProjection = baseProjection.Compose(Projector.ProcessProjector);
            var processIdProjection = processProjection.Compose(Projector.ProcessIdProjector);
            var processNameProjection = processProjection.Compose(Projector.ProcessNameProjector);
            var deviceSessionProjection = processProjection.Compose(Projector.DeviceSessionProjector);
            var cpuProjection = baseProjection.Compose(Projector.CpuProjector);
            var stackProjection = baseProjection.Compose(Projector.StackProjector);
            var moduleProjection = stackProjection.Compose(Projector.ModuleProjector);
            var functionProjection = stackProjection.Compose(Projector.FunctionProjector);
            var weightProjection = baseProjection.Compose(Projector.WeightProjector);
            var columnOneProjection = baseProjection.Compose(Projector.ColumnOneProjector);
            var columnTwoProjection = baseProjection.Compose(Projector.ColumnTwoProjector);

            var startTimeProjection = Projection.Select(timeStampProjection, weightProjection, new ReduceTimeMinusDelta());
            var viewportClippedStartTimeProjection =
                Projection.ClipTimeToVisibleDomain.Create(startTimeProjection);
            var viewportClippedEndTimeProjection =
                Projection.ClipTimeToVisibleDomain.Create(timeStampProjection);
            var clippedWeightColumn = Projection.Select(
                viewportClippedEndTimeProjection,
                viewportClippedStartTimeProjection,
                new ReduceTimeSinceLastDiff());
            var weightPercentProjection =
                Projection.VisibleDomainRelativePercent.Create(clippedWeightColumn);

            tableBuilderWithRowCount.AddColumn(timeStampColumn, timeStampProjection);
            tableBuilderWithRowCount.AddColumn(threadIdColumn, threadIdProjection);
            tableBuilderWithRowCount.AddColumn(processIdColumn, processIdProjection);
            tableBuilderWithRowCount.AddColumn(processNameColumn, processNameProjection);
            tableBuilderWithRowCount.AddColumn(deviceSessionColumn, deviceSessionProjection);
            tableBuilderWithRowCount.AddColumn(cpuColumn, cpuProjection);
            tableBuilderWithRowCount.AddColumn(weightColumn, weightProjection);
            tableBuilderWithRowCount.AddColumn(columnOneColumn, columnOneProjection);
            tableBuilderWithRowCount.AddColumn(columnTwoColumn, columnTwoProjection);
            tableBuilderWithRowCount.AddHierarchicalColumn(stackColumn,
                    stackProjection, new StackAccessProvider());
            tableBuilderWithRowCount.AddColumn(moduleColumn, moduleProjection);
            tableBuilderWithRowCount.AddColumn(functionColumn, functionProjection);
            tableBuilderWithRowCount.AddColumn(countPreset, Projection.Constant(1));
            tableBuilderWithRowCount.AddColumn(weightViewportPreset, clippedWeightColumn);
            tableBuilderWithRowCount.AddColumn(weightPercentPreset, weightPercentProjection);

            var tableConfig = new TableConfiguration("Utilization by Process, Stack")
            {
                Columns = new[]
                {
                    processNameColumn,
                    stackColumn,
                    TableConfiguration.PivotColumn,
                    countPreset,
                    columnOneColumn,
                    columnTwoColumn,
                    weightViewportPreset,
                    timeStampColumn,
                    TableConfiguration.GraphColumn,
                    weightPercentPreset
                },
            };

            tableConfig.AddColumnRole(ColumnRole.StartTime, timeStampColumn);
            tableConfig.AddColumnRole(ColumnRole.Duration, weightColumn);

            tableBuilder.AddTableConfiguration(tableConfig);
            tableBuilder.SetDefaultTableConfiguration(tableConfig);
        }

        private struct ReduceTimeMinusDelta
            : IFunc<int, Timestamp, TimestampDelta, Timestamp>
        {
            public Timestamp Invoke(int value, Timestamp timestamp, TimestampDelta delta)
            {
                return timestamp - delta;
            }
        }

        private struct ReduceTimeSinceLastDiff
            : IFunc<int, Timestamp, Timestamp, TimestampDelta>
        {
            public TimestampDelta Invoke(int value, Timestamp timeSinceLast1, Timestamp timeSinceLast2)
            {
                return timeSinceLast1 - timeSinceLast2;
            }
        }
    }
}