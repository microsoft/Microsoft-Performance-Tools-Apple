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
    /// <summary>
    /// Per-process metrics from ProcInfo records, mirroring snap_report.lua categories:
    /// basic, memory, cpu, io, power — with deltas between snapshots.
    /// </summary>
    [Table]
    public sealed class PcwSnapProcessTable
    {
        public static TableDescriptor TableDescriptor =>
           new TableDescriptor(
              Guid.Parse("{2b3c4d5e-6f7a-4b8c-9d0e-1f2a3b4c5d6e}"),
              "PcwSnap Processes",
              "Per-process ProcInfo metrics (memory, CPU, I/O, power) with delta breakdown",
              "PcwSnap",
              requiredDataCookers: new List<DataCookerPath>
              {
                  PcwSnapProcessCooker.DataCookerPath
              });

        // ---- Identity columns ----
        private static readonly ColumnConfiguration snapshotCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000001"), "Snapshot #") { ShortDescription = "Sequential index of the pcwsnap snapshot" },
            new UIHints { IsVisible = true, Width = 80 });

        private static readonly ColumnConfiguration timeCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000002"), "Timestamp") { ShortDescription = "Wall-clock time of the snapshot" },
            new UIHints
            {
                IsVisible = true, Width = 140,
                CellFormat = TimestampFormatter.FormatMicrosecondsGrouped,
                AggregationMode = AggregationMode.Min
            });

        private static readonly ColumnConfiguration pidCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000003"), "PID") { ShortDescription = "Process ID from the kcdata task snapshot" },
            new UIHints { IsVisible = true, Width = 70 });

        private static readonly ColumnConfiguration processCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000004"), "Process") { ShortDescription = "Process name from the kcdata task snapshot" },
            new UIHints { IsVisible = true, Width = 160 });

        private static readonly ColumnConfiguration countCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000005"), "Count") { ShortDescription = "Row count (always 1), useful for aggregation" },
            new UIHints { IsVisible = true, Width = 60, AggregationMode = AggregationMode.Sum });

        // ---- Basic ----
        private static readonly ColumnConfiguration physFootprintCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000010"), "Phys Footprint", "Total physical memory charged to the process, computed by get_task_phys_footprint(). Includes internal (anonymous/private) memory, internal compressed pages, IOKit mappings (GPU/IOSurface buffers), non-volatile purgeable memory, and page table pages. Excludes external (file-backed/shared) pages, reusable memory, and volatile purgeable memory. This is the primary metric used by macOS/iOS for memory pressure decisions and jetsam (OOM killing).") { ShortDescription = "Physical memory footprint in bytes (phys_footprint from XNU task_info)" },
            new UIHints { IsVisible = true, Width = 120, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration cpuUsageMsCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000015"), "CPU Usage (ms)") { ShortDescription = "Total delta CPU time (user + system) in milliseconds since previous snapshot" },
            new UIHints { IsVisible = true, Width = 120, AggregationMode = AggregationMode.Sum });

        private static readonly ColumnConfiguration deltaCpuSysCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000013"), "Δ CPU Sys (s)") { ShortDescription = "Change in kernel/system CPU time in seconds since previous snapshot" },
            new UIHints { IsVisible = true, Width = 110, AggregationMode = AggregationMode.Sum });

        private static readonly ColumnConfiguration deltaCpuUserCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000014"), "Δ CPU User (s)") { ShortDescription = "Change in user-space CPU time in seconds since previous snapshot" },
            new UIHints { IsVisible = true, Width = 110, AggregationMode = AggregationMode.Sum });

        // ---- Memory ----
        private static readonly ColumnConfiguration residentCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000020"), "Resident", "Total physical RAM currently occupied by the process's pages, regardless of ownership. Includes both internal (anonymous/private) and external (file-backed/shared) pages such as mapped dylibs and frameworks. Does not include compressed pages (they leave RAM) or unmapped virtual pages. A process can have high Resident but low Physical Footprint if it maps many shared libraries, or low Resident but high Physical Footprint if much of its private memory has been compressed.") { ShortDescription = "Current resident set size in bytes (pti_resident_size)" },
            new UIHints { IsVisible = true, Width = 100, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration maxResidentCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000021"), "Max Resident") { ShortDescription = "Peak resident set size in bytes (task_resident_max)" },
            new UIHints { IsVisible = true, Width = 110, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration internalCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000022"), "Internal") { ShortDescription = "Internal (anonymous/private) memory in bytes, not shared with other processes" },
            new UIHints { IsVisible = true, Width = 100, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration externalCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000023"), "External") { ShortDescription = "External (file-backed/shared) memory in bytes" },
            new UIHints { IsVisible = true, Width = 100, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration compressedCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000024"), "Compressed") { ShortDescription = "Memory currently compressed by the VM compressor in bytes" },
            new UIHints { IsVisible = true, Width = 100, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration virtualCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000025"), "Virtual") { ShortDescription = "Total virtual address space size in bytes (pti_virtual_size)" },
            new UIHints { IsVisible = true, Width = 100, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration reusableCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000026"), "Reusable") { ShortDescription = "Memory marked as reusable that can be reclaimed without paging" },
            new UIHints { IsVisible = true, Width = 100, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration purgeableNVCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000027"), "Purgeable NV") { ShortDescription = "Non-volatile purgeable memory in bytes" },
            new UIHints { IsVisible = true, Width = 100, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration gfxFootprintCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000028"), "Gfx Footprint") { ShortDescription = "GPU/graphics memory footprint in bytes (IOSurface and GPU allocations)" },
            new UIHints { IsVisible = true, Width = 110, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration compLifetimeCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000029"), "Compressed Lifetime") { ShortDescription = "Cumulative bytes compressed over the process lifetime" },
            new UIHints { IsVisible = true, Width = 130, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration decomprCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-00000000002a"), "Decompressions") { ShortDescription = "Cumulative number of page decompression operations" },
            new UIHints { IsVisible = true, Width = 110, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration regionCountCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-00000000002b"), "Regions") { ShortDescription = "Number of virtual memory regions in the process address space" },
            new UIHints { IsVisible = true, Width = 80, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration deltaPhysFootprintCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-00000000002c"), "Δ Phys Footprint") { ShortDescription = "Change in physical footprint since previous snapshot" },
            new UIHints { IsVisible = true, Width = 130, AggregationMode = AggregationMode.Sum });

        private static readonly ColumnConfiguration deltaResidentCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-00000000002d"), "Δ Resident") { ShortDescription = "Change in resident set size since previous snapshot" },
            new UIHints { IsVisible = true, Width = 100, AggregationMode = AggregationMode.Sum });

        private static readonly ColumnConfiguration deltaInternalCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-00000000002e"), "Δ Internal") { ShortDescription = "Change in internal memory since previous snapshot" },
            new UIHints { IsVisible = true, Width = 100, AggregationMode = AggregationMode.Sum });

        private static readonly ColumnConfiguration deltaExternalCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-00000000002f"), "Δ External") { ShortDescription = "Change in external memory since previous snapshot" },
            new UIHints { IsVisible = true, Width = 100, AggregationMode = AggregationMode.Sum });

        private static readonly ColumnConfiguration deltaCompressedCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000030"), "Δ Compressed") { ShortDescription = "Change in compressed memory since previous snapshot" },
            new UIHints { IsVisible = true, Width = 110, AggregationMode = AggregationMode.Sum });

        // ---- CPU ----
        private static readonly ColumnConfiguration threadsCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000040"), "Threads") { ShortDescription = "Total number of threads in the process (pti_threadnum)" },
            new UIHints { IsVisible = true, Width = 70, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration threadsRunningCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000041"), "Threads Running") { ShortDescription = "Number of threads currently in a running state (pti_numrunning)" },
            new UIHints { IsVisible = true, Width = 110, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration cswCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000042"), "Context Switches") { ShortDescription = "Cumulative voluntary and involuntary context switches (pti_csw)" },
            new UIHints { IsVisible = true, Width = 120, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration syscallsCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000043"), "Syscalls") { ShortDescription = "Cumulative system calls (Mach + Unix combined from pti_syscalls_mach + pti_syscalls_unix)" },
            new UIHints { IsVisible = true, Width = 90, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration instructionsCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000044"), "Instructions") { ShortDescription = "Cumulative retired instructions across all CPU cores (ics_instructions)" },
            new UIHints { IsVisible = true, Width = 110, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration cyclesCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000045"), "Cycles") { ShortDescription = "Cumulative CPU cycles consumed across all cores (ics_cycles)" },
            new UIHints { IsVisible = true, Width = 100, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration pInstructionsCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000046"), "P-Instructions") { ShortDescription = "Cumulative instructions retired on Performance (P) cores only (ics_p_instructions)" },
            new UIHints { IsVisible = true, Width = 120, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration pCyclesCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000047"), "P-Cycles") { ShortDescription = "Cumulative CPU cycles consumed on Performance (P) cores only (ics_p_cycles)" },
            new UIHints { IsVisible = true, Width = 100, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration ipcCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000048"), "IPC") { ShortDescription = "Instructions Per Cycle ratio (instructions / cycles) — higher is more efficient" },
            new UIHints { IsVisible = true, Width = 70, AggregationMode = AggregationMode.Average });

        private static readonly ColumnConfiguration runnableTimeCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000049"), "Runnable Time (s)") { ShortDescription = "Time in seconds the task spent runnable but waiting to be scheduled" },
            new UIHints { IsVisible = true, Width = 120, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration deltaInstrCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-00000000004a"), "Δ Instructions") { ShortDescription = "Change in retired instructions since previous snapshot" },
            new UIHints { IsVisible = true, Width = 120, AggregationMode = AggregationMode.Sum });

        private static readonly ColumnConfiguration deltaCyclesCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-00000000004b"), "Δ Cycles") { ShortDescription = "Change in CPU cycles since previous snapshot" },
            new UIHints { IsVisible = true, Width = 100, AggregationMode = AggregationMode.Sum });

        private static readonly ColumnConfiguration deltaPInstrCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-00000000004c"), "Δ P-Instructions") { ShortDescription = "Change in P-core retired instructions since previous snapshot" },
            new UIHints { IsVisible = true, Width = 130, AggregationMode = AggregationMode.Sum });

        private static readonly ColumnConfiguration deltaPCyclesCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-00000000004d"), "Δ P-Cycles") { ShortDescription = "Change in P-core CPU cycles since previous snapshot" },
            new UIHints { IsVisible = true, Width = 110, AggregationMode = AggregationMode.Sum });

        private static readonly ColumnConfiguration deltaCSWCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-00000000004e"), "Δ Context Switches") { ShortDescription = "Change in context switches since previous snapshot" },
            new UIHints { IsVisible = true, Width = 140, AggregationMode = AggregationMode.Sum });

        private static readonly ColumnConfiguration deltaSyscallsCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-00000000004f"), "Δ Syscalls") { ShortDescription = "Change in total system calls since previous snapshot" },
            new UIHints { IsVisible = true, Width = 100, AggregationMode = AggregationMode.Sum });

        private static readonly ColumnConfiguration deltaIPCCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000050"), "Δ IPC") { ShortDescription = "Change in Instructions Per Cycle ratio since previous snapshot" },
            new UIHints { IsVisible = true, Width = 70, AggregationMode = AggregationMode.Average });

        // QoS breakdown
        private static readonly ColumnConfiguration qosUICol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000051"), "QoS UI (s)") { ShortDescription = "CPU time in seconds at User Interactive QoS tier (highest priority, UI work)" },
            new UIHints { IsVisible = true, Width = 90, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration qosUInitCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000052"), "QoS User-Init (s)") { ShortDescription = "CPU time in seconds at User Initiated QoS tier (user-triggered tasks)" },
            new UIHints { IsVisible = true, Width = 120, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration qosDefaultCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000053"), "QoS Default (s)") { ShortDescription = "CPU time in seconds at Default QoS tier" },
            new UIHints { IsVisible = true, Width = 110, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration qosUtilityCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000054"), "QoS Utility (s)") { ShortDescription = "CPU time in seconds at Utility QoS tier (long-running non-UI work)" },
            new UIHints { IsVisible = true, Width = 110, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration qosBGCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000055"), "QoS Background (s)") { ShortDescription = "CPU time in seconds at Background QoS tier (low-priority maintenance work)" },
            new UIHints { IsVisible = true, Width = 130, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration qosMaintCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000056"), "QoS Maintenance (s)") { ShortDescription = "CPU time in seconds at Maintenance QoS tier (lowest priority, housekeeping)" },
            new UIHints { IsVisible = true, Width = 130, AggregationMode = AggregationMode.Max });

        // QoS deltas
        private static readonly ColumnConfiguration deltaQosUICol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000091"), "\u0394 QoS UI (s)") { ShortDescription = "Change in User Interactive QoS CPU time since previous snapshot" },
            new UIHints { IsVisible = true, Width = 100, AggregationMode = AggregationMode.Sum });

        private static readonly ColumnConfiguration deltaQosUInitCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000092"), "\u0394 QoS User-Init (s)") { ShortDescription = "Change in User Initiated QoS CPU time since previous snapshot" },
            new UIHints { IsVisible = true, Width = 130, AggregationMode = AggregationMode.Sum });

        private static readonly ColumnConfiguration deltaQosDefaultCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000093"), "\u0394 QoS Default (s)") { ShortDescription = "Change in Default QoS CPU time since previous snapshot" },
            new UIHints { IsVisible = true, Width = 120, AggregationMode = AggregationMode.Sum });

        private static readonly ColumnConfiguration deltaQosUtilityCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000094"), "\u0394 QoS Utility (s)") { ShortDescription = "Change in Utility QoS CPU time since previous snapshot" },
            new UIHints { IsVisible = true, Width = 120, AggregationMode = AggregationMode.Sum });

        private static readonly ColumnConfiguration deltaQosBGCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000095"), "\u0394 QoS Background (s)") { ShortDescription = "Change in Background QoS CPU time since previous snapshot" },
            new UIHints { IsVisible = true, Width = 140, AggregationMode = AggregationMode.Sum });

        private static readonly ColumnConfiguration deltaQosMaintCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000096"), "\u0394 QoS Maintenance (s)") { ShortDescription = "Change in Maintenance QoS CPU time since previous snapshot" },
            new UIHints { IsVisible = true, Width = 140, AggregationMode = AggregationMode.Sum });

        private static readonly ColumnConfiguration deltaRunnableTimeCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000097"), "\u0394 Runnable Time (s)") { ShortDescription = "Change in runnable time since previous snapshot" },
            new UIHints { IsVisible = true, Width = 130, AggregationMode = AggregationMode.Sum });

        // ---- I/O ----
        private static readonly ColumnConfiguration logicalWritesCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000060"), "Logical Writes") { ShortDescription = "Cumulative bytes of logical disk writes by the process" },
            new UIHints { IsVisible = true, Width = 110, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration deltaLogicalWritesCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000061"), "Δ Logical Writes") { ShortDescription = "Change in logical disk writes since previous snapshot" },
            new UIHints { IsVisible = true, Width = 120, AggregationMode = AggregationMode.Sum });

        // ---- Power ----
        private static readonly ColumnConfiguration energyCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000070"), "Energy (nJ)") { ShortDescription = "Cumulative energy consumed in nanojoules (recount_task_energy_nj)" },
            new UIHints { IsVisible = true, Width = 100, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration pEnergyCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000071"), "P-Energy (nJ)") { ShortDescription = "Cumulative energy consumed on Performance (P) cores in nanojoules" },
            new UIHints { IsVisible = true, Width = 110, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration billedEnergyCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000072"), "Billed Energy") { ShortDescription = "Energy billed to this task, including work done on its behalf by other tasks" },
            new UIHints { IsVisible = true, Width = 110, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration servicedEnergyCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000073"), "Serviced Energy") { ShortDescription = "Energy from work this task performed on behalf of other tasks" },
            new UIHints { IsVisible = true, Width = 110, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration intWkupsCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000074"), "Interrupt Wakeups") { ShortDescription = "Cumulative times the task was woken by hardware interrupts (task_interrupt_wakeups)" },
            new UIHints { IsVisible = true, Width = 120, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration pkgIdleWkupsCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000075"), "Pkg Idle Wakeups") { ShortDescription = "Cumulative times the task caused the CPU package to exit idle state (expensive)" },
            new UIHints { IsVisible = true, Width = 120, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration msgSentCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000076"), "Msg Sent") { ShortDescription = "Cumulative Mach IPC messages sent by the task" },
            new UIHints { IsVisible = true, Width = 90, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration msgRecvCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000077"), "Msg Recv") { ShortDescription = "Cumulative Mach IPC messages received by the task" },
            new UIHints { IsVisible = true, Width = 90, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration deltaEnergyCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000078"), "Δ Energy (nJ)") { ShortDescription = "Change in energy consumed since previous snapshot" },
            new UIHints { IsVisible = true, Width = 110, AggregationMode = AggregationMode.Sum });

        private static readonly ColumnConfiguration deltaPEnergyCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-000000000079"), "Δ P-Energy (nJ)") { ShortDescription = "Change in P-core energy consumed since previous snapshot" },
            new UIHints { IsVisible = true, Width = 120, AggregationMode = AggregationMode.Sum });

        private static readonly ColumnConfiguration deltaIntWkupsCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-00000000007a"), "Δ Int Wakeups") { ShortDescription = "Change in interrupt wakeups since previous snapshot" },
            new UIHints { IsVisible = true, Width = 110, AggregationMode = AggregationMode.Sum });

        private static readonly ColumnConfiguration deltaPkgIdleWkupsCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-00000000007b"), "Δ Pkg Idle Wkups") { ShortDescription = "Change in package idle wakeups since previous snapshot" },
            new UIHints { IsVisible = true, Width = 130, AggregationMode = AggregationMode.Sum });

        private static readonly ColumnConfiguration deltaMsgSentCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-00000000007c"), "Δ Msg Sent") { ShortDescription = "Change in Mach messages sent since previous snapshot" },
            new UIHints { IsVisible = true, Width = 100, AggregationMode = AggregationMode.Sum });

        private static readonly ColumnConfiguration deltaMsgRecvCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("b2001001-0001-4000-8000-00000000007d"), "Δ Msg Recv") { ShortDescription = "Change in Mach messages received since previous snapshot" },
            new UIHints { IsVisible = true, Width = 100, AggregationMode = AggregationMode.Sum });

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

            // Basic
            table.AddColumn(physFootprintCol, bp.Compose(e => e.PhysFootprint));
            table.AddColumn(cpuUsageMsCol, bp.Compose(e => (e.DeltaCpuSys + e.DeltaCpuUser) * 1000.0));
            table.AddColumn(deltaCpuSysCol, bp.Compose(e => e.DeltaCpuSys));
            table.AddColumn(deltaCpuUserCol, bp.Compose(e => e.DeltaCpuUser));

            // Memory
            table.AddColumn(residentCol, bp.Compose(e => e.Resident));
            table.AddColumn(maxResidentCol, bp.Compose(e => e.MaxResident));
            table.AddColumn(internalCol, bp.Compose(e => e.Internal));
            table.AddColumn(externalCol, bp.Compose(e => e.External));
            table.AddColumn(compressedCol, bp.Compose(e => e.Compressed));
            table.AddColumn(virtualCol, bp.Compose(e => e.Virtual));
            table.AddColumn(reusableCol, bp.Compose(e => e.Reusable));
            table.AddColumn(purgeableNVCol, bp.Compose(e => e.PurgeableNV));
            table.AddColumn(gfxFootprintCol, bp.Compose(e => e.GfxFootprint));
            table.AddColumn(compLifetimeCol, bp.Compose(e => e.CompressedLifetime));
            table.AddColumn(decomprCol, bp.Compose(e => e.Decompressions));
            table.AddColumn(regionCountCol, bp.Compose(e => e.RegionCount));
            table.AddColumn(deltaPhysFootprintCol, bp.Compose(e => e.DeltaPhysFootprint));
            table.AddColumn(deltaResidentCol, bp.Compose(e => e.DeltaResident));
            table.AddColumn(deltaInternalCol, bp.Compose(e => e.DeltaInternal));
            table.AddColumn(deltaExternalCol, bp.Compose(e => e.DeltaExternal));
            table.AddColumn(deltaCompressedCol, bp.Compose(e => e.DeltaCompressed));

            // CPU
            table.AddColumn(threadsCol, bp.Compose(e => e.Threads));
            table.AddColumn(threadsRunningCol, bp.Compose(e => e.ThreadsRunning));
            table.AddColumn(cswCol, bp.Compose(e => e.ContextSwitches));
            table.AddColumn(syscallsCol, bp.Compose(e => e.Syscalls));
            table.AddColumn(instructionsCol, bp.Compose(e => e.Instructions));
            table.AddColumn(cyclesCol, bp.Compose(e => e.Cycles));
            table.AddColumn(pInstructionsCol, bp.Compose(e => e.PInstructions));
            table.AddColumn(pCyclesCol, bp.Compose(e => e.PCycles));
            table.AddColumn(ipcCol, bp.Compose(e => e.IPC));
            table.AddColumn(runnableTimeCol, bp.Compose(e => e.RunnableTime));
            table.AddColumn(deltaInstrCol, bp.Compose(e => e.DeltaInstructions));
            table.AddColumn(deltaCyclesCol, bp.Compose(e => e.DeltaCycles));
            table.AddColumn(deltaPInstrCol, bp.Compose(e => e.DeltaPInstructions));
            table.AddColumn(deltaPCyclesCol, bp.Compose(e => e.DeltaPCycles));
            table.AddColumn(deltaCSWCol, bp.Compose(e => e.DeltaContextSwitches));
            table.AddColumn(deltaSyscallsCol, bp.Compose(e => e.DeltaSyscalls));
            table.AddColumn(deltaIPCCol, bp.Compose(e => e.DeltaIPC));
            table.AddColumn(qosUICol, bp.Compose(e => e.QosUserInteractive));
            table.AddColumn(qosUInitCol, bp.Compose(e => e.QosUserInitiated));
            table.AddColumn(qosDefaultCol, bp.Compose(e => e.QosDefault));
            table.AddColumn(qosUtilityCol, bp.Compose(e => e.QosUtility));
            table.AddColumn(qosBGCol, bp.Compose(e => e.QosBackground));
            table.AddColumn(qosMaintCol, bp.Compose(e => e.QosMaintenance));
            table.AddColumn(deltaQosUICol, bp.Compose(e => e.DeltaQosUserInteractive));
            table.AddColumn(deltaQosUInitCol, bp.Compose(e => e.DeltaQosUserInitiated));
            table.AddColumn(deltaQosDefaultCol, bp.Compose(e => e.DeltaQosDefault));
            table.AddColumn(deltaQosUtilityCol, bp.Compose(e => e.DeltaQosUtility));
            table.AddColumn(deltaQosBGCol, bp.Compose(e => e.DeltaQosBackground));
            table.AddColumn(deltaQosMaintCol, bp.Compose(e => e.DeltaQosMaintenance));
            table.AddColumn(deltaRunnableTimeCol, bp.Compose(e => e.DeltaRunnableTime));

            // I/O
            table.AddColumn(logicalWritesCol, bp.Compose(e => e.LogicalWrites));
            table.AddColumn(deltaLogicalWritesCol, bp.Compose(e => e.DeltaLogicalWrites));

            // Power
            table.AddColumn(energyCol, bp.Compose(e => e.EnergyNj));
            table.AddColumn(pEnergyCol, bp.Compose(e => e.PEnergyNj));
            table.AddColumn(billedEnergyCol, bp.Compose(e => e.BilledEnergy));
            table.AddColumn(servicedEnergyCol, bp.Compose(e => e.ServicedEnergy));
            table.AddColumn(intWkupsCol, bp.Compose(e => e.InterruptWakeups));
            table.AddColumn(pkgIdleWkupsCol, bp.Compose(e => e.PkgIdleWakeups));
            table.AddColumn(msgSentCol, bp.Compose(e => e.MsgSent));
            table.AddColumn(msgRecvCol, bp.Compose(e => e.MsgRecv));
            table.AddColumn(deltaEnergyCol, bp.Compose(e => e.DeltaEnergyNj));
            table.AddColumn(deltaPEnergyCol, bp.Compose(e => e.DeltaPEnergyNj));
            table.AddColumn(deltaIntWkupsCol, bp.Compose(e => e.DeltaInterruptWakeups));
            table.AddColumn(deltaPkgIdleWkupsCol, bp.Compose(e => e.DeltaPkgIdleWakeups));
            table.AddColumn(deltaMsgSentCol, bp.Compose(e => e.DeltaMsgSent));
            table.AddColumn(deltaMsgRecvCol, bp.Compose(e => e.DeltaMsgRecv));

            // ---- Table configurations (like snap_report category tabs) ----

            // Basic (footprint + CPU overview)
            var basicConfig = new TableConfiguration("Basic")
            {
                Columns = new[]
                {
                    processCol,
                    pidCol,
                    TableConfiguration.PivotColumn,
                    physFootprintCol,
                    deltaPhysFootprintCol,
                    cpuUsageMsCol,
                    deltaCpuSysCol,
                    deltaCpuUserCol,
                    threadsCol,
                    countCol,
                    TableConfiguration.GraphColumn,
                    timeCol,
                },
            };
            basicConfig.AddColumnRole(ColumnRole.StartTime, timeCol);
            basicConfig.AddColumnRole(ColumnRole.EndTime, timeCol);
            tableBuilder.AddTableConfiguration(basicConfig);

            // Memory
            var memConfig = new TableConfiguration("Memory")
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
                    maxResidentCol,
                    internalCol,
                    deltaInternalCol,
                    externalCol,
                    deltaExternalCol,
                    compressedCol,
                    deltaCompressedCol,
                    virtualCol,
                    reusableCol,
                    purgeableNVCol,
                    gfxFootprintCol,
                    compLifetimeCol,
                    decomprCol,
                    regionCountCol,
                    countCol,
                    TableConfiguration.GraphColumn,
                    timeCol,
                },
            };
            memConfig.AddColumnRole(ColumnRole.StartTime, timeCol);
            memConfig.AddColumnRole(ColumnRole.EndTime, timeCol);
            tableBuilder.AddTableConfiguration(memConfig);

            // CPU
            var cpuConfig = new TableConfiguration("CPU")
            {
                Columns = new[]
                {
                    processCol,
                    pidCol,
                    TableConfiguration.PivotColumn,
                    cpuUsageMsCol,
                    deltaCpuSysCol,
                    deltaCpuUserCol,
                    deltaInstrCol,
                    deltaCyclesCol,
                    deltaIPCCol,
                    deltaPInstrCol,
                    deltaPCyclesCol,
                    deltaCSWCol,
                    deltaSyscallsCol,
                    threadsCol,
                    threadsRunningCol,
                    runnableTimeCol,
                    deltaRunnableTimeCol,
                    qosUICol,
                    deltaQosUICol,
                    qosUInitCol,
                    deltaQosUInitCol,
                    qosDefaultCol,
                    deltaQosDefaultCol,
                    qosUtilityCol,
                    deltaQosUtilityCol,
                    qosBGCol,
                    deltaQosBGCol,
                    qosMaintCol,
                    deltaQosMaintCol,
                    countCol,
                    TableConfiguration.GraphColumn,
                    timeCol,
                },
            };
            cpuConfig.AddColumnRole(ColumnRole.StartTime, timeCol);
            cpuConfig.AddColumnRole(ColumnRole.EndTime, timeCol);
            tableBuilder.AddTableConfiguration(cpuConfig);

            // I/O
            var ioConfig = new TableConfiguration("I/O")
            {
                Columns = new[]
                {
                    processCol,
                    pidCol,
                    TableConfiguration.PivotColumn,
                    logicalWritesCol,
                    deltaLogicalWritesCol,
                    countCol,
                    TableConfiguration.GraphColumn,
                    timeCol,
                },
            };
            ioConfig.AddColumnRole(ColumnRole.StartTime, timeCol);
            ioConfig.AddColumnRole(ColumnRole.EndTime, timeCol);
            tableBuilder.AddTableConfiguration(ioConfig);

            // Power
            var powerConfig = new TableConfiguration("Power")
            {
                Columns = new[]
                {
                    processCol,
                    pidCol,
                    TableConfiguration.PivotColumn,
                    energyCol,
                    deltaEnergyCol,
                    pEnergyCol,
                    deltaPEnergyCol,
                    billedEnergyCol,
                    servicedEnergyCol,
                    intWkupsCol,
                    deltaIntWkupsCol,
                    pkgIdleWkupsCol,
                    deltaPkgIdleWkupsCol,
                    msgSentCol,
                    deltaMsgSentCol,
                    msgRecvCol,
                    deltaMsgRecvCol,
                    countCol,
                    TableConfiguration.GraphColumn,
                    timeCol,
                },
            };
            powerConfig.AddColumnRole(ColumnRole.StartTime, timeCol);
            powerConfig.AddColumnRole(ColumnRole.EndTime, timeCol);
            tableBuilder.AddTableConfiguration(powerConfig);

            tableBuilder.SetDefaultTableConfiguration(basicConfig);
        }
    }
}
