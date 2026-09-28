// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using InstrumentsProcessor.AccessProviders;
using InstrumentsProcessor.Cookers;
using InstrumentsProcessor.Parsing.DataModels;
using InstrumentsProcessor.Parsing.Events;
using Microsoft.Performance.SDK;
using Microsoft.Performance.SDK.Extensibility;
using Microsoft.Performance.SDK.Processing;
using Microsoft.Performance.SDK.Processing.ColumnBuilding;
using System;
using System.Collections.Generic;
using Timestamp = Microsoft.Performance.SDK.Timestamp;
using TimestampDelta = Microsoft.Performance.SDK.TimestampDelta;

namespace InstrumentsProcessor.Tables
{
    [Table]
    public sealed class CswitchIntervalTable
    {
        public static TableDescriptor TableDescriptor =>
           new TableDescriptor(
              Guid.Parse("{7a2c8e4f-1b3d-4f5a-9e6c-0d8f2a4b6c7e}"),
              "Context Switch Interval",
              "Events from Context Switch Interval",
              "Context Switch",
              requiredDataCookers: new List<DataCookerPath>
              {
              CswitchIntervalCooker.DataCookerPath
              });

        private static readonly ColumnConfiguration startTimeColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d"), "Start Time"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                CellFormat = TimestampFormatter.FormatMicrosecondsGrouped,
                AggregationMode = AggregationMode.Min
            });

        private static readonly ColumnConfiguration stopTimeColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e"), "Stop Time"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                CellFormat = TimestampFormatter.FormatMicrosecondsGrouped,
                AggregationMode = AggregationMode.Max
            });

        private static readonly ColumnConfiguration durationColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("c3d4e5f6-a7b8-4c9d-0e1f-2a3b4c5d6e7f"), "Duration"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                AggregationMode = AggregationMode.Sum,
                CellFormat = TimestampFormatter.FormatMillisecondsGrouped
            });

        private static readonly ColumnConfiguration processNameColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d4e5f6a7-b8c9-4d0e-1f2a-3b4c5d6e7f80"), "Process"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration processIdColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("e5f6a7b8-c9d0-4e1f-2a3b-4c5d6e7f8091"), "Process ID"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration threadNameColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("f6a7b8c9-d0e1-4f2a-3b4c-5d6e7f809102"), "Thread Name"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration threadIdColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("a7b8c9d0-e1f2-4a3b-4c5d-6e7f80910213"), "Thread ID"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration cpuColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b8c9d0e1-f2a3-4b4c-5d6e-7f8091021324"), "CPU"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration deltaInstructionsColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("c9d0e1f2-a3b4-4c5d-6e7f-809102132435"), "Δ Instructions"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                AggregationMode = AggregationMode.Sum
            });

        private static readonly ColumnConfiguration deltaCyclesColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d0e1f2a3-b4c5-4d6e-7f80-910213243546"), "Δ Cycles"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                AggregationMode = AggregationMode.Sum
            });

        private static readonly ColumnConfiguration deltaTimeColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("e1f2a3b4-c5d6-4e7f-8091-021324354657"), "Δ Time"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                CellFormat = TimestampFormatter.FormatMillisecondsGrouped,
                AggregationMode = AggregationMode.Sum
            });

        private static readonly ColumnConfiguration switchInStackColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("f2a3b4c5-d6e7-4f80-9102-132435465768"), "Switch-In Stack"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration switchInKernelStackColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("a3b4c5d6-e7f8-4091-0213-243546576879"), "Switch-In Kernel Stack"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration countColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b4c5d6e7-f809-4102-1324-35465768798a"), "Count"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                AggregationMode = AggregationMode.Sum
            });

        private static readonly ColumnConfiguration cpuUsageInViewportColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("c5d6e7f8-0910-4213-2435-4657687989ab"), "CPU Usage (in view)") { ShortDescription = "The time the thread switching in spends switched in" },
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                CellFormat = TimestampFormatter.FormatMillisecondsGrouped,
                AggregationMode = AggregationMode.Sum
            });

        private static readonly ColumnConfiguration percentCpuUsageColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d6e7f809-1021-4324-3546-57687989ab0c"), "% CPU Usage") { IsPercent = true, ShortDescription = "Percentage of the total visible processor that was spent by the thread switching in" },
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                SortOrder = SortOrder.Descending,
                CellFormat = ColumnFormats.PercentFormat,
                AggregationMode = AggregationMode.Sum
            });

        private static readonly ColumnVariantProperties baseProperties = new ColumnVariantProperties()
        {
            Label = "Stack frames",
            ColumnName = "Switch-In Stack",
        };

        private static ColumnVariantDescriptor invertedDescriptor = new ColumnVariantDescriptor(
                new Guid("e7f80910-2132-4435-4657-687989ab0c1d"),
                new ColumnVariantProperties
                {
                    Label = "Invert",
                    ColumnName = $"{baseProperties.ColumnName} (Inverted)",
                });

        private static readonly ColumnVariantProperties kernelBaseProperties = new ColumnVariantProperties()
        {
            Label = "Stack frames",
            ColumnName = "Switch-In Kernel Stack",
        };

        private static ColumnVariantDescriptor kernelInvertedDescriptor = new ColumnVariantDescriptor(
                new Guid("f8091021-3243-4546-5768-7989ab0c1d2e"),
                new ColumnVariantProperties
                {
                    Label = "Invert",
                    ColumnName = $"{kernelBaseProperties.ColumnName} (Inverted)",
                });

        public static bool IsDataAvailable(IDataExtensionRetrieval requiredData)
        {
            var data = requiredData.QueryOutput<List<CswitchIntervalEvent>>(
                new DataOutputPath(CswitchIntervalCooker.DataCookerPath, nameof(CswitchIntervalCooker.CswitchIntervalEvents)));
            return data != null && data.Count > 0;
        }

        public static void BuildTable(
            ITableBuilder tableBuilder,
            IDataExtensionRetrieval requiredData
        )
        {
            List<CswitchIntervalEvent> data =
                requiredData.QueryOutput<List<CswitchIntervalEvent>>(new DataOutputPath(CswitchIntervalCooker.DataCookerPath, nameof(CswitchIntervalCooker.CswitchIntervalEvents)));

            ITableBuilderWithRowCount tableBuilderWithRowCount = tableBuilder.SetRowCount(data.Count);
            StackAccessProvider stackAccessProvider = new StackAccessProvider();

            var baseProjection = Projection.Index(data);

            var startTimeProjection = baseProjection.Compose(Projector.StartTimeProjector);
            var stopTimeProjection = baseProjection.Compose(Projector.StopTimeProjector);
            var durationProjection = baseProjection.Compose(Projector.DurationProjector);
            var processProjection = baseProjection.Compose(Projector.ProcessProjector);
            var processIdProjection = processProjection.Compose(Projector.ProcessIdProjector);
            var processNameProjection = processProjection.Compose(Projector.ProcessNameProjector);
            var threadProjection = baseProjection.Compose(Projector.ThreadProjector);
            var threadIdProjection = threadProjection.Compose(Projector.ThreadIdProjector);
            var threadNameProjection = threadProjection.Compose(Projector.ThreadNameProjector);
            var cpuProjection = baseProjection.Compose(Projector.CpuProjector);
            var deltaInstructionsProjection = baseProjection.Compose(Projector.DeltaInstructionsProjector);
            var deltaCyclesProjection = baseProjection.Compose(Projector.DeltaCyclesProjector);
            var deltaTimeProjection = baseProjection.Compose(Projector.DeltaTimeProjector);
            var switchInStackProjection = baseProjection.Compose(Projector.SwitchInStackProjector);
            var switchInKernelStackProjection = baseProjection.Compose(Projector.SwitchInKernelStackProjector);

            var viewportClippedStartTimeProjection =
                Projection.ClipTimeToVisibleDomain.Create(startTimeProjection);
            var viewportClippedStopTimeProjection =
                Projection.ClipTimeToVisibleDomain.Create(stopTimeProjection);
            var cpuUsageProjection = Projection.Select(stopTimeProjection, startTimeProjection, new ReduceTimeSinceLastDiff());
            var cpuUsageInViewportProjection = Projection.Select(
                    viewportClippedStopTimeProjection,
                    viewportClippedStartTimeProjection,
                    new ReduceTimeSinceLastDiff());
            var percentCpuUsageProjection =
                Projection.VisibleDomainRelativePercent.Create(cpuUsageInViewportProjection);

            tableBuilderWithRowCount.AddColumn(startTimeColumn, startTimeProjection);
            tableBuilderWithRowCount.AddColumn(stopTimeColumn, stopTimeProjection);
            tableBuilderWithRowCount.AddColumn(durationColumn, durationProjection);
            tableBuilderWithRowCount.AddColumn(processNameColumn, processNameProjection);
            tableBuilderWithRowCount.AddColumn(processIdColumn, processIdProjection);
            tableBuilderWithRowCount.AddColumn(threadNameColumn, threadNameProjection);
            tableBuilderWithRowCount.AddColumn(threadIdColumn, threadIdProjection);
            tableBuilderWithRowCount.AddColumn(cpuColumn, cpuProjection);
            tableBuilderWithRowCount.AddColumn(deltaInstructionsColumn, deltaInstructionsProjection);
            tableBuilderWithRowCount.AddColumn(deltaCyclesColumn, deltaCyclesProjection);
            tableBuilderWithRowCount.AddColumn(deltaTimeColumn, deltaTimeProjection);
            tableBuilderWithRowCount.AddHierarchicalColumnWithVariants(switchInStackColumn,
                switchInStackProjection, stackAccessProvider, builder =>
                {
                    return builder
                        .WithModes(
                            baseProperties,
                            modeBuilder =>
                            {
                                return modeBuilder.WithHierarchicalToggle(
                                    invertedDescriptor,
                                    switchInStackProjection,
                                    new InvertedCollectionAccessProvider<StackAccessProvider, Backtrace, string>(stackAccessProvider));
                            });
                });
            tableBuilderWithRowCount.AddHierarchicalColumnWithVariants(switchInKernelStackColumn,
                switchInKernelStackProjection, stackAccessProvider, builder =>
                {
                    return builder
                        .WithModes(
                            kernelBaseProperties,
                            modeBuilder =>
                            {
                                return modeBuilder.WithHierarchicalToggle(
                                    kernelInvertedDescriptor,
                                    switchInKernelStackProjection,
                                    new InvertedCollectionAccessProvider<StackAccessProvider, Backtrace, string>(stackAccessProvider));
                            });
                });
            tableBuilderWithRowCount.AddColumn(cpuUsageInViewportColumn, cpuUsageInViewportProjection);
            tableBuilderWithRowCount.AddColumn(percentCpuUsageColumn, percentCpuUsageProjection);
            tableBuilderWithRowCount.AddColumn(countColumn, Projection.Constant(1));

            var tableConfig = new TableConfiguration("Utilization by Process, Thread")
            {
                Columns = new[]
                {
                    processNameColumn,
                    threadIdColumn,
                    TableConfiguration.PivotColumn,
                    countColumn,
                    cpuUsageInViewportColumn,
                    deltaInstructionsColumn,
                    deltaCyclesColumn,
                    startTimeColumn,
                    TableConfiguration.GraphColumn,
                    percentCpuUsageColumn
                },
            };

            tableConfig.AddColumnRole(ColumnRole.StartTime, startTimeColumn);
            tableConfig.AddColumnRole(ColumnRole.EndTime, stopTimeColumn);
            tableBuilder.AddTableConfiguration(tableConfig);

            var tableConfigTimeLineByCpu = new TableConfiguration("Timeline by CPU")
            {
                Columns = new[]
                {
                    cpuColumn,
                    processNameColumn,
                    TableConfiguration.PivotColumn,
                    countColumn,
                    cpuUsageInViewportColumn,
                    deltaInstructionsColumn,
                    deltaCyclesColumn,
                    startTimeColumn,
                    TableConfiguration.GraphColumn,
                    startTimeColumn,
                    stopTimeColumn
                },
            };

            tableConfigTimeLineByCpu.AddColumnRole(ColumnRole.StartTime, startTimeColumn);
            tableConfigTimeLineByCpu.AddColumnRole(ColumnRole.EndTime, stopTimeColumn);
            tableBuilder.AddTableConfiguration(tableConfigTimeLineByCpu);

            var tableConfigTimeLineByProcess = new TableConfiguration("Timeline by Process, Thread")
            {
                Columns = new[]
                {
                    processNameColumn,
                    threadIdColumn,
                    TableConfiguration.PivotColumn,
                    countColumn,
                    cpuUsageInViewportColumn,
                    deltaInstructionsColumn,
                    deltaCyclesColumn,
                    startTimeColumn,
                    TableConfiguration.GraphColumn,
                    startTimeColumn,
                    stopTimeColumn
                },
            };

            tableConfigTimeLineByProcess.AddColumnRole(ColumnRole.StartTime, startTimeColumn);
            tableConfigTimeLineByProcess.AddColumnRole(ColumnRole.EndTime, stopTimeColumn);
            tableBuilder.AddTableConfiguration(tableConfigTimeLineByProcess);

            tableBuilder.SetDefaultTableConfiguration(tableConfig);
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
