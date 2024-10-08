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
    public sealed class MetalGpuIntervalTable
    {
        public static TableDescriptor TableDescriptor =>
            new TableDescriptor(
                Guid.Parse("e6a9cbff-3e33-4ac0-b908-6f55d9e3c42d"),
                "Metal GPU Interval",
                "Events from Metal GPU Interval",
                "Metal GPU Interval Events",
                requiredDataCookers: new List<DataCookerPath>
                {
                    MetalGpuIntervalCooker.DataCookerPath
                });

        private static readonly ColumnConfiguration startTimeColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("44e2a3c8-440e-4193-9f10-4f62cb9063d1"), "Start Time"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                AggregationMode = AggregationMode.Min,
                CellFormat = TimestampFormatter.FormatMicrosecondsGrouped,
            });

        private static readonly ColumnConfiguration stopTimeColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("c7ae41ea-0593-4d79-8a5f-ae49f865316d"), "Stop Time"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                AggregationMode = AggregationMode.Min,
                CellFormat = TimestampFormatter.FormatMicrosecondsGrouped,
            });

        private static readonly ColumnConfiguration durationColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("4e615583-ebc5-4dfe-a49a-c849d1eec9fd"), "Duration"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                AggregationMode = AggregationMode.Sum,
                CellFormat = TimestampFormatter.FormatMillisecondsGrouped
            });

        private static readonly ColumnConfiguration channelNameColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("61790774-b444-4cd6-9205-4e5e2a813aeb"), "Channel Name"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration frameColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("3b052bbb-981a-4cad-b025-9d48a440997e"), "Frame"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration cpuToGpuLatencyColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("2de08b6a-ffd6-4aab-95d2-eea012731735"), "CPU to GPU Latency"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                CellFormat = TimestampFormatter.FormatMillisecondsGrouped,
                AggregationMode = AggregationMode.Max
            });

        private static readonly ColumnConfiguration depthColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("4775d80e-8b38-4e08-a748-69745a1135f5"), "Depth"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration labelColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("62441339-6c85-4d0f-86ee-d2cbbfcc5a31"), "Label"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration stateColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b29f6722-1039-4b30-af46-239826c2f37a"), "State"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration connectionUUIDColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("7cb0ea5d-b268-428d-bcee-a22e4b876151"), "Connection UUID"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration colorColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("da870f76-3795-4868-9c61-59d8d02dd907"), "Color"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration processNameColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("af999949-a807-440d-8b6a-37f05234bed5"), "Process"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration processIdColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("db67bfda-8784-499b-aa3b-dbf5df0780d5"), "Process ID"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration deviceSessionColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("238cb6f5-787a-4878-9c92-c973e0763a73"), "Device Session"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration metalDeviceColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b78ebdb5-a8ce-4d9f-a823-7d55f271b940"), "Metal Device"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration channelSubtitleColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("5b4b1561-9082-4df2-a336-b3b7a379454e"), "Channel Subtitle"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration ioSurfaceAccessesColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("0e53e500-2851-46f4-a1f7-10c2f77759e3"), "IOSurface Accesses"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration bytesColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("34bc65de-7a4c-4276-a8c4-a1765cbcad36"), "Bytes"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                AggregationMode = AggregationMode.Sum
            });

        private static readonly ColumnConfiguration commandBufferIdColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("f1eef3a0-6e0f-49f5-a1a6-094841e2117e"), "Command Buffer Id"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration encoderIDColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("74bf1da7-5e79-4ac6-af1c-5cd158654b4a"), "Encoder Id"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration gpuSubmissionIdColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("87aa13d8-ef0c-438b-b6f8-3fc6b3d8d92d"), "GPU Submission Id"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration countColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("123a01d5-a8fd-4cc7-93d0-b7b44c8e644c"), "Count"),
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
            List<MetalGpuIntervalEvent> data =
                requiredData.QueryOutput<List<MetalGpuIntervalEvent>>(new DataOutputPath(MetalGpuIntervalCooker.DataCookerPath, nameof(MetalGpuIntervalCooker.MetalGpuIntervalEvents)));
            ITableBuilderWithRowCount tableBuilderWithRowCount = tableBuilder.SetRowCount(data.Count);

            var baseProjection = Projection.Index(data);

            var startTimeProjection = baseProjection.Compose(Projector.StartTimeProjector);
            var stopTimeProjection = baseProjection.Compose(Projector.StopTimeProjector);
            var durationProjection = baseProjection.Compose(Projector.DurationProjector);
            var channelNameProjection = baseProjection.Compose(Projector.ChannelNameProjector);
            var frameProjection = baseProjection.Compose(Projector.FrameProjector);
            var cpuToGpuLatencyProjection = baseProjection.Compose(Projector.CpuToGpuLatencyProjector);
            var depthProjection = baseProjection.Compose(Projector.DepthProjector);
            var labelProjection = baseProjection.Compose(Projector.LabelProjector);
            var stateProjection = baseProjection.Compose(Projector.StateProjector);
            var connectionUUIDProjection = baseProjection.Compose(Projector.ConnectionUUIDProjector);
            var colorProjection = baseProjection.Compose(Projector.ColorProjector);
            var processProjection = baseProjection.Compose(Projector.ProcessProjector); 
            var processIdProjection = processProjection.Compose(Projector.ProcessIdProjector);
            var processNameProjection = processProjection.Compose(Projector.ProcessNameProjector);
            var deviceSessionProjection = processProjection.Compose(Projector.DeviceSessionProjector);
            var metalDeviceProjection = baseProjection.Compose(Projector.MetalDeviceProjector);
            var channelSubtitleProjection = baseProjection.Compose(Projector.ChannelSubtitleProjector);
            var ioSurfaceAccessesProjection = baseProjection.Compose(Projector.IOSurfaceAccessesProjector);
            var bytesProjection = baseProjection.Compose(Projector.BytesProjector);
            var commandBufferIdProjection = baseProjection.Compose(Projector.CommandBufferIdProjector);
            var encoderIDProjection = baseProjection.Compose(Projector.EncoderIDProjector);
            var gpuSubmissionIdProjection = baseProjection.Compose(Projector.GpuSubmissionIdProjector);

            tableBuilderWithRowCount.AddColumn(startTimeColumn, startTimeProjection);
            tableBuilderWithRowCount.AddColumn(stopTimeColumn, stopTimeProjection);
            tableBuilderWithRowCount.AddColumn(durationColumn, durationProjection);
            tableBuilderWithRowCount.AddColumn(channelNameColumn, channelNameProjection);
            tableBuilderWithRowCount.AddColumn(frameColumn, frameProjection);
            tableBuilderWithRowCount.AddColumn(cpuToGpuLatencyColumn, cpuToGpuLatencyProjection);
            tableBuilderWithRowCount.AddColumn(depthColumn, depthProjection);
            tableBuilderWithRowCount.AddColumn(labelColumn, labelProjection);
            tableBuilderWithRowCount.AddColumn(stateColumn, stateProjection);
            tableBuilderWithRowCount.AddColumn(connectionUUIDColumn, connectionUUIDProjection);
            tableBuilderWithRowCount.AddColumn(colorColumn, colorProjection);
            tableBuilderWithRowCount.AddColumn(processIdColumn, processIdProjection);
            tableBuilderWithRowCount.AddColumn(processNameColumn, processNameProjection);
            tableBuilderWithRowCount.AddColumn(deviceSessionColumn, deviceSessionProjection);
            tableBuilderWithRowCount.AddColumn(metalDeviceColumn, metalDeviceProjection);
            tableBuilderWithRowCount.AddColumn(channelSubtitleColumn, channelSubtitleProjection);
            tableBuilderWithRowCount.AddColumn(ioSurfaceAccessesColumn, ioSurfaceAccessesProjection);
            tableBuilderWithRowCount.AddColumn(bytesColumn, bytesProjection);
            tableBuilderWithRowCount.AddColumn(commandBufferIdColumn, commandBufferIdProjection);
            tableBuilderWithRowCount.AddColumn(encoderIDColumn, encoderIDProjection);
            tableBuilderWithRowCount.AddColumn(gpuSubmissionIdColumn, gpuSubmissionIdProjection);
            tableBuilderWithRowCount.AddColumn(countColumn, Projection.Constant(1));

            var tableConfig = new TableConfiguration("Gpu Usage by Process")
            {
                Columns = new[]
                {
                    processNameColumn,
                    channelNameColumn,
                    colorColumn,
                    TableConfiguration.PivotColumn,
                    countColumn,
                    cpuToGpuLatencyColumn,
                    bytesColumn,
                    frameColumn,
                    depthColumn,
                    connectionUUIDColumn,
                    commandBufferIdColumn,
                    encoderIDColumn,
                    gpuSubmissionIdColumn,
                    stateColumn,
                    metalDeviceColumn,
                    channelSubtitleColumn,
                    ioSurfaceAccessesColumn,
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