// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using InstrumentsProcessor.Cookers;
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
    public sealed class PcwSnapDiskTable
    {
        public static TableDescriptor TableDescriptor =>
           new TableDescriptor(
              Guid.Parse("{d15c10e0-a001-4b8c-9d0e-1f2a3b4c5d6e}"),
              "PcwSnap Disk I/O",
              "Per-process disk I/O statistics from kcdata IoStats snapshots",
              "PcwSnap",
              requiredDataCookers: new List<DataCookerPath>
              {
                  PcwSnapDiskCooker.DataCookerPath
              });

        // ---- Identity columns ----
        private static readonly ColumnConfiguration snapshotCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c10e0-0001-4000-8000-000000000001"), "Snapshot #") { ShortDescription = "Sequential index of the pcwsnap snapshot" },
            new UIHints { IsVisible = true, Width = 80 });

        private static readonly ColumnConfiguration timeCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c10e0-0001-4000-8000-000000000002"), "Timestamp") { ShortDescription = "Wall-clock time of the snapshot" },
            new UIHints
            {
                IsVisible = true, Width = 140,
                CellFormat = TimestampFormatter.FormatMicrosecondsGrouped,
                AggregationMode = AggregationMode.Min
            });

        private static readonly ColumnConfiguration pidCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c10e0-0001-4000-8000-000000000003"), "PID") { ShortDescription = "Process ID from the kcdata task snapshot" },
            new UIHints { IsVisible = true, Width = 70 });

        private static readonly ColumnConfiguration processCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c10e0-0001-4000-8000-000000000004"), "Process") { ShortDescription = "Process name from the kcdata task snapshot" },
            new UIHints { IsVisible = true, Width = 160 });

        private static readonly ColumnConfiguration countCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c10e0-0001-4000-8000-000000000005"), "Count") { ShortDescription = "Row count (always 1), useful for aggregation" },
            new UIHints { IsVisible = true, Width = 60, AggregationMode = AggregationMode.Sum });

        // ---- Disk Reads ----
        private static readonly ColumnConfiguration diskReadsCountCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c10e0-0001-4000-8000-000000000010"), "Disk Reads Count") { ShortDescription = "Cumulative number of disk read operations (ss_disk_reads_count from XNU io_stats_snapshot)" },
            new UIHints { IsVisible = true, Width = 120, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration diskReadsSizeCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c10e0-0001-4000-8000-000000000011"), "Disk Reads Size") { ShortDescription = "Cumulative bytes read from disk (ss_disk_reads_size from XNU io_stats_snapshot)" },
            new UIHints { IsVisible = true, Width = 120, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration deltaDiskReadsCountCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c10e0-0001-4000-8000-000000000012"), "Δ Disk Reads Count") { ShortDescription = "Change in disk read count since previous snapshot" },
            new UIHints { IsVisible = true, Width = 130, AggregationMode = AggregationMode.Sum });

        private static readonly ColumnConfiguration deltaDiskReadsSizeCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c10e0-0001-4000-8000-000000000013"), "Δ Disk Reads Size") { ShortDescription = "Change in disk read bytes since previous snapshot" },
            new UIHints { IsVisible = true, Width = 130, AggregationMode = AggregationMode.Sum });

        // ---- Disk Writes ----
        private static readonly ColumnConfiguration diskWritesCountCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c10e0-0001-4000-8000-000000000020"), "Disk Writes Count") { ShortDescription = "Cumulative number of disk write operations (total_io.count − disk_reads.count)" },
            new UIHints { IsVisible = true, Width = 120, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration diskWritesSizeCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c10e0-0001-4000-8000-000000000021"), "Disk Writes Size") { ShortDescription = "Cumulative bytes written to disk (total_io.size − disk_reads.size)" },
            new UIHints { IsVisible = true, Width = 120, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration deltaDiskWritesCountCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c10e0-0001-4000-8000-000000000022"), "Δ Disk Writes Count") { ShortDescription = "Change in disk write count since previous snapshot" },
            new UIHints { IsVisible = true, Width = 130, AggregationMode = AggregationMode.Sum });

        private static readonly ColumnConfiguration deltaDiskWritesSizeCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c10e0-0001-4000-8000-000000000023"), "Δ Disk Writes Size") { ShortDescription = "Change in disk write bytes since previous snapshot" },
            new UIHints { IsVisible = true, Width = 130, AggregationMode = AggregationMode.Sum });

        // ---- Paging ----
        private static readonly ColumnConfiguration pagingCountCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c10e0-0001-4000-8000-000000000030"), "Paging Count") { ShortDescription = "Cumulative number of paging I/O operations (ss_paging_count from XNU io_stats_snapshot)" },
            new UIHints { IsVisible = true, Width = 110, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration pagingSizeCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c10e0-0001-4000-8000-000000000031"), "Paging Size") { ShortDescription = "Cumulative bytes of paging I/O (ss_paging_size from XNU io_stats_snapshot)" },
            new UIHints { IsVisible = true, Width = 110, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration deltaPagingCountCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c10e0-0001-4000-8000-000000000032"), "Δ Paging Count") { ShortDescription = "Change in paging I/O count since previous snapshot" },
            new UIHints { IsVisible = true, Width = 120, AggregationMode = AggregationMode.Sum });

        private static readonly ColumnConfiguration deltaPagingSizeCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c10e0-0001-4000-8000-000000000033"), "Δ Paging Size") { ShortDescription = "Change in paging I/O bytes since previous snapshot" },
            new UIHints { IsVisible = true, Width = 120, AggregationMode = AggregationMode.Sum });

        // ---- Non-Paging ----
        private static readonly ColumnConfiguration nonPagingCountCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c10e0-0001-4000-8000-000000000040"), "Non-Paging Count") { ShortDescription = "Cumulative number of non-paging I/O operations (total_io.count − paging.count)" },
            new UIHints { IsVisible = true, Width = 120, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration nonPagingSizeCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c10e0-0001-4000-8000-000000000041"), "Non-Paging Size") { ShortDescription = "Cumulative bytes of non-paging I/O (total_io.size − paging.size)" },
            new UIHints { IsVisible = true, Width = 120, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration deltaNonPagingCountCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c10e0-0001-4000-8000-000000000042"), "Δ Non-Paging Count") { ShortDescription = "Change in non-paging I/O count since previous snapshot" },
            new UIHints { IsVisible = true, Width = 130, AggregationMode = AggregationMode.Sum });

        private static readonly ColumnConfiguration deltaNonPagingSizeCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c10e0-0001-4000-8000-000000000043"), "Δ Non-Paging Size") { ShortDescription = "Change in non-paging I/O bytes since previous snapshot" },
            new UIHints { IsVisible = true, Width = 130, AggregationMode = AggregationMode.Sum });

        // ---- Metadata ----
        private static readonly ColumnConfiguration metadataCountCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c10e0-0001-4000-8000-000000000050"), "Metadata Count") { ShortDescription = "Cumulative number of metadata I/O operations (ss_metadata_count from XNU io_stats_snapshot)" },
            new UIHints { IsVisible = true, Width = 110, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration metadataSizeCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c10e0-0001-4000-8000-000000000051"), "Metadata Size") { ShortDescription = "Cumulative bytes of metadata I/O (ss_metadata_size from XNU io_stats_snapshot)" },
            new UIHints { IsVisible = true, Width = 110, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration deltaMetadataCountCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c10e0-0001-4000-8000-000000000052"), "Δ Metadata Count") { ShortDescription = "Change in metadata I/O count since previous snapshot" },
            new UIHints { IsVisible = true, Width = 120, AggregationMode = AggregationMode.Sum });

        private static readonly ColumnConfiguration deltaMetadataSizeCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c10e0-0001-4000-8000-000000000053"), "Δ Metadata Size") { ShortDescription = "Change in metadata I/O bytes since previous snapshot" },
            new UIHints { IsVisible = true, Width = 120, AggregationMode = AggregationMode.Sum });

        // ---- Data ----
        private static readonly ColumnConfiguration dataCountCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c10e0-0001-4000-8000-000000000060"), "Data Count") { ShortDescription = "Cumulative number of data I/O operations (total_io.count − metadata.count)" },
            new UIHints { IsVisible = true, Width = 100, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration dataSizeCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c10e0-0001-4000-8000-000000000061"), "Data Size") { ShortDescription = "Cumulative bytes of data I/O (total_io.size − metadata.size)" },
            new UIHints { IsVisible = true, Width = 100, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration deltaDataCountCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c10e0-0001-4000-8000-000000000062"), "Δ Data Count") { ShortDescription = "Change in data I/O count since previous snapshot" },
            new UIHints { IsVisible = true, Width = 110, AggregationMode = AggregationMode.Sum });

        private static readonly ColumnConfiguration deltaDataSizeCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c10e0-0001-4000-8000-000000000063"), "Δ Data Size") { ShortDescription = "Change in data I/O bytes since previous snapshot" },
            new UIHints { IsVisible = true, Width = 110, AggregationMode = AggregationMode.Sum });

        // ---- I/O Priority 0..3 ----
        private static readonly ColumnConfiguration ioPri0CountCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c10e0-0001-4000-8000-000000000070"), "IO Priority 0 Count") { ShortDescription = "Cumulative I/O count at priority tier 0 (highest priority, no throttle)" },
            new UIHints { IsVisible = true, Width = 130, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration ioPri0SizeCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c10e0-0001-4000-8000-000000000071"), "IO Priority 0 Size") { ShortDescription = "Cumulative I/O bytes at priority tier 0 (highest priority, no throttle)" },
            new UIHints { IsVisible = true, Width = 130, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration deltaIoPri0CountCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c10e0-0001-4000-8000-000000000072"), "Δ IO Priority 0 Count") { ShortDescription = "Change in tier-0 I/O count since previous snapshot" },
            new UIHints { IsVisible = true, Width = 140, AggregationMode = AggregationMode.Sum });

        private static readonly ColumnConfiguration deltaIoPri0SizeCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c10e0-0001-4000-8000-000000000073"), "Δ IO Priority 0 Size") { ShortDescription = "Change in tier-0 I/O bytes since previous snapshot" },
            new UIHints { IsVisible = true, Width = 140, AggregationMode = AggregationMode.Sum });

        private static readonly ColumnConfiguration ioPri1CountCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c10e0-0001-4000-8000-000000000074"), "IO Priority 1 Count") { ShortDescription = "Cumulative I/O count at priority tier 1" },
            new UIHints { IsVisible = true, Width = 130, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration ioPri1SizeCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c10e0-0001-4000-8000-000000000075"), "IO Priority 1 Size") { ShortDescription = "Cumulative I/O bytes at priority tier 1" },
            new UIHints { IsVisible = true, Width = 130, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration deltaIoPri1CountCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c10e0-0001-4000-8000-000000000076"), "Δ IO Priority 1 Count") { ShortDescription = "Change in tier-1 I/O count since previous snapshot" },
            new UIHints { IsVisible = true, Width = 140, AggregationMode = AggregationMode.Sum });

        private static readonly ColumnConfiguration deltaIoPri1SizeCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c10e0-0001-4000-8000-000000000077"), "Δ IO Priority 1 Size") { ShortDescription = "Change in tier-1 I/O bytes since previous snapshot" },
            new UIHints { IsVisible = true, Width = 140, AggregationMode = AggregationMode.Sum });

        private static readonly ColumnConfiguration ioPri2CountCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c10e0-0001-4000-8000-000000000078"), "IO Priority 2 Count") { ShortDescription = "Cumulative I/O count at priority tier 2" },
            new UIHints { IsVisible = true, Width = 130, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration ioPri2SizeCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c10e0-0001-4000-8000-000000000079"), "IO Priority 2 Size") { ShortDescription = "Cumulative I/O bytes at priority tier 2" },
            new UIHints { IsVisible = true, Width = 130, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration deltaIoPri2CountCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c10e0-0001-4000-8000-00000000007a"), "Δ IO Priority 2 Count") { ShortDescription = "Change in tier-2 I/O count since previous snapshot" },
            new UIHints { IsVisible = true, Width = 140, AggregationMode = AggregationMode.Sum });

        private static readonly ColumnConfiguration deltaIoPri2SizeCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c10e0-0001-4000-8000-00000000007b"), "Δ IO Priority 2 Size") { ShortDescription = "Change in tier-2 I/O bytes since previous snapshot" },
            new UIHints { IsVisible = true, Width = 140, AggregationMode = AggregationMode.Sum });

        private static readonly ColumnConfiguration ioPri3CountCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c10e0-0001-4000-8000-00000000007c"), "IO Priority 3 Count") { ShortDescription = "Cumulative I/O count at priority tier 3 (lowest priority, most throttled)" },
            new UIHints { IsVisible = true, Width = 130, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration ioPri3SizeCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c10e0-0001-4000-8000-00000000007d"), "IO Priority 3 Size") { ShortDescription = "Cumulative I/O bytes at priority tier 3 (lowest priority, most throttled)" },
            new UIHints { IsVisible = true, Width = 130, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration deltaIoPri3CountCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c10e0-0001-4000-8000-00000000007e"), "Δ IO Priority 3 Count") { ShortDescription = "Change in tier-3 I/O count since previous snapshot" },
            new UIHints { IsVisible = true, Width = 140, AggregationMode = AggregationMode.Sum });

        private static readonly ColumnConfiguration deltaIoPri3SizeCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c10e0-0001-4000-8000-00000000007f"), "Δ IO Priority 3 Size") { ShortDescription = "Change in tier-3 I/O bytes since previous snapshot" },
            new UIHints { IsVisible = true, Width = 140, AggregationMode = AggregationMode.Sum });

        // ---- Rates ----
        private static readonly ColumnConfiguration diskReadRateCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c10e0-0001-4000-8000-000000000080"), "Disk Read Rate (B/s)") { ShortDescription = "Disk read throughput (Δ bytes / Δ time between snapshots). Right-click to switch units." },
            new UIHints { IsVisible = true, Width = 150, AggregationMode = AggregationMode.Average });

        private static readonly ColumnConfiguration diskWriteRateCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d15c10e0-0001-4000-8000-000000000081"), "Disk Write Rate (B/s)") { ShortDescription = "Disk write throughput (Δ bytes / Δ time between snapshots). Right-click to switch units." },
            new UIHints { IsVisible = true, Width = 150, AggregationMode = AggregationMode.Average });

        // Variant descriptors for rate unit switching
        private static readonly ColumnVariantDescriptor readRateBps = new ColumnVariantDescriptor(
            new Guid("d15c10e0-0001-4000-8000-000000000090"),
            new ColumnVariantProperties { Label = "B/s", ColumnName = "Disk Read Rate (B/s)" });
        private static readonly ColumnVariantDescriptor readRateKBps = new ColumnVariantDescriptor(
            new Guid("d15c10e0-0001-4000-8000-000000000091"),
            new ColumnVariantProperties { Label = "KB/s", ColumnName = "Disk Read Rate (KB/s)" });
        private static readonly ColumnVariantDescriptor readRateMBps = new ColumnVariantDescriptor(
            new Guid("d15c10e0-0001-4000-8000-000000000092"),
            new ColumnVariantProperties { Label = "MB/s", ColumnName = "Disk Read Rate (MB/s)" });

        private static readonly ColumnVariantDescriptor writeRateBps = new ColumnVariantDescriptor(
            new Guid("d15c10e0-0001-4000-8000-0000000000a0"),
            new ColumnVariantProperties { Label = "B/s", ColumnName = "Disk Write Rate (B/s)" });
        private static readonly ColumnVariantDescriptor writeRateKBps = new ColumnVariantDescriptor(
            new Guid("d15c10e0-0001-4000-8000-0000000000a1"),
            new ColumnVariantProperties { Label = "KB/s", ColumnName = "Disk Write Rate (KB/s)" });
        private static readonly ColumnVariantDescriptor writeRateMBps = new ColumnVariantDescriptor(
            new Guid("d15c10e0-0001-4000-8000-0000000000a2"),
            new ColumnVariantProperties { Label = "MB/s", ColumnName = "Disk Write Rate (MB/s)" });

        public static bool IsDataAvailable(IDataExtensionRetrieval requiredData)
        {
            var data = requiredData.QueryOutput<List<PcwSnapDiskEvent>>(
                new DataOutputPath(PcwSnapDiskCooker.DataCookerPath, nameof(PcwSnapDiskCooker.DiskEvents)));
            return data != null && data.Count > 0;
        }

        public static void BuildTable(
            ITableBuilder tableBuilder,
            IDataExtensionRetrieval requiredData)
        {
            var data = requiredData.QueryOutput<List<PcwSnapDiskEvent>>(
                new DataOutputPath(PcwSnapDiskCooker.DataCookerPath, nameof(PcwSnapDiskCooker.DiskEvents)));

            var table = tableBuilder.SetRowCount(data.Count);
            var bp = Projection.Index(data);

            // Identity
            table.AddColumn(snapshotCol, bp.Compose(e => e.SnapshotIndex));
            table.AddColumn(timeCol, bp.Compose(e => e.Timestamp));
            table.AddColumn(pidCol, bp.Compose(e => e.Pid));
            table.AddColumn(processCol, bp.Compose(e => e.ProcessName));
            table.AddColumn(countCol, Projection.Constant(1));

            // Disk Reads
            table.AddColumn(diskReadsCountCol, bp.Compose(e => e.DiskReadsCount));
            table.AddColumn(diskReadsSizeCol, bp.Compose(e => e.DiskReadsSize));
            table.AddColumn(deltaDiskReadsCountCol, bp.Compose(e => e.DeltaDiskReadsCount));
            table.AddColumn(deltaDiskReadsSizeCol, bp.Compose(e => e.DeltaDiskReadsSize));

            // Disk Writes
            table.AddColumn(diskWritesCountCol, bp.Compose(e => e.DiskWritesCount));
            table.AddColumn(diskWritesSizeCol, bp.Compose(e => e.DiskWritesSize));
            table.AddColumn(deltaDiskWritesCountCol, bp.Compose(e => e.DeltaDiskWritesCount));
            table.AddColumn(deltaDiskWritesSizeCol, bp.Compose(e => e.DeltaDiskWritesSize));

            // Paging
            table.AddColumn(pagingCountCol, bp.Compose(e => e.PagingCount));
            table.AddColumn(pagingSizeCol, bp.Compose(e => e.PagingSize));
            table.AddColumn(deltaPagingCountCol, bp.Compose(e => e.DeltaPagingCount));
            table.AddColumn(deltaPagingSizeCol, bp.Compose(e => e.DeltaPagingSize));

            // Non-Paging
            table.AddColumn(nonPagingCountCol, bp.Compose(e => e.NonPagingCount));
            table.AddColumn(nonPagingSizeCol, bp.Compose(e => e.NonPagingSize));
            table.AddColumn(deltaNonPagingCountCol, bp.Compose(e => e.DeltaNonPagingCount));
            table.AddColumn(deltaNonPagingSizeCol, bp.Compose(e => e.DeltaNonPagingSize));

            // Metadata
            table.AddColumn(metadataCountCol, bp.Compose(e => e.MetadataCount));
            table.AddColumn(metadataSizeCol, bp.Compose(e => e.MetadataSize));
            table.AddColumn(deltaMetadataCountCol, bp.Compose(e => e.DeltaMetadataCount));
            table.AddColumn(deltaMetadataSizeCol, bp.Compose(e => e.DeltaMetadataSize));

            // Data
            table.AddColumn(dataCountCol, bp.Compose(e => e.DataCount));
            table.AddColumn(dataSizeCol, bp.Compose(e => e.DataSize));
            table.AddColumn(deltaDataCountCol, bp.Compose(e => e.DeltaDataCount));
            table.AddColumn(deltaDataSizeCol, bp.Compose(e => e.DeltaDataSize));

            // I/O Priority
            table.AddColumn(ioPri0CountCol, bp.Compose(e => e.IoPriority0Count));
            table.AddColumn(ioPri0SizeCol, bp.Compose(e => e.IoPriority0Size));
            table.AddColumn(deltaIoPri0CountCol, bp.Compose(e => e.DeltaIoPriority0Count));
            table.AddColumn(deltaIoPri0SizeCol, bp.Compose(e => e.DeltaIoPriority0Size));
            table.AddColumn(ioPri1CountCol, bp.Compose(e => e.IoPriority1Count));
            table.AddColumn(ioPri1SizeCol, bp.Compose(e => e.IoPriority1Size));
            table.AddColumn(deltaIoPri1CountCol, bp.Compose(e => e.DeltaIoPriority1Count));
            table.AddColumn(deltaIoPri1SizeCol, bp.Compose(e => e.DeltaIoPriority1Size));
            table.AddColumn(ioPri2CountCol, bp.Compose(e => e.IoPriority2Count));
            table.AddColumn(ioPri2SizeCol, bp.Compose(e => e.IoPriority2Size));
            table.AddColumn(deltaIoPri2CountCol, bp.Compose(e => e.DeltaIoPriority2Count));
            table.AddColumn(deltaIoPri2SizeCol, bp.Compose(e => e.DeltaIoPriority2Size));
            table.AddColumn(ioPri3CountCol, bp.Compose(e => e.IoPriority3Count));
            table.AddColumn(ioPri3SizeCol, bp.Compose(e => e.IoPriority3Size));
            table.AddColumn(deltaIoPri3CountCol, bp.Compose(e => e.DeltaIoPriority3Count));
            table.AddColumn(deltaIoPri3SizeCol, bp.Compose(e => e.DeltaIoPriority3Size));

            // Rates (modal columns: user can switch between B/s, KB/s, MB/s)
            table.AddColumnWithVariants(
                diskReadRateCol,
                bp.Compose(e => e.DiskReadRate),
                builder => builder
                    .WithModes(new ColumnVariantProperties { Label = "Unit" })
                    .WithMode(readRateBps,  bp.Compose(e => e.DiskReadRate))
                    .WithMode(readRateKBps, bp.Compose(e => e.DiskReadRateKB))
                    .WithMode(readRateMBps, bp.Compose(e => e.DiskReadRateMB))
                    .WithDefaultMode(readRateKBps.Guid));

            table.AddColumnWithVariants(
                diskWriteRateCol,
                bp.Compose(e => e.DiskWriteRate),
                builder => builder
                    .WithModes(new ColumnVariantProperties { Label = "Unit" })
                    .WithMode(writeRateBps,  bp.Compose(e => e.DiskWriteRate))
                    .WithMode(writeRateKBps, bp.Compose(e => e.DiskWriteRateKB))
                    .WithMode(writeRateMBps, bp.Compose(e => e.DiskWriteRateMB))
                    .WithDefaultMode(writeRateKBps.Guid));

            // ---- Table configurations ----

            // Overview: reads + writes with deltas
            var overviewConfig = new TableConfiguration("Overview")
            {
                Columns = new[]
                {
                    processCol,
                    pidCol,
                    TableConfiguration.PivotColumn,
                    deltaDiskReadsCountCol,
                    deltaDiskReadsSizeCol,
                    deltaDiskWritesCountCol,
                    deltaDiskWritesSizeCol,
                    diskReadsCountCol,
                    diskReadsSizeCol,
                    diskWritesCountCol,
                    diskWritesSizeCol,
                    countCol,
                    TableConfiguration.GraphColumn,
                    timeCol,
                },
            };
            overviewConfig.AddColumnRole(ColumnRole.StartTime, timeCol);
            overviewConfig.AddColumnRole(ColumnRole.EndTime, timeCol);
            tableBuilder.AddTableConfiguration(overviewConfig);

            // Paging
            var pagingConfig = new TableConfiguration("Paging")
            {
                Columns = new[]
                {
                    processCol,
                    pidCol,
                    TableConfiguration.PivotColumn,
                    deltaPagingCountCol,
                    deltaPagingSizeCol,
                    deltaNonPagingCountCol,
                    deltaNonPagingSizeCol,
                    pagingCountCol,
                    pagingSizeCol,
                    nonPagingCountCol,
                    nonPagingSizeCol,
                    countCol,
                    TableConfiguration.GraphColumn,
                    timeCol,
                },
            };
            pagingConfig.AddColumnRole(ColumnRole.StartTime, timeCol);
            pagingConfig.AddColumnRole(ColumnRole.EndTime, timeCol);
            tableBuilder.AddTableConfiguration(pagingConfig);

            // By Type (metadata + data)
            var typeConfig = new TableConfiguration("By Type")
            {
                Columns = new[]
                {
                    processCol,
                    pidCol,
                    TableConfiguration.PivotColumn,
                    deltaMetadataCountCol,
                    deltaMetadataSizeCol,
                    deltaDataCountCol,
                    deltaDataSizeCol,
                    metadataCountCol,
                    metadataSizeCol,
                    dataCountCol,
                    dataSizeCol,
                    countCol,
                    TableConfiguration.GraphColumn,
                    timeCol,
                },
            };
            typeConfig.AddColumnRole(ColumnRole.StartTime, timeCol);
            typeConfig.AddColumnRole(ColumnRole.EndTime, timeCol);
            tableBuilder.AddTableConfiguration(typeConfig);

            // By Priority
            var priorityConfig = new TableConfiguration("By Priority")
            {
                Columns = new[]
                {
                    processCol,
                    pidCol,
                    TableConfiguration.PivotColumn,
                    deltaIoPri0CountCol,
                    deltaIoPri0SizeCol,
                    deltaIoPri1CountCol,
                    deltaIoPri1SizeCol,
                    deltaIoPri2CountCol,
                    deltaIoPri2SizeCol,
                    deltaIoPri3CountCol,
                    deltaIoPri3SizeCol,
                    ioPri0CountCol,
                    ioPri0SizeCol,
                    ioPri1CountCol,
                    ioPri1SizeCol,
                    ioPri2CountCol,
                    ioPri2SizeCol,
                    ioPri3CountCol,
                    ioPri3SizeCol,
                    countCol,
                    TableConfiguration.GraphColumn,
                    timeCol,
                },
            };
            priorityConfig.AddColumnRole(ColumnRole.StartTime, timeCol);
            priorityConfig.AddColumnRole(ColumnRole.EndTime, timeCol);
            tableBuilder.AddTableConfiguration(priorityConfig);

            // Rates: line graph of read/write throughput over time
            var ratesConfig = new TableConfiguration("Rates")
            {
                Columns = new[]
                {
                    processCol,
                    pidCol,
                    TableConfiguration.PivotColumn,
                    diskReadRateCol,
                    diskWriteRateCol,
                    countCol,
                    TableConfiguration.GraphColumn,
                    timeCol,
                },
                ChartType = ChartType.Line,
            };
            ratesConfig.AddColumnRole(ColumnRole.StartTime, timeCol);
            ratesConfig.AddColumnRole(ColumnRole.EndTime, timeCol);
            tableBuilder.AddTableConfiguration(ratesConfig);

            tableBuilder.SetDefaultTableConfiguration(overviewConfig);
        }
    }
}
