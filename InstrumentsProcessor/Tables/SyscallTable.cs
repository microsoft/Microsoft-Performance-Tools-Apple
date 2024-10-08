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
    public sealed class SyscallTable
    {
        public static TableDescriptor TableDescriptor =>
           new TableDescriptor(
              Guid.Parse("{a3618f19-dae7-4a0c-a1b2-f6da4d452962}"),
              "Syscall",
              "Events from Syscall",
              "Syscall",
              requiredDataCookers: new List<DataCookerPath>
              {
              SyscallCooker.DataCookerPath
              });

        private static readonly ColumnConfiguration startTimeColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("2280ddf6-9ee6-4418-99ea-cc1ba7d371ac"), "StartTime"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                CellFormat = TimestampFormatter.FormatMillisecondsGrouped,
            });

        private static readonly ColumnConfiguration stopTimeColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("68bdc4e1-ad63-4475-aa1d-06a717cf9424"), "StopTime"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                CellFormat = TimestampFormatter.FormatMillisecondsGrouped,
            });

        private static readonly ColumnConfiguration durationColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("183db6b5-8cfb-41d7-a45a-0f3b4013f49c"), "Duration"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                CellFormat = TimestampFormatter.FormatMillisecondsGrouped,
            });

        private static readonly ColumnConfiguration processIdColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("eda3d91a-d1cd-4e62-9a33-a698e40819e8"), "Process Id"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration threadIdColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("3dbbcba5-decb-47b3-9932-e994643c792f"), "Thread Id"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration processNameColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("75d628ab-3e47-4a96-baf2-1078946d4060"), "Process Name"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration callColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("0a585d8b-e36a-4ab9-8ce8-27eea0a2e915"), "Call"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration signatureColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("8bddc228-9a83-472e-ae5a-49f564384075"), "Signarure"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration noteColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("bb252028-bdaf-4cac-8134-fec46dc95340"), "Note"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration stackColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b1c62005-4979-4fd6-9a68-7d0ffc110555"), "Stack"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
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
            List<SyscallEvent> data =
                requiredData.QueryOutput<List<SyscallEvent>>(new DataOutputPath(SyscallCooker.DataCookerPath, nameof(SyscallCooker.SyscallEvents)));

            ITableBuilderWithRowCount tableBuilderWithRowCount = tableBuilder.SetRowCount(data.Count);

            var baseProjection = Projection.Index(data);
            var startTimeProjection = baseProjection.Compose(Projector.StartTimeProjector);
            var stopTimeProjection = baseProjection.Compose(Projector.StopTimeProjector);
            var durationProjection = baseProjection.Compose(Projector.DurationProjector);
            var processProjection = baseProjection.Compose(Projector.ProcessProjector);
            var processIdProjection = processProjection.Compose(Projector.ProcessIdProjector);
            var processNameProjection = processProjection.Compose(Projector.ProcessNameProjector);
            var threadProjection = baseProjection.Compose(Projector.ThreadProjector);
            var threadIdProjection = threadProjection.Compose(Projector.ThreadIdProjector);
            var callProjection = baseProjection.Compose(Projector.CallProjector);
            var signatureProjection = baseProjection.Compose(Projector.SignatureProjector);
            var stackProjection = baseProjection.Compose(Projector.StackProjector);
            var noteProjection = baseProjection.Compose(Projector.NoteProjector);

            tableBuilderWithRowCount.AddColumn(startTimeColumn, startTimeProjection);
            tableBuilderWithRowCount.AddColumn(stopTimeColumn, stopTimeProjection);
            tableBuilderWithRowCount.AddColumn(durationColumn, durationProjection);
            tableBuilderWithRowCount.AddColumn(processIdColumn, processIdProjection);
            tableBuilderWithRowCount.AddColumn(processNameColumn, processNameProjection);
            tableBuilderWithRowCount.AddColumn(threadIdColumn, threadIdProjection);
            tableBuilderWithRowCount.AddColumn(callColumn, callProjection);
            tableBuilderWithRowCount.AddColumn(signatureColumn, signatureProjection);
            tableBuilderWithRowCount.AddColumn(noteColumn, noteProjection);
            tableBuilderWithRowCount.AddHierarchicalColumn(stackColumn,
                                stackProjection, new StackAccessProvider());

            var tableConfig = new TableConfiguration("Syscall")
            {
                Columns = new[]
                {
                    processNameColumn,
                    threadIdColumn,
                    stackColumn,
                    TableConfiguration.PivotColumn,
                    callColumn,
                    signatureColumn,
                    TableConfiguration.GraphColumn,
                    startTimeColumn,
                    stopTimeColumn
                },
            };

            tableBuilder.AddTableConfiguration(tableConfig);
            tableBuilder.SetDefaultTableConfiguration(tableConfig);
        }
    }
}