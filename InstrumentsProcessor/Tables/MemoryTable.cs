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

namespace InstrumentsProcessor.Tables
{
    [Table]
    public sealed class MemoryTable
    {
        public static TableDescriptor TableDescriptor =>
           new TableDescriptor(
              Guid.Parse("{a283dbe2-e77f-49fb-8887-b526d620f8fd}"),
              "Memory",
              "Events from Virtual Memory",
              "Virtual Memory Events",
              requiredDataCookers: new List<DataCookerPath>
              {
              VirtualMemoryCooker.DataCookerPath
              });

        private static readonly ColumnConfiguration startTimeColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("8ab9550d-65b5-4843-a933-116c2dae55db"), "StartTime"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                CellFormat = TimestampFormatter.FormatMicrosecondsGrouped,
            });

        private static readonly ColumnConfiguration durationColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("c64de568-5c64-4776-a941-19e25b487997"), "Duration"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                AggregationMode = AggregationMode.Sum,
                CellFormat = TimestampFormatter.FormatMillisecondsGrouped
            });

        private static readonly ColumnConfiguration threadIdColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("85dd0f13-140b-4bd6-9554-1a8a2c1f0cd2"), "Thread ID"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration operationColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("aff675b5-5095-414a-b4ec-5a2c01e37a21"), "Operation"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration processIdColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("22c257be-ada1-43d8-b6a6-5e14e596a437"), "Process ID"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration processNameColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("5eaf56f1-7b0e-4ca0-8cc2-c5ea0d7c7509"), "Process"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration deviceSessionColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("eac104ba-b495-4351-9ba2-0061da430735"), "Device Session"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration cpuTimeColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("bd4250fa-e9e1-4546-8418-15afb878e95f"), "CPU Time"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                CellFormat = TimestampFormatter.FormatMillisecondsGrouped,
                AggregationMode = AggregationMode.Max
            });

        private static readonly ColumnConfiguration waitTimeColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("48f46270-3a9a-48cb-8080-4251bc0edbea"), "Wait Time"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                CellFormat = TimestampFormatter.FormatMillisecondsGrouped,
                AggregationMode = AggregationMode.Max
            });

        private static readonly ColumnConfiguration addressColumn = new ColumnConfiguration(
          new ColumnMetadata(new Guid("22761b34-e893-46b6-ad0a-6b733daacbe5"), "Address"),
          new UIHints
          {
              IsVisible = true,
              Width = 100,
          });

        private static readonly ColumnConfiguration sizeColumn = new ColumnConfiguration(
          new ColumnMetadata(new Guid("52878551-a6f7-4ef9-b03b-12c9341fce46"), "Size") { ShortDescription = "Size of the operation in bytes." },
          new UIHints
          {
              IsVisible = true,
              Width = 100,
              AggregationMode = AggregationMode.Sum,
              SortOrder = SortOrder.Descending
          });

        private static readonly ColumnConfiguration stackColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("1707206d-15e7-4c91-9d8d-8157f7cbb68b"), "Stack"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration countPreset = new ColumnConfiguration(
            new ColumnMetadata(new Guid("1e430d6f-7c88-4e59-bd2a-1c51230a46d9"), "Count"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                AggregationMode = AggregationMode.Sum
            });

        private static readonly ColumnVariantProperties baseProperties = new ColumnVariantProperties()
        {
            Label = "Stack frames",
            ColumnName = "Stack",
        };

        private static ColumnVariantDescriptor invertedDescriptor = new ColumnVariantDescriptor(
                new Guid("13a5ed0c-1591-4210-8fc2-147e1aba2d82"),
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
            List<VirtualMemoryEvent> data =
                requiredData.QueryOutput<List<VirtualMemoryEvent>>(new DataOutputPath(VirtualMemoryCooker.DataCookerPath, nameof(VirtualMemoryCooker.VirtualMemoryEvents)));

            ITableBuilderWithRowCount tableBuilderWithRowCount = tableBuilder.SetRowCount(data.Count);
            StackAccessProvider stackAccessProvider = new StackAccessProvider();

            var baseProjection = Projection.Index(data);

            var startTimeProjection = baseProjection.Compose(Projector.StartTimeProjector);
            var durationProjection = baseProjection.Compose(Projector.DurationProjector);
            var threadProjection = baseProjection.Compose(Projector.ThreadProjector);
            var threadIdProjection = threadProjection.Compose(Projector.ThreadIdProjector);
            var operationProjection = baseProjection.Compose(Projector.OperationProjector);
            var processProjection = baseProjection.Compose(Projector.ProcessProjector);
            var processIdProjection = processProjection.Compose(Projector.ProcessIdProjector);
            var processNameProjection = processProjection.Compose(Projector.ProcessNameProjector);
            var deviceSessionProjection = processProjection.Compose(Projector.DeviceSessionProjector);
            var cpuTimeProjection = baseProjection.Compose(Projector.CpuTimeProjector);
            var waitTimeProjection = baseProjection.Compose(Projector.WaitTimeProjector);
            var addressProjection = baseProjection.Compose(Projector.AddressProjector);
            var sizeProjection = baseProjection.Compose(Projector.SizeProjector);
            var stackProjection = baseProjection.Compose(Projector.StackProjector);

            tableBuilderWithRowCount.AddColumn(startTimeColumn, startTimeProjection);
            tableBuilderWithRowCount.AddColumn(durationColumn, durationProjection);
            tableBuilderWithRowCount.AddColumn(threadIdColumn, threadIdProjection);
            tableBuilderWithRowCount.AddColumn(operationColumn, operationProjection);
            tableBuilderWithRowCount.AddColumn(processIdColumn, processIdProjection);
            tableBuilderWithRowCount.AddColumn(processNameColumn, processNameProjection);
            tableBuilderWithRowCount.AddColumn(deviceSessionColumn, deviceSessionProjection);
            tableBuilderWithRowCount.AddColumn(cpuTimeColumn, cpuTimeProjection);
            tableBuilderWithRowCount.AddColumn(waitTimeColumn, waitTimeProjection);
            tableBuilderWithRowCount.AddColumn(addressColumn, addressProjection);
            tableBuilderWithRowCount.AddColumn(sizeColumn, sizeProjection);
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
            tableBuilderWithRowCount.AddColumn(countPreset, Projection.Constant(1));

            var tableConfig = new TableConfiguration("Memory by Process, Stack")
            {
                Columns = new[]
                {
                    processNameColumn,
                    stackColumn,
                    TableConfiguration.PivotColumn,
                    countPreset,
                    operationColumn,
                    addressColumn,
                    sizeColumn,
                    waitTimeColumn,
                    cpuTimeColumn,
                    TableConfiguration.GraphColumn,
                    startTimeColumn
                },
            };

            tableConfig.AddColumnRole(ColumnRole.StartTime, startTimeColumn);
            tableConfig.AddColumnRole(ColumnRole.Duration, durationColumn);

            tableBuilder.AddTableConfiguration(tableConfig);
            tableBuilder.SetDefaultTableConfiguration(tableConfig);
        }
    }
}