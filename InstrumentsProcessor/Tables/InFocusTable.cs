// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using InstrumentsProcessor.Cookers;
using InstrumentsProcessor.Parsing.Events;
using Microsoft.Performance.SDK;
using Microsoft.Performance.SDK.Extensibility;
using Microsoft.Performance.SDK.Processing;
using System;
using System.Collections.Generic;
using System.Linq;

namespace InstrumentsProcessor.Tables
{
    [Table]
    public sealed class InFocusTable
    {
        public static TableDescriptor TableDescriptor =>
           new TableDescriptor(
              Guid.Parse("{d1a2b3c4-e5f6-4789-abcd-0123456789ef}"),
              "In Focus",
              "Shows intervals when a process is in the foreground or background",
              "App Lifecycle",
              requiredDataCookers: new List<DataCookerPath>
              {
                  TrRoleChangeCooker.DataCookerPath,
                  ProcessInfoCooker.DataCookerPath,
              });

        private static readonly ColumnConfiguration startTimeColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d1a20001-0001-4001-a001-000000000001"), "Start Time"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                CellFormat = TimestampFormatter.FormatMicrosecondsGrouped,
                AggregationMode = AggregationMode.Min,
            });

        private static readonly ColumnConfiguration stopTimeColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d1a20001-0001-4001-a001-000000000002"), "Stop Time"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                CellFormat = TimestampFormatter.FormatMicrosecondsGrouped,
                AggregationMode = AggregationMode.Max,
            });

        private static readonly ColumnConfiguration durationColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d1a20001-0001-4001-a001-000000000003"), "Duration"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
                AggregationMode = AggregationMode.Sum,
                SortOrder = SortOrder.Descending,
                CellFormat = TimestampFormatter.FormatMillisecondsGrouped,
            });

        private static readonly ColumnConfiguration targetPidColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d1a20001-0001-4001-a001-000000000004"), "Target PID"),
            new UIHints
            {
                IsVisible = true,
                Width = 80,
            });

        private static readonly ColumnConfiguration targetProcessNameColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d1a20001-0001-4001-a001-000000000005"), "Target Process"),
            new UIHints
            {
                IsVisible = true,
                Width = 150,
            });

        private static readonly ColumnConfiguration roleColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d1a20001-0001-4001-a001-000000000006"), "Role"),
            new UIHints
            {
                IsVisible = true,
                Width = 100,
            });

        private static readonly ColumnConfiguration countColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("d1a20001-0001-4001-a001-000000000007"), "Count"),
            new UIHints
            {
                IsVisible = true,
                Width = 80,
                AggregationMode = AggregationMode.Sum,
            });

        public static bool IsDataAvailable(IDataExtensionRetrieval requiredData)
        {
            var data = requiredData.QueryOutput<List<TrRoleChangeEvent>>(
                new DataOutputPath(TrRoleChangeCooker.DataCookerPath, nameof(TrRoleChangeCooker.TrRoleChangeEvents)));
            return data != null && data.Count > 0;
        }

        public static void BuildTable(
            ITableBuilder tableBuilder,
            IDataExtensionRetrieval requiredData)
        {
            List<TrRoleChangeEvent> roleChangeEvents =
                requiredData.QueryOutput<List<TrRoleChangeEvent>>(
                    new DataOutputPath(TrRoleChangeCooker.DataCookerPath, nameof(TrRoleChangeCooker.TrRoleChangeEvents)));

            List<ProcessInfoEvent> processInfoEvents =
                requiredData.QueryOutput<List<ProcessInfoEvent>>(
                    new DataOutputPath(ProcessInfoCooker.DataCookerPath, nameof(ProcessInfoCooker.ProcessInfoEvents)));

            // Build PID -> Process Name lookup from process-info table
            var pidToName = new Dictionary<long, string>();
            if (processInfoEvents != null)
            {
                foreach (var pi in processInfoEvents)
                {
                    long pid = pi.ProcessId?.Value ?? -1;
                    string name = pi.ProcessName?.Value;
                    if (pid >= 0 && !string.IsNullOrEmpty(name))
                    {
                        pidToName[pid] = name;
                    }
                }
            }

            // Build joined InFocusInterval rows by pairing foreground/background transitions.
            // A foreground event (role=1) marks the start of an in-focus interval for that PID.
            // A background event (role=2) closes that interval. If the first event for a PID is
            // background, it means the process was already in foreground from the trace start.
            var sortedEvents = roleChangeEvents
                .Where(e => { ulong r = e.NewRole?.Value ?? 0; return r == 1 || r == 2; })
                .OrderBy(e => e.StartTime?.Value ?? default)
                .ToList();

            // Track the foreground-start timestamp per PID
            var foregroundStart = new Dictionary<long, Timestamp>();

            var intervals = new List<InFocusInterval>();
            foreach (var e in sortedEvents)
            {
                ulong role = e.NewRole?.Value ?? 0;
                long targetPid = e.TargetPid?.Value ?? -1;
                pidToName.TryGetValue(targetPid, out string processName);
                string name = processName ?? $"PID {targetPid}";

                if (role == 1)
                {
                    // Foreground: record start time for this PID
                    foregroundStart[targetPid] = e.StartTime?.Value ?? default;
                }
                else if (role == 2)
                {
                    // Background: close the foreground interval for this PID
                    Timestamp start;
                    if (foregroundStart.TryGetValue(targetPid, out start))
                    {
                        foregroundStart.Remove(targetPid);
                    }
                    else
                    {
                        // First background event with no prior foreground means it was
                        // already in foreground from the beginning of the trace
                        start = Timestamp.Zero;
                    }

                    var stopTime = e.StartTime?.Value ?? default;
                    var duration = stopTime - start;

                    intervals.Add(new InFocusInterval
                    {
                        StartTime = start,
                        Duration = duration,
                        TargetPid = targetPid,
                        TargetProcessName = name,
                        NewRole = 1,
                        RoleName = "Foreground",
                    });
                }
            }

            ITableBuilderWithRowCount tableBuilderWithRowCount = tableBuilder.SetRowCount(intervals.Count);

            var baseProjection = Projection.Index(intervals);

            var startTimeProjection = baseProjection.Compose(e => e.StartTime);
            var stopTimeProjection = baseProjection.Compose(e => e.StartTime + e.Duration);
            var durationProjection = baseProjection.Compose(e => e.Duration);
            var targetPidProjection = baseProjection.Compose(e => e.TargetPid);
            var targetProcessNameProjection = baseProjection.Compose(e => e.TargetProcessName);
            var roleProjection = baseProjection.Compose(e => e.RoleName);

            tableBuilderWithRowCount.AddColumn(startTimeColumn, startTimeProjection);
            tableBuilderWithRowCount.AddColumn(stopTimeColumn, stopTimeProjection);
            tableBuilderWithRowCount.AddColumn(durationColumn, durationProjection);
            tableBuilderWithRowCount.AddColumn(targetPidColumn, targetPidProjection);
            tableBuilderWithRowCount.AddColumn(targetProcessNameColumn, targetProcessNameProjection);
            tableBuilderWithRowCount.AddColumn(roleColumn, roleProjection);
            tableBuilderWithRowCount.AddColumn(countColumn, Projection.Constant(1));

            var tableConfig = new TableConfiguration("In Focus by Process")
            {
                Columns = new[]
                {
                    targetProcessNameColumn,
                    targetPidColumn,
                    TableConfiguration.PivotColumn,
                    countColumn,
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
