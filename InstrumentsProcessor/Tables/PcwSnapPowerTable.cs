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
    public sealed class PcwSnapPowerTable
    {
        public static TableDescriptor TableDescriptor =>
           new TableDescriptor(
              Guid.Parse("{4d5e6f7a-8b9c-4dae-bf01-2a3b4c5d6e7f}"),
              "PcwSnap Power",
              "Per-process power metrics (energy, wakeups, messages) with delta breakdown",
              "PcwSnap",
              requiredDataCookers: new List<DataCookerPath>
              {
                  PcwSnapProcessCooker.DataCookerPath
              });

        // ---- Identity ----
        private static readonly ColumnConfiguration snapshotCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d4001001-0001-4000-8000-000000000001"), "Snapshot #") { ShortDescription = "Sequential index of the pcwsnap snapshot" },
            new UIHints { IsVisible = true, Width = 80 });

        private static readonly ColumnConfiguration timeCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d4001001-0001-4000-8000-000000000002"), "Timestamp") { ShortDescription = "Wall-clock time of the snapshot" },
            new UIHints
            {
                IsVisible = true, Width = 140,
                CellFormat = TimestampFormatter.FormatMicrosecondsGrouped,
                AggregationMode = AggregationMode.Min
            });

        private static readonly ColumnConfiguration pidCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d4001001-0001-4000-8000-000000000003"), "PID") { ShortDescription = "Process ID from the kcdata task snapshot" },
            new UIHints { IsVisible = true, Width = 70 });

        private static readonly ColumnConfiguration processCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d4001001-0001-4000-8000-000000000004"), "Process") { ShortDescription = "Process name from the kcdata task snapshot" },
            new UIHints { IsVisible = true, Width = 160 });

        private static readonly ColumnConfiguration countCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d4001001-0001-4000-8000-000000000005"), "Count") { ShortDescription = "Row count (always 1), useful for aggregation" },
            new UIHints { IsVisible = true, Width = 60, AggregationMode = AggregationMode.Sum });

        // ---- Energy ----
        private static readonly ColumnConfiguration energyCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d4001001-0001-4000-8000-000000000010"), "Energy (nJ)") { ShortDescription = "Cumulative energy consumed in nanojoules (recount_task_energy_nj)" },
            new UIHints { IsVisible = true, Width = 100, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration deltaEnergyCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d4001001-0001-4000-8000-000000000011"), "Δ Energy (nJ)") { ShortDescription = "Change in energy consumed since previous snapshot" },
            new UIHints { IsVisible = true, Width = 110, AggregationMode = AggregationMode.Sum });

        private static readonly ColumnConfiguration pEnergyCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d4001001-0001-4000-8000-000000000012"), "P-Energy (nJ)") { ShortDescription = "Cumulative energy consumed on Performance (P) cores in nanojoules" },
            new UIHints { IsVisible = true, Width = 110, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration deltaPEnergyCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d4001001-0001-4000-8000-000000000013"), "Δ P-Energy (nJ)") { ShortDescription = "Change in P-core energy consumed since previous snapshot" },
            new UIHints { IsVisible = true, Width = 120, AggregationMode = AggregationMode.Sum });

        private static readonly ColumnConfiguration billedEnergyCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d4001001-0001-4000-8000-000000000014"), "Billed Energy") { ShortDescription = "Energy billed to this task, including work done on its behalf by other tasks" },
            new UIHints { IsVisible = true, Width = 110, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration servicedEnergyCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d4001001-0001-4000-8000-000000000015"), "Serviced Energy") { ShortDescription = "Energy from work this task performed on behalf of other tasks" },
            new UIHints { IsVisible = true, Width = 120, AggregationMode = AggregationMode.Max });

        // ---- Wakeups ----
        private static readonly ColumnConfiguration intWkupsCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d4001001-0001-4000-8000-000000000020"), "Interrupt Wakeups") { ShortDescription = "Cumulative times the task was woken by hardware interrupts (task_interrupt_wakeups)" },
            new UIHints { IsVisible = true, Width = 120, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration deltaIntWkupsCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d4001001-0001-4000-8000-000000000021"), "Δ Int Wakeups") { ShortDescription = "Change in interrupt wakeups since previous snapshot" },
            new UIHints { IsVisible = true, Width = 110, AggregationMode = AggregationMode.Sum });

        private static readonly ColumnConfiguration pkgIdleWkupsCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d4001001-0001-4000-8000-000000000022"), "Pkg Idle Wakeups") { ShortDescription = "Cumulative times the task caused the CPU package to exit idle state (expensive)" },
            new UIHints { IsVisible = true, Width = 120, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration deltaPkgIdleWkupsCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d4001001-0001-4000-8000-000000000023"), "Δ Pkg Idle Wkups") { ShortDescription = "Change in package idle wakeups since previous snapshot" },
            new UIHints { IsVisible = true, Width = 130, AggregationMode = AggregationMode.Sum });

        // ---- Messages ----
        private static readonly ColumnConfiguration msgSentCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d4001001-0001-4000-8000-000000000030"), "Msg Sent") { ShortDescription = "Cumulative Mach IPC messages sent by the task" },
            new UIHints { IsVisible = true, Width = 90, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration deltaMsgSentCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d4001001-0001-4000-8000-000000000031"), "Δ Msg Sent") { ShortDescription = "Change in Mach messages sent since previous snapshot" },
            new UIHints { IsVisible = true, Width = 100, AggregationMode = AggregationMode.Sum });

        private static readonly ColumnConfiguration msgRecvCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d4001001-0001-4000-8000-000000000032"), "Msg Recv") { ShortDescription = "Cumulative Mach IPC messages received by the task" },
            new UIHints { IsVisible = true, Width = 90, AggregationMode = AggregationMode.Max });

        private static readonly ColumnConfiguration deltaMsgRecvCol = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d4001001-0001-4000-8000-000000000033"), "Δ Msg Recv") { ShortDescription = "Change in Mach messages received since previous snapshot" },
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

            // Energy
            table.AddColumn(energyCol, bp.Compose(e => e.EnergyNj));
            table.AddColumn(deltaEnergyCol, bp.Compose(e => e.DeltaEnergyNj));
            table.AddColumn(pEnergyCol, bp.Compose(e => e.PEnergyNj));
            table.AddColumn(deltaPEnergyCol, bp.Compose(e => e.DeltaPEnergyNj));
            table.AddColumn(billedEnergyCol, bp.Compose(e => e.BilledEnergy));
            table.AddColumn(servicedEnergyCol, bp.Compose(e => e.ServicedEnergy));

            // Wakeups
            table.AddColumn(intWkupsCol, bp.Compose(e => e.InterruptWakeups));
            table.AddColumn(deltaIntWkupsCol, bp.Compose(e => e.DeltaInterruptWakeups));
            table.AddColumn(pkgIdleWkupsCol, bp.Compose(e => e.PkgIdleWakeups));
            table.AddColumn(deltaPkgIdleWkupsCol, bp.Compose(e => e.DeltaPkgIdleWakeups));

            // Messages
            table.AddColumn(msgSentCol, bp.Compose(e => e.MsgSent));
            table.AddColumn(deltaMsgSentCol, bp.Compose(e => e.DeltaMsgSent));
            table.AddColumn(msgRecvCol, bp.Compose(e => e.MsgRecv));
            table.AddColumn(deltaMsgRecvCol, bp.Compose(e => e.DeltaMsgRecv));

            // ---- Configurations ----

            // By Process
            var byProcess = new TableConfiguration("By Process")
            {
                Columns = new[]
                {
                    processCol,
                    pidCol,
                    TableConfiguration.PivotColumn,
                    deltaEnergyCol,
                    deltaPEnergyCol,
                    deltaIntWkupsCol,
                    deltaPkgIdleWkupsCol,
                    deltaMsgSentCol,
                    deltaMsgRecvCol,
                    energyCol,
                    pEnergyCol,
                    billedEnergyCol,
                    servicedEnergyCol,
                    countCol,
                    TableConfiguration.GraphColumn,
                    timeCol,
                },
            };
            byProcess.AddColumnRole(ColumnRole.StartTime, timeCol);
            byProcess.AddColumnRole(ColumnRole.EndTime, timeCol);
            tableBuilder.AddTableConfiguration(byProcess);

            // Full Detail
            var full = new TableConfiguration("Full Detail")
            {
                Columns = new[]
                {
                    processCol,
                    pidCol,
                    snapshotCol,
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
            full.AddColumnRole(ColumnRole.StartTime, timeCol);
            full.AddColumnRole(ColumnRole.EndTime, timeCol);
            tableBuilder.AddTableConfiguration(full);

            tableBuilder.SetDefaultTableConfiguration(byProcess);
        }
    }
}
