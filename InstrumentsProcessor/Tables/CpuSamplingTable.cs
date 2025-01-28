// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using InstrumentsProcessor.AccessProviders;
using InstrumentsProcessor.Cookers;
using InstrumentsProcessor.Parsing.Events;
using Microsoft.Performance.SDK;
using Microsoft.Performance.SDK.Extensibility;
using Microsoft.Performance.SDK.Processing;
using Microsoft.Performance.SDK.Processing.ColumnBuilding;
using System;
using System.Collections.Generic;

using Backtrace = InstrumentsProcessor.Parsing.DataModels.Backtrace;

namespace InstrumentsProcessor.Tables
{
    [Table]
    public sealed class CpuSamplingTable
    {
        public static TableDescriptor TableDescriptor =>
           new TableDescriptor(
              Guid.Parse("{13592e58-8ed7-4ea9-a51e-ffb124ab052b}"),
              "CPU Usage (Sampled)",
              "Events from Time Profile",
              "Time Profile",
              requiredDataCookers: new List<DataCookerPath>
              {
              TimeProfileCooker.DataCookerPath
              });

        private static readonly ColumnConfiguration timeStampColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b6cc607d-c420-4dfe-846d-ec9c5e09b281"), "TimeStamp"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration stackColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("c0b3dfde-7693-4dc8-bdab-9eb32189e545"), "Stack"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration moduleColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("374b0856-7742-4509-84b5-36e90c1bcfe5"), "Module"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration functionColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("cf06ada3-1678-4fb6-8ee6-c2f26be99424"), "Function"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration threadIdColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("2ead4e1c-d9c8-427c-b9eb-23e5f17e5ad3"), "Thread ID"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration processIdColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("e4b7e1a5-5bae-4157-b9d7-36c58cbeab28"), "Process ID"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration processNameColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b1e5d6cf-8147-42f7-9bd6-728ce6ea4476"), "Process"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration deviceSessionColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("271339a9-3b5d-4d20-9512-13f319ca9559"), "Device Session"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration cpuColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("3a78ac08-6126-41e3-a7e5-27b8537613f6"), "CPU"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration processorClassColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d42d296e-e1cf-4610-a54f-f7bacd1426ce"), "Processor Class"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration stateColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("8f79c3c0-e2af-4a88-bba4-1a69ba8ac7f7"), "State"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration weightColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("10529ffc-769d-477e-909a-aadb8147ef09"), "Weight"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                AggregationMode = AggregationMode.Sum,
                CellFormat = TimestampFormatter.FormatMillisecondsGrouped
            });

        private static readonly ColumnConfiguration countPreset = new ColumnConfiguration(
            new ColumnMetadata(new Guid("a87251f6-25f8-4988-9f36-99ebdb1217a7"), "Count"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                AggregationMode = AggregationMode.Sum
            });

        private static readonly ColumnConfiguration weightViewportPreset = new ColumnConfiguration(
            new ColumnMetadata(new Guid("a23ad51e-ec9f-496f-aba3-98b35d975043"), "Weight (in view)"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                AggregationMode = AggregationMode.Sum,
                CellFormat = "mN",
            });

        private static readonly ColumnConfiguration weightPercentPreset = new ColumnConfiguration(
            new ColumnMetadata(new Guid("f93f3441-5ac0-4447-a2d0-776f62d9f5cf"), "% Weight") { IsPercent = true,
                ShortDescription = "Weight is expressed as a percentage of total CPU time that is spent over the currently visible time range" },
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                AggregationMode = AggregationMode.Sum,
                SortOrder = SortOrder.Descending,
                CellFormat = "N2",
            });

        private static readonly ColumnVariantProperties baseProperties = new ColumnVariantProperties()
        {
            Label = "Stack frames",
            ColumnName = "Stack",
        };

        private static ColumnVariantDescriptor invertedDescriptor = new ColumnVariantDescriptor(
                new Guid("94b3e830-d188-451a-96fc-694dfe49f01f"),
                new ColumnVariantProperties
                {
                    Label = "Invert",
                    ColumnName = $"{baseProperties.ColumnName} (Inverted)",
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
            List<TimeProfileEvent> data =
                requiredData.QueryOutput<List<TimeProfileEvent>>(new DataOutputPath(TimeProfileCooker.DataCookerPath, nameof(TimeProfileCooker.TimeProfileEvents)));

            ITableBuilderWithRowCount tableBuilderWithRowCount = tableBuilder.SetRowCount(data.Count);
            StackAccessProvider stackAccessProvider = new StackAccessProvider();
            var baseProjection = Projection.Index(data);

            var timeStampProjection = baseProjection.Compose(Projector.TimeStampProjector);
            var threadProjection = baseProjection.Compose(Projector.ThreadProjector);
            var threadIdProjection = threadProjection.Compose(Projector.ThreadIdProjector);
            var processProjection = baseProjection.Compose(Projector.ProcessProjector);
            var processIdProjection = processProjection.Compose(Projector.ProcessIdProjector);
            var processNameProjection = processProjection.Compose(Projector.ProcessNameProjector);
            var deviceSessionProjection = processProjection.Compose(Projector.DeviceSessionProjector);
            var cpuProjection = baseProjection.Compose(Projector.CpuProjector);
            var processorClassProjection = baseProjection.Compose(Projector.ProcessorClassProjector);
            var stackProjection = baseProjection.Compose(Projector.StackProjector);
            var moduleProjection = stackProjection.Compose(Projector.ModuleProjector);
            var functionProjection = stackProjection.Compose(Projector.FunctionProjector);
            var stateProjection = baseProjection.Compose(Projector.StateProjector);
            var weightProjection = baseProjection.Compose(Projector.WeightProjector);

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
            tableBuilderWithRowCount.AddColumn(processorClassColumn, processorClassProjection);
            tableBuilderWithRowCount.AddColumn(stateColumn, stateProjection);
            tableBuilderWithRowCount.AddColumn(weightColumn, weightProjection);
            tableBuilderWithRowCount.AddHierarchicalColumnWithVariants(stackColumn,
                stackProjection, stackAccessProvider, builder =>
                {
                    return builder
                        .WithModes(
                            baseProperties,
                            modeBuilder =>
                            {
                                return modeBuilder.WithHierarchicalToggle(
                                    invertedDescriptor,
                                    stackProjection,
                                    new InvertedCollectionAccessProvider<StackAccessProvider, Backtrace, string>(stackAccessProvider));
                            });
                });
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