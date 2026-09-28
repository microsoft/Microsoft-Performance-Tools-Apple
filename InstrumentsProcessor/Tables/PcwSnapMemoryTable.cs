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
    public sealed class PcwSnapMemoryTable
    {
        public static TableDescriptor TableDescriptor =>
           new TableDescriptor(
              Guid.Parse("{3c4d5e6f-7a8b-4c9d-ae0f-1a2b3c4d5e6f}"),
              "PcwSnap Memory",
              "Per-process memory breakdown from ProcInfo (footprint, resident, internal, external, compressed, graphics, purgeable)",
              "PcwSnap",
              requiredDataCookers: new List<DataCookerPath>
              {
                  PcwSnapProcessCooker.DataCookerPath
              });

        // ---- Identity ----
        private static readonly ColumnConfiguration snapshotCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("c3001001-0001-4000-8000-000000000001"), "Snapshot #") { ShortDescription = "Sequential index of the pcwsnap snapshot" },
            new UIHints { IsVisible = true, Width = 80 });

        private static readonly ColumnConfiguration timeCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("c3001001-0001-4000-8000-000000000002"), "Timestamp") { ShortDescription = "Wall-clock time of the snapshot" },
            new UIHints
            {
                IsVisible = true, Width = 140,
                CellFormat = TimestampFormatter.FormatMicrosecondsGrouped,
                AggregationMode = AggregationMode.Min
            });

        private static readonly ColumnConfiguration pidCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("c3001001-0001-4000-8000-000000000003"), "PID") { ShortDescription = "Process ID from the kcdata task snapshot" },
            new UIHints { IsVisible = true, Width = 70 });

        private static readonly ColumnConfiguration processCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("c3001001-0001-4000-8000-000000000004"), "Process") { ShortDescription = "Process name from the kcdata task snapshot" },
            new UIHints { IsVisible = true, Width = 160 });

        private static readonly ColumnConfiguration countCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("c3001001-0001-4000-8000-000000000005"), "Count") { ShortDescription = "Row count (always 1), useful for aggregation" },
            new UIHints { IsVisible = true, Width = 60, AggregationMode = AggregationMode.Sum });

        // ---- Footprint ----
        private static readonly ColumnConfiguration physFootprintCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("c3001001-0001-4000-8000-000000000010"), "Phys Footprint", "Total physical memory charged to the process, computed by get_task_phys_footprint(). Includes internal (anonymous/private) memory, internal compressed pages, IOKit mappings (GPU/IOSurface buffers), non-volatile purgeable memory, and page table pages. Excludes external (file-backed/shared) pages, reusable memory, and volatile purgeable memory. This is the primary metric used by macOS/iOS for memory pressure decisions and jetsam (OOM killing).") { ShortDescription = "Physical memory footprint in bytes (phys_footprint from XNU task_info)" },
            new UIHints { IsVisible = true, Width = 120, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration deltaPhysFootprintCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("c3001001-0001-4000-8000-000000000011"), "Δ Phys Footprint") { ShortDescription = "Change in physical footprint since previous snapshot" },
            new UIHints { IsVisible = true, Width = 130, AggregationMode = AggregationMode.Sum });

        private static readonly ColumnConfiguration lifetimeMaxFootprintCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("c3001001-0001-4000-8000-000000000012"), "Lifetime Max Footprint") { ShortDescription = "Peak physical memory footprint over the entire process lifetime" },
            new UIHints { IsVisible = true, Width = 150, AggregationMode = AggregationMode.Max });

        // ---- Resident ----
        private static readonly ColumnConfiguration residentCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("c3001001-0001-4000-8000-000000000020"), "Resident", "Total physical RAM currently occupied by the process's pages, regardless of ownership. Includes both internal (anonymous/private) and external (file-backed/shared) pages such as mapped dylibs and frameworks. Does not include compressed pages (they leave RAM) or unmapped virtual pages. A process can have high Resident but low Physical Footprint if it maps many shared libraries, or low Resident but high Physical Footprint if much of its private memory has been compressed.") { ShortDescription = "Current resident set size in bytes (pti_resident_size)" },
            new UIHints { IsVisible = true, Width = 100, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration deltaResidentCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("c3001001-0001-4000-8000-000000000021"), "Δ Resident") { ShortDescription = "Change in resident set size since previous snapshot" },
            new UIHints { IsVisible = true, Width = 100, AggregationMode = AggregationMode.Sum });

        private static readonly ColumnConfiguration maxResidentCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("c3001001-0001-4000-8000-000000000022"), "Max Resident") { ShortDescription = "Peak resident set size in bytes (task_resident_max)" },
            new UIHints { IsVisible = true, Width = 110, AggregationMode = AggregationMode.Max });

        // ---- Internal / External ----
        private static readonly ColumnConfiguration internalCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("c3001001-0001-4000-8000-000000000030"), "Internal") { ShortDescription = "Internal (anonymous/private) memory in bytes, not shared with other processes" },
            new UIHints { IsVisible = true, Width = 100, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration deltaInternalCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("c3001001-0001-4000-8000-000000000031"), "Δ Internal") { ShortDescription = "Change in internal memory since previous snapshot" },
            new UIHints { IsVisible = true, Width = 100, AggregationMode = AggregationMode.Sum });

        private static readonly ColumnConfiguration externalCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("c3001001-0001-4000-8000-000000000032"), "External") { ShortDescription = "External (file-backed/shared) memory in bytes" },
            new UIHints { IsVisible = true, Width = 100, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration deltaExternalCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("c3001001-0001-4000-8000-000000000033"), "Δ External") { ShortDescription = "Change in external memory since previous snapshot" },
            new UIHints { IsVisible = true, Width = 100, AggregationMode = AggregationMode.Sum });

        // ---- Compressed ----
        private static readonly ColumnConfiguration compressedCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("c3001001-0001-4000-8000-000000000040"), "Compressed") { ShortDescription = "Memory currently compressed by the VM compressor in bytes" },
            new UIHints { IsVisible = true, Width = 100, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration deltaCompressedCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("c3001001-0001-4000-8000-000000000041"), "Δ Compressed") { ShortDescription = "Change in compressed memory since previous snapshot" },
            new UIHints { IsVisible = true, Width = 110, AggregationMode = AggregationMode.Sum });

        private static readonly ColumnConfiguration compLifetimeCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("c3001001-0001-4000-8000-000000000042"), "Compressed Lifetime") { ShortDescription = "Cumulative bytes compressed over the process lifetime" },
            new UIHints { IsVisible = true, Width = 140, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration decomprCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("c3001001-0001-4000-8000-000000000043"), "Decompressions") { ShortDescription = "Cumulative number of page decompression operations" },
            new UIHints { IsVisible = true, Width = 110, AggregationMode = AggregationMode.Max });

        // ---- Virtual / Reusable ----
        private static readonly ColumnConfiguration virtualCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("c3001001-0001-4000-8000-000000000050"), "Virtual") { ShortDescription = "Total virtual address space size in bytes (pti_virtual_size)" },
            new UIHints { IsVisible = true, Width = 100, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration reusableCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("c3001001-0001-4000-8000-000000000051"), "Reusable") { ShortDescription = "Memory marked as reusable that can be reclaimed without paging" },
            new UIHints { IsVisible = true, Width = 100, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration regionCountCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("c3001001-0001-4000-8000-000000000052"), "Regions") { ShortDescription = "Number of virtual memory regions in the process address space" },
            new UIHints { IsVisible = true, Width = 80, AggregationMode = AggregationMode.Max });

        // ---- Purgeable ----
        private static readonly ColumnConfiguration purgeableNVCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("c3001001-0001-4000-8000-000000000060"), "Purgeable NV") { ShortDescription = "Non-volatile purgeable memory in bytes" },
            new UIHints { IsVisible = true, Width = 110, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration purgeableNVCompCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("c3001001-0001-4000-8000-000000000061"), "Purgeable NV Compressed") { ShortDescription = "Non-volatile purgeable memory that is currently compressed" },
            new UIHints { IsVisible = true, Width = 160, AggregationMode = AggregationMode.Max });

        // ---- Graphics ----
        private static readonly ColumnConfiguration gfxFootprintCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("c3001001-0001-4000-8000-000000000070"), "Gfx Footprint") { ShortDescription = "GPU/graphics memory footprint in bytes (IOSurface and GPU allocations)" },
            new UIHints { IsVisible = true, Width = 110, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration gfxFootprintCompCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("c3001001-0001-4000-8000-000000000071"), "Gfx Footprint Compressed") { ShortDescription = "GPU/graphics memory that is currently compressed" },
            new UIHints { IsVisible = true, Width = 170, AggregationMode = AggregationMode.Max });

        public static bool IsDataAvailable(IDataExtensionRetrieval requiredData)
        {
            var data = requiredData.QueryOutput<List<PcwSnapProcessEvent>>(
                new DataOutputPath(PcwSnapProcessCooker.DataCookerPath, nameof(PcwSnapProcessCooker.Processes)));
            return data != null && data.Count > 0;
        }

        public static void BuildTable(
            ITableBuilder tableBuilder,
            IDataExtensionRetrieval requiredData)
        {
            var data = requiredData.QueryOutput<List<PcwSnapProcessEvent>>(
                new DataOutputPath(PcwSnapProcessCooker.DataCookerPath, nameof(PcwSnapProcessCooker.Processes)));

            var table = tableBuilder.SetRowCount(data.Count);
            var bp = Projection.Index(data);

            // Identity
            table.AddColumn(snapshotCol, bp.Compose(e => e.SnapshotIndex));
            table.AddColumn(timeCol, bp.Compose(e => e.Timestamp));
            table.AddColumn(pidCol, bp.Compose(e => e.Pid));
            table.AddColumn(processCol, bp.Compose(e => e.ProcessName));
            table.AddColumn(countCol, Projection.Constant(1));

            // Footprint
            table.AddColumn(physFootprintCol, bp.Compose(e => e.PhysFootprint));
            table.AddColumn(deltaPhysFootprintCol, bp.Compose(e => e.DeltaPhysFootprint));
            table.AddColumn(lifetimeMaxFootprintCol, bp.Compose(e => e.LifetimeMaxPhysFootprint));

            // Resident
            table.AddColumn(residentCol, bp.Compose(e => e.Resident));
            table.AddColumn(deltaResidentCol, bp.Compose(e => e.DeltaResident));
            table.AddColumn(maxResidentCol, bp.Compose(e => e.MaxResident));

            // Internal / External
            table.AddColumn(internalCol, bp.Compose(e => e.Internal));
            table.AddColumn(deltaInternalCol, bp.Compose(e => e.DeltaInternal));
            table.AddColumn(externalCol, bp.Compose(e => e.External));
            table.AddColumn(deltaExternalCol, bp.Compose(e => e.DeltaExternal));

            // Compressed
            table.AddColumn(compressedCol, bp.Compose(e => e.Compressed));
            table.AddColumn(deltaCompressedCol, bp.Compose(e => e.DeltaCompressed));
            table.AddColumn(compLifetimeCol, bp.Compose(e => e.CompressedLifetime));
            table.AddColumn(decomprCol, bp.Compose(e => e.Decompressions));

            // Virtual / Reusable
            table.AddColumn(virtualCol, bp.Compose(e => e.Virtual));
            table.AddColumn(reusableCol, bp.Compose(e => e.Reusable));
            table.AddColumn(regionCountCol, bp.Compose(e => e.RegionCount));

            // Purgeable
            table.AddColumn(purgeableNVCol, bp.Compose(e => e.PurgeableNV));
            table.AddColumn(purgeableNVCompCol, bp.Compose(e => e.PurgeableNVComp));

            // Graphics
            table.AddColumn(gfxFootprintCol, bp.Compose(e => e.GfxFootprint));
            table.AddColumn(gfxFootprintCompCol, bp.Compose(e => e.GfxFootprintComp));

            // ---- Configurations ----

            // By Process — footprint focus
            var byProcess = new TableConfiguration("By Process")
            {
                Columns = new[]
                {
                    processCol,
                    pidCol,
                    TableConfiguration.PivotColumn,
                    physFootprintCol,
                    deltaPhysFootprintCol,
                    residentCol,
                    deltaResidentCol,
                    internalCol,
                    deltaInternalCol,
                    externalCol,
                    deltaExternalCol,
                    compressedCol,
                    deltaCompressedCol,
                    virtualCol,
                    gfxFootprintCol,
                    purgeableNVCol,
                    reusableCol,
                    regionCountCol,
                    countCol,
                    TableConfiguration.GraphColumn,
                    timeCol,
                },
            };
            byProcess.AddColumnRole(ColumnRole.StartTime, timeCol);
            byProcess.AddColumnRole(ColumnRole.EndTime, timeCol);
            tableBuilder.AddTableConfiguration(byProcess);

            // Deltas only
            var deltas = new TableConfiguration("Deltas by Process")
            {
                Columns = new[]
                {
                    processCol,
                    pidCol,
                    TableConfiguration.PivotColumn,
                    deltaPhysFootprintCol,
                    deltaResidentCol,
                    deltaInternalCol,
                    deltaExternalCol,
                    deltaCompressedCol,
                    countCol,
                    TableConfiguration.GraphColumn,
                    timeCol,
                },
            };
            deltas.AddColumnRole(ColumnRole.StartTime, timeCol);
            deltas.AddColumnRole(ColumnRole.EndTime, timeCol);
            tableBuilder.AddTableConfiguration(deltas);

            // Full detail
            var full = new TableConfiguration("Full Detail")
            {
                Columns = new[]
                {
                    processCol,
                    pidCol,
                    snapshotCol,
                    TableConfiguration.PivotColumn,
                    physFootprintCol,
                    deltaPhysFootprintCol,
                    lifetimeMaxFootprintCol,
                    residentCol,
                    deltaResidentCol,
                    maxResidentCol,
                    internalCol,
                    deltaInternalCol,
                    externalCol,
                    deltaExternalCol,
                    compressedCol,
                    deltaCompressedCol,
                    compLifetimeCol,
                    decomprCol,
                    virtualCol,
                    reusableCol,
                    regionCountCol,
                    purgeableNVCol,
                    purgeableNVCompCol,
                    gfxFootprintCol,
                    gfxFootprintCompCol,
                    countCol,
                    TableConfiguration.GraphColumn,
                    timeCol,
                },
            };
            full.AddColumnRole(ColumnRole.StartTime, timeCol);
            full.AddColumnRole(ColumnRole.EndTime, timeCol);
            tableBuilder.AddTableConfiguration(full);

            tableBuilder.SetDefaultTableConfiguration(byProcess);
        }
    }
}
