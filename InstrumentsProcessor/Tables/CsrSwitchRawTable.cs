// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using InstrumentsProcessor.Cookers;
using InstrumentsProcessor.AccessProviders;
using InstrumentsProcessor.Parsing;
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
    public sealed class CsrSwitchRawTable
    {
        public static TableDescriptor TableDescriptor =>
           new TableDescriptor(
              Guid.Parse("{8f2a4b6c-7d9e-4f1a-3b5c-0d2e4f6a8b9c}"),
              "CSR Context Switch (Raw)",
              "Raw csr-switch-on and csr-switch-off point events",
              "Context Switch",
              requiredDataCookers: new List<DataCookerPath>
              {
                  CsrSwitchCooker.DataCookerPath
              });

        private static readonly ColumnConfiguration timeColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5e"), "Time"),
            new UIHints
            {
                IsVisible = true,
                Width = 120,
                CellFormat = TimestampFormatter.FormatMicrosecondsGrouped,
                AggregationMode = AggregationMode.Min
            });

        private static readonly ColumnConfiguration eventTypeColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6f"), "Event Type"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration processNameColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("c3d4e5f6-a7b8-4c9d-0e1f-2a3b4c5d6e70"), "Process"),
            new UIHints
            {
                IsVisible = true,
                Width = 150,
            });

        private static readonly ColumnConfiguration processIdColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d4e5f6a7-b8c9-4d0e-1f2a-3b4c5d6e7f81"), "Process ID"),
            new UIHints
            {
                IsVisible = true,
                Width = 80,
            });

        private static readonly ColumnConfiguration threadNameColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("e5f6a7b8-c9d0-4e1f-2a3b-4c5d6e7f8092"), "Thread Name"),
            new UIHints
            {
                IsVisible = true,
                Width = 150,
            });

        private static readonly ColumnConfiguration threadIdColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("f6a7b8c9-d0e1-4f2a-3b4c-5d6e7f809103"), "Thread ID"),
            new UIHints
            {
                IsVisible = true,
                Width = 80,
            });

        private static readonly ColumnConfiguration cpuColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("a7b8c9d0-e1f2-4a3b-4c5d-6e7f80910214"), "CPU"),
            new UIHints
            {
                IsVisible = true,
                Width = 60,
            });

        private static readonly ColumnConfiguration instructionsColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b8c9d0e1-f2a3-4b4c-5d6e-7f8091021325"), "Instructions (Fixed)"),
            new UIHints
            {
                IsVisible = true,
                Width = 140,
                AggregationMode = AggregationMode.Max
            });

        private static readonly ColumnConfiguration cyclesColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("c9d0e1f2-a3b4-4c5d-6e7f-809102132436"), "Cycles (Fixed)"),
            new UIHints
            {
                IsVisible = true,
                Width = 140,
                AggregationMode = AggregationMode.Max
            });

        private static readonly ColumnConfiguration countColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d0e1f2a3-b4c5-4d6e-7f80-910213243547"), "Count"),
            new UIHints
            {
                IsVisible = true,
                Width = 60,
                AggregationMode = AggregationMode.Sum
            });

        private static readonly ColumnConfiguration stackColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("ace8af48-b68b-4fbd-aade-8c9982ea4c01"), "Stack")
            { ShortDescription = "User callstack recorded for this exact switch-on or switch-off event." },
            new UIHints { IsVisible = true, Width = 300 });

        private static readonly ColumnConfiguration kernelStackColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("8a1fbd9c-9214-4d88-af14-455ad57d45b7"), "Kernel Stack"),
            new UIHints { IsVisible = true, Width = 300 });

        public static bool IsDataAvailable(IDataExtensionRetrieval requiredData)
        {
            var data = requiredData.QueryOutput<List<CsrSwitchEvent>>(
                new DataOutputPath(CsrSwitchCooker.DataCookerPath, nameof(CsrSwitchCooker.CsrSwitchEvents)));
            return data != null && data.Count > 0;
        }

        public static void BuildTable(
            ITableBuilder tableBuilder,
            IDataExtensionRetrieval requiredData)
        {
            List<CsrSwitchEvent> data =
                requiredData.QueryOutput<List<CsrSwitchEvent>>(
                    new DataOutputPath(CsrSwitchCooker.DataCookerPath, nameof(CsrSwitchCooker.CsrSwitchEvents)));

            ITableBuilderWithRowCount tableBuilderWithRowCount = tableBuilder.SetRowCount(data.Count);

            var baseProjection = Projection.Index(data);

            var timeProjection = baseProjection.Compose(Projector.TimeProjector);
            var eventTypeProjection = baseProjection.Compose(Projector.EventTypeProjector);
            var processProjection = baseProjection.Compose(Projector.ProcessProjector);
            var processIdProjection = processProjection.Compose(Projector.ProcessIdProjector);
            var processNameProjection = processProjection.Compose(Projector.ProcessNameProjector);
            var threadProjection = baseProjection.Compose(Projector.ThreadProjector);
            var threadIdProjection = threadProjection.Compose(Projector.ThreadIdProjector);
            var threadNameProjection = threadProjection.Compose(Projector.ThreadNameProjector);
            var cpuProjection = baseProjection.Compose(Projector.CpuProjector);
            var instructionsProjection = baseProjection.Compose(Projector.InstructionsProjector);
            var cyclesProjection = baseProjection.Compose(Projector.CyclesProjector);
            var stackProjection = baseProjection.Compose((CsrSwitchEvent evt) => evt.Stack);
            var kernelStackProjection = baseProjection.Compose((CsrSwitchEvent evt) => evt.KernelStack);
            var stackAccess = new StackAccessProvider();

            tableBuilderWithRowCount.AddColumn(timeColumn, timeProjection);
            tableBuilderWithRowCount.AddColumn(eventTypeColumn, eventTypeProjection);
            tableBuilderWithRowCount.AddColumn(processNameColumn, processNameProjection);
            tableBuilderWithRowCount.AddColumn(processIdColumn, processIdProjection);
            tableBuilderWithRowCount.AddColumn(threadNameColumn, threadNameProjection);
            tableBuilderWithRowCount.AddColumn(threadIdColumn, threadIdProjection);
            tableBuilderWithRowCount.AddColumn(cpuColumn, cpuProjection);
            tableBuilderWithRowCount.AddColumn(instructionsColumn, instructionsProjection);
            tableBuilderWithRowCount.AddColumn(cyclesColumn, cyclesProjection);
            tableBuilderWithRowCount.AddColumn(countColumn, Projection.Constant(1));
            tableBuilderWithRowCount.AddHierarchicalColumnWithVariants(stackColumn, stackProjection, stackAccess,
                builder => builder.WithModes(new ColumnVariantProperties { Label = "Stack frames", ColumnName = "Stack" },
                    modes => modes.WithHierarchicalToggle(new ColumnVariantDescriptor(
                        new Guid("3dcd03fb-9817-4ca3-a847-2739f6aa70fa"),
                        new ColumnVariantProperties { Label = "Invert", ColumnName = "Stack (Inverted)" }),
                        stackProjection, new InvertedCollectionAccessProvider<StackAccessProvider, Backtrace, string>(stackAccess))));
            tableBuilderWithRowCount.AddHierarchicalColumnWithVariants(kernelStackColumn, kernelStackProjection, stackAccess,
                builder => builder.WithModes(new ColumnVariantProperties { Label = "Stack frames", ColumnName = "Kernel Stack" },
                    modes => modes.WithHierarchicalToggle(new ColumnVariantDescriptor(
                        new Guid("fd3c278d-48d4-4615-baa1-e2b04c714a68"),
                        new ColumnVariantProperties { Label = "Invert", ColumnName = "Kernel Stack (Inverted)" }),
                        kernelStackProjection, new InvertedCollectionAccessProvider<StackAccessProvider, Backtrace, string>(stackAccess))));

            // By CPU
            var tableConfig = new TableConfiguration("By CPU")
            {
                Columns = new[]
                {
                    cpuColumn,
                    eventTypeColumn,
                    processNameColumn,
                    threadIdColumn,
                    stackColumn,
                    TableConfiguration.PivotColumn,
                    countColumn,
                    instructionsColumn,
                    cyclesColumn,
                    TableConfiguration.GraphColumn,
                    timeColumn,
                },
            };

            tableConfig.AddColumnRole(ColumnRole.StartTime, timeColumn);
            tableConfig.AddColumnRole(ColumnRole.EndTime, timeColumn);
            tableBuilder.AddTableConfiguration(tableConfig);

            // By Process, Thread
            var tableConfigByProcess = new TableConfiguration("By Process, Thread")
            {
                Columns = new[]
                {
                    processNameColumn,
                    threadIdColumn,
                    eventTypeColumn,
                    stackColumn,
                    TableConfiguration.PivotColumn,
                    countColumn,
                    instructionsColumn,
                    cyclesColumn,
                    TableConfiguration.GraphColumn,
                    timeColumn,
                },
            };

            tableConfigByProcess.AddColumnRole(ColumnRole.StartTime, timeColumn);
            tableConfigByProcess.AddColumnRole(ColumnRole.EndTime, timeColumn);
            tableBuilder.AddTableConfiguration(tableConfigByProcess);

            tableBuilder.SetDefaultTableConfiguration(tableConfig);
        }
    }
}
