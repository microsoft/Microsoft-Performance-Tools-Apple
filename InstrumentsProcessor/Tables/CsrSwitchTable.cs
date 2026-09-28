// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using InstrumentsProcessor.Cookers;
using InstrumentsProcessor.Parsing.DataModels;
using InstrumentsProcessor.Parsing.Events;
using Microsoft.Performance.SDK;
using Microsoft.Performance.SDK.Extensibility;
using Microsoft.Performance.SDK.Processing;
using System;
using System.Collections.Generic;
using System.Linq;
using Timestamp = Microsoft.Performance.SDK.Timestamp;
using TimestampDelta = Microsoft.Performance.SDK.TimestampDelta;

namespace InstrumentsProcessor.Tables
{
    [Table]
    public sealed class CsrSwitchTable
    {
        public static TableDescriptor TableDescriptor =>
           new TableDescriptor(
              Guid.Parse("{3e5a1c7b-9d2f-4e8a-b6c4-1f0d3a5b7c9e}"),
              "CSR Context Switch",
              "Correlated csr-switch-on/off intervals with Δ Instructions and Δ Cycles",
              "Context Switch",
              requiredDataCookers: new List<DataCookerPath>
              {
                  CsrSwitchCooker.DataCookerPath
              });

        private static readonly ColumnConfiguration switchOnTimeColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("4a6b8c0d-2e3f-4a5b-9c7d-1e0f3a5b7c9d"), "Switch-On Time"),
            new UIHints
            {
                IsVisible = true,
                Width = 120,
                CellFormat = TimestampFormatter.FormatMicrosecondsGrouped,
                AggregationMode = AggregationMode.Min
            });

        private static readonly ColumnConfiguration switchOffTimeColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("5b7c9d1e-3f4a-4b6c-0d2e-3f5a7b9c1d0e"), "Switch-Off Time"),
            new UIHints
            {
                IsVisible = true,
                Width = 120,
                CellFormat = TimestampFormatter.FormatMicrosecondsGrouped,
                AggregationMode = AggregationMode.Max
            });

        private static readonly ColumnConfiguration durationColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("ab1c2d3e-4f5a-4b6c-7d8e-9f0a1b2c3d4e"), "Duration"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                AggregationMode = AggregationMode.Sum,
                CellFormat = TimestampFormatter.FormatMillisecondsGrouped
            });

        private static readonly ColumnConfiguration processNameColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("6c8d0e2f-4a5b-4c7d-1e3f-4a6b8c0d2e3f"), "Process"),
            new UIHints
            {
                IsVisible = true,
                Width = 150,
            });

        private static readonly ColumnConfiguration processIdColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("7d9e1f3a-5b6c-4d8e-2f4a-5b7c9d1e3f4a"), "Process ID"),
            new UIHints
            {
                IsVisible = true,
                Width = 80,
            });

        private static readonly ColumnConfiguration threadNameColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("8e0f2a4b-6c7d-4e9f-3a5b-6c8d0e2f4a5b"), "Thread Name"),
            new UIHints
            {
                IsVisible = true,
                Width = 150,
            });

        private static readonly ColumnConfiguration threadIdColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("9f1a3b5c-7d8e-4f0a-4b6c-7d9e1f3a5b6c"), "Thread ID"),
            new UIHints
            {
                IsVisible = true,
                Width = 80,
            });

        private static readonly ColumnConfiguration cpuColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("0a2b4c6d-8e9f-4a1b-5c7d-8e0f2a4b6c7d"), "CPU"),
            new UIHints
            {
                IsVisible = true,
                Width = 60,
            });

        private static readonly ColumnConfiguration deltaInstructionsColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("1b3c5d7e-9f0a-4b2c-6d8e-9f1a3b5c7d8e"), "Δ Instructions"),
            new UIHints
            {
                IsVisible = true,
                Width = 140,
                AggregationMode = AggregationMode.Sum
            });

        private static readonly ColumnConfiguration deltaCyclesColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("2c4d6e8f-0a1b-4c3d-7e9f-0a2b4c6d8e9f"), "Δ Cycles"),
            new UIHints
            {
                IsVisible = true,
                Width = 140,
                AggregationMode = AggregationMode.Sum
            });

        private static readonly ColumnConfiguration countColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("3d5e7f9a-1b2c-4d4e-8f0a-1b3c5d7e9f0a"), "Count"),
            new UIHints
            {
                IsVisible = true,
                Width = 60,
                AggregationMode = AggregationMode.Sum
            });

        private static readonly ColumnConfiguration cpuUsageInViewportColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("4e6f8a9b-2c3d-4e5f-9a0b-2c4d6e8f0a1b"), "CPU Usage (in view)") { ShortDescription = "The time the thread spends running on the CPU" },
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                CellFormat = TimestampFormatter.FormatMillisecondsGrouped,
                AggregationMode = AggregationMode.Sum
            });

        private static readonly ColumnConfiguration percentCpuUsageColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("5f7a9b0c-3d4e-4f6a-0b1c-3d5e7f9a1b2c"), "% CPU Usage") { IsPercent = true, ShortDescription = "Percentage of the total visible processor time" },
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                SortOrder = SortOrder.Descending,
                CellFormat = ColumnFormats.PercentFormat,
                AggregationMode = AggregationMode.Sum
            });

        public static bool IsDataAvailable(IDataExtensionRetrieval requiredData)
        {
            var data = requiredData.QueryOutput<List<CsrSwitchEvent>>(
                new DataOutputPath(CsrSwitchCooker.DataCookerPath, nameof(CsrSwitchCooker.CsrSwitchEvents)));
            return data != null && data.Count > 0;
        }

        /// <summary>
        /// Correlates csr-switch-on/off events per CPU to produce intervals.
        /// Per CPU, each switch-on is paired with the next switch-off to compute
        /// Δ Instructions and Δ Cycles for the time the thread was running.
        /// </summary>
        private static List<CsrSwitchInterval> BuildIntervals(List<CsrSwitchEvent> events)
        {
            var intervals = new List<CsrSwitchInterval>();

            // Sort all events by timestamp so on/off events interleave correctly
            var sorted = events.OrderBy(e => e.Timestamp).ToList();

            // Per CPU, track the pending switch-on and the latest switch-off.
            // Consecutive switch-off events without an intervening switch-on mean
            // the thread is still on the CPU — keep overwriting until a switch-on
            // arrives, then emit the interval using the last switch-off.
            var pendingSwitchOn = new Dictionary<long, CsrSwitchEvent>();
            var pendingSwitchOff = new Dictionary<long, CsrSwitchEvent>();

            foreach (var evt in sorted)
            {
                long cpu = (long)(evt.Cpu?.Value ?? 0);

                if (evt.IsSwitchOn)
                {
                    // A new switch-on arrived. If there's a pending on+off pair,
                    // finalize the interval using the last switch-off we saw.
                    if (pendingSwitchOn.TryGetValue(cpu, out var onEvt) &&
                        pendingSwitchOff.TryGetValue(cpu, out var offEvt))
                    {
                        ulong onInstr = onEvt.Instructions?.Value ?? 0;
                        ulong offInstr = offEvt.Instructions?.Value ?? 0;
                        ulong onCycles = onEvt.Cycles?.Value ?? 0;
                        ulong offCycles = offEvt.Cycles?.Value ?? 0;

                        intervals.Add(new CsrSwitchInterval
                        {
                            SwitchOnTime = onEvt.Time?.Value ?? default,
                            SwitchOffTime = offEvt.Time?.Value ?? default,
                            Duration = (offEvt.Time?.Value ?? default) - (onEvt.Time?.Value ?? default),
                            Process = onEvt.Process,
                            Thread = onEvt.Thread,
                            Cpu = cpu,
                            DeltaInstructions = offInstr - onInstr,
                            DeltaCycles = offCycles - onCycles,
                        });
                    }

                    // Start tracking the new switch-on, clear any stale switch-off
                    pendingSwitchOn[cpu] = evt;
                    pendingSwitchOff.Remove(cpu);
                }
                else if (evt.IsSwitchOff)
                {
                    // Always overwrite — keep only the latest switch-off per CPU
                    pendingSwitchOff[cpu] = evt;
                }
            }

            // Flush any remaining on+off pairs at end of trace
            foreach (var cpu in pendingSwitchOn.Keys)
            {
                if (pendingSwitchOn.TryGetValue(cpu, out var onEvt) &&
                    pendingSwitchOff.TryGetValue(cpu, out var offEvt))
                {
                    ulong onInstr = onEvt.Instructions?.Value ?? 0;
                    ulong offInstr = offEvt.Instructions?.Value ?? 0;
                    ulong onCycles = onEvt.Cycles?.Value ?? 0;
                    ulong offCycles = offEvt.Cycles?.Value ?? 0;

                    intervals.Add(new CsrSwitchInterval
                    {
                        SwitchOnTime = onEvt.Time?.Value ?? default,
                        SwitchOffTime = offEvt.Time?.Value ?? default,
                        Duration = (offEvt.Time?.Value ?? default) - (onEvt.Time?.Value ?? default),
                        Process = onEvt.Process,
                        Thread = onEvt.Thread,
                        Cpu = cpu,
                        DeltaInstructions = offInstr - onInstr,
                        DeltaCycles = offCycles - onCycles,
                    });
                }
            }

            return intervals;
        }

        public static void BuildTable(
            ITableBuilder tableBuilder,
            IDataExtensionRetrieval requiredData)
        {
            List<CsrSwitchEvent> rawData =
                requiredData.QueryOutput<List<CsrSwitchEvent>>(
                    new DataOutputPath(CsrSwitchCooker.DataCookerPath, nameof(CsrSwitchCooker.CsrSwitchEvents)));

            List<CsrSwitchInterval> data = BuildIntervals(rawData);

            ITableBuilderWithRowCount tableBuilderWithRowCount = tableBuilder.SetRowCount(data.Count);

            var baseProjection = Projection.Index(data);

            var switchOnTimeProjection = baseProjection.Compose(Projector.SwitchOnTimeProjector);
            var switchOffTimeProjection = baseProjection.Compose(Projector.SwitchOffTimeProjector);
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

            var viewportClippedSwitchOnTimeProjection =
                Projection.ClipTimeToVisibleDomain.Create(switchOnTimeProjection);
            var viewportClippedSwitchOffTimeProjection =
                Projection.ClipTimeToVisibleDomain.Create(switchOffTimeProjection);
            var cpuUsageInViewportProjection = Projection.Select(
                    viewportClippedSwitchOffTimeProjection,
                    viewportClippedSwitchOnTimeProjection,
                    new ReduceTimeSinceLastDiff());
            var percentCpuUsageProjection =
                Projection.VisibleDomainRelativePercent.Create(cpuUsageInViewportProjection);

            tableBuilderWithRowCount.AddColumn(switchOnTimeColumn, switchOnTimeProjection);
            tableBuilderWithRowCount.AddColumn(switchOffTimeColumn, switchOffTimeProjection);
            tableBuilderWithRowCount.AddColumn(durationColumn, durationProjection);
            tableBuilderWithRowCount.AddColumn(processNameColumn, processNameProjection);
            tableBuilderWithRowCount.AddColumn(processIdColumn, processIdProjection);
            tableBuilderWithRowCount.AddColumn(threadNameColumn, threadNameProjection);
            tableBuilderWithRowCount.AddColumn(threadIdColumn, threadIdProjection);
            tableBuilderWithRowCount.AddColumn(cpuColumn, cpuProjection);
            tableBuilderWithRowCount.AddColumn(deltaInstructionsColumn, deltaInstructionsProjection);
            tableBuilderWithRowCount.AddColumn(deltaCyclesColumn, deltaCyclesProjection);
            tableBuilderWithRowCount.AddColumn(cpuUsageInViewportColumn, cpuUsageInViewportProjection);
            tableBuilderWithRowCount.AddColumn(percentCpuUsageColumn, percentCpuUsageProjection);
            tableBuilderWithRowCount.AddColumn(countColumn, Projection.Constant(1));

            // Utilization by Process, Thread
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
                    switchOnTimeColumn,
                    TableConfiguration.GraphColumn,
                    percentCpuUsageColumn,
                },
            };

            tableConfig.AddColumnRole(ColumnRole.StartTime, switchOnTimeColumn);
            tableConfig.AddColumnRole(ColumnRole.EndTime, switchOffTimeColumn);
            tableBuilder.AddTableConfiguration(tableConfig);

            // Timeline by CPU
            var tableConfigByCpu = new TableConfiguration("Timeline by CPU")
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
                    switchOnTimeColumn,
                    TableConfiguration.GraphColumn,
                    switchOnTimeColumn,
                    switchOffTimeColumn,
                },
            };

            tableConfigByCpu.AddColumnRole(ColumnRole.StartTime, switchOnTimeColumn);
            tableConfigByCpu.AddColumnRole(ColumnRole.EndTime, switchOffTimeColumn);
            tableBuilder.AddTableConfiguration(tableConfigByCpu);

            // Timeline by Process, Thread
            var tableConfigByProcess = new TableConfiguration("Timeline by Process, Thread")
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
                    switchOnTimeColumn,
                    TableConfiguration.GraphColumn,
                    switchOnTimeColumn,
                    switchOffTimeColumn,
                },
            };

            tableConfigByProcess.AddColumnRole(ColumnRole.StartTime, switchOnTimeColumn);
            tableConfigByProcess.AddColumnRole(ColumnRole.EndTime, switchOffTimeColumn);
            tableBuilder.AddTableConfiguration(tableConfigByProcess);

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
