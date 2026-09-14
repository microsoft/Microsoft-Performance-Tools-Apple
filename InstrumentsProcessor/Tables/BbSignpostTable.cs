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
using System.Text.RegularExpressions;

namespace InstrumentsProcessor.Tables
{
    [Table]
    public sealed class BbSignpostTable
    {
        public static TableDescriptor TableDescriptor =>
           new TableDescriptor(
              Guid.Parse("{7f1a2b3c-4d5e-6f7a-8b9c-0d1e2f3a4b5c}"),
              "Browser Benchmark Signpost",
              "Browser Benchmark signpost events (TabSwitchPaint, WindowRestore, etc.)",
              "Browser",
              requiredDataCookers: new List<DataCookerPath>
              {
                  BbSignpostCooker.DataCookerPath
              });

        private static readonly ColumnConfiguration timeColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("8a1b2c3d-4e5f-6a7b-8c9d-0e1f2a3b4c5d"), "Time"),
            new UIHints
            {
                IsVisible = true,
                Width = 120,
                CellFormat = TimestampFormatter.FormatMicrosecondsGrouped,
            });

        private static readonly ColumnConfiguration metricColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("9b2c3d4e-5f6a-7b8c-9d0e-1f2a3b4c5d6e"), "Metric"),
            new UIHints
            {
                IsVisible = true,
                Width = 500,
            });

        private static readonly ColumnConfiguration eventNameColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("1a2b3c4d-5e6f-7a8b-9c0d-1e2f3a4b5c6d"), "EventName"),
            new UIHints { IsVisible = true, Width = 150 });

        private static readonly ColumnConfiguration pageColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("2b3c4d5e-6f7a-8b9c-0d1e-2f3a4b5c6d7e"), "Page"),
            new UIHints { IsVisible = true, Width = 100 });

        private static readonly ColumnConfiguration outerColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("3c4d5e6f-7a8b-9c0d-1e2f-3a4b5c6d7e8f"), "Outer"),
            new UIHints { IsVisible = true, Width = 60 });

        private static readonly ColumnConfiguration cycleColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("4d5e6f7a-8b9c-0d1e-2f3a-4b5c6d7e8f90"), "Cycle"),
            new UIHints { IsVisible = true, Width = 60 });

        private static readonly ColumnConfiguration instanceColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("5e6f7a8b-9c0d-1e2f-3a4b-5c6d7e8f9001"), "Instance"),
            new UIHints { IsVisible = true, Width = 60 });

        private static readonly ColumnConfiguration paintMsColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("6f7a8b9c-0d1e-2f3a-4b5c-6d7e8f900112"), "Paint (ms)"),
            new UIHints { IsVisible = true, Width = 80 });

        private static readonly ColumnConfiguration settleMsColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("7a8b9c0d-1e2f-3a4b-5c6d-7e8f90011223"), "Settle (ms)"),
            new UIHints { IsVisible = true, Width = 80 });

        private static readonly ColumnConfiguration threadColumn = new ColumnConfiguration(
            new ColumnMetadata(new Guid("0c3d4e5f-6a7b-8c9d-0e1f-2a3b4c5d6e7f"), "Thread"),
            new UIHints { IsVisible = true, Width = 100 });

        // Regex: "EventName [key=val, key=val, ...]" or just "EventName"
        private static readonly Regex MetricPattern = new Regex(
            @"^(\S+)\s*(?:\[(.+)\])?$", RegexOptions.Compiled);

        private struct ParsedMetric
        {
            public string EventName;
            public string Page;
            public string Outer;
            public string Cycle;
            public string Instance;
            public string PaintMs;
            public string SettleMs;
        }

        private static ParsedMetric Parse(string metric)
        {
            var result = new ParsedMetric();
            if (string.IsNullOrEmpty(metric)) return result;

            var m = MetricPattern.Match(metric);
            if (!m.Success) { result.EventName = metric; return result; }

            result.EventName = m.Groups[1].Value;
            if (!m.Groups[2].Success) return result;

            // Parse key=value pairs
            foreach (var pair in m.Groups[2].Value.Split(','))
            {
                var kv = pair.Trim().Split(new[] { '=' }, 2);
                if (kv.Length != 2) continue;
                string key = kv[0].Trim();
                string val = kv[1].Trim();
                switch (key)
                {
                    case "page": result.Page = val; break;
                    case "outer": result.Outer = val; break;
                    case "cycle": result.Cycle = val; break;
                    case "instance": result.Instance = val; break;
                    case "paint_ms": result.PaintMs = val; break;
                    case "settle_ms": result.SettleMs = val; break;
                }
            }
            return result;
        }

        public static bool IsDataAvailable(IDataExtensionRetrieval tableData)
        {
            var events = tableData.QueryOutput<List<BbSignpostEvent>>(
                new DataOutputPath(BbSignpostCooker.DataCookerPath, nameof(BbSignpostCooker.BbSignpostEvents)));
            return events != null && events.Count > 0;
        }

        public static void BuildTable(ITableBuilder tableBuilder, IDataExtensionRetrieval tableData)
        {
            var events = tableData.QueryOutput<List<BbSignpostEvent>>(
                new DataOutputPath(BbSignpostCooker.DataCookerPath, nameof(BbSignpostCooker.BbSignpostEvents)));

            if (events == null || events.Count == 0) return;

            // Pre-parse all metrics
            var parsed = events.Select(e => Parse(e.Metric?.Value ?? "")).ToList();

            var baseProjection = Projection.Index(events);
            var parsedProjection = Projection.Index(parsed);

            var timeProjection = baseProjection.Compose(e => e.Time?.Value ?? default);
            var metricProjection = baseProjection.Compose(e => e.Metric?.Value ?? string.Empty);
            var eventNameProjection = parsedProjection.Compose(p => p.EventName ?? "");
            var pageProjection = parsedProjection.Compose(p => p.Page ?? "");
            var outerProjection = parsedProjection.Compose(p => p.Outer ?? "");
            var cycleProjection = parsedProjection.Compose(p => p.Cycle ?? "");
            var instanceProjection = parsedProjection.Compose(p => p.Instance ?? "");
            var paintMsProjection = parsedProjection.Compose(p => p.PaintMs ?? "");
            var settleMsProjection = parsedProjection.Compose(p => p.SettleMs ?? "");
            var threadProjection = baseProjection.Compose(e =>
            {
                var t = e.Thread;
                if (t == null) return "Unknown";
                string name = t.Name ?? "";
                ulong tid = (ulong)(t.ThreadId?.Value ?? 0);
                return string.IsNullOrEmpty(name) ? $"tid:{tid}" : $"{name} ({tid})";
            });

            tableBuilder.SetRowCount(events.Count)
                .AddColumn(timeColumn, timeProjection)
                .AddColumn(eventNameColumn, eventNameProjection)
                .AddColumn(pageColumn, pageProjection)
                .AddColumn(outerColumn, outerProjection)
                .AddColumn(cycleColumn, cycleProjection)
                .AddColumn(instanceColumn, instanceProjection)
                .AddColumn(paintMsColumn, paintMsProjection)
                .AddColumn(settleMsColumn, settleMsProjection)
                .AddColumn(metricColumn, metricProjection)
                .AddColumn(threadColumn, threadProjection);

            var tableConfig = new TableConfiguration("Browser Benchmark Events")
            {
                Columns = new[]
                {
                    eventNameColumn, pageColumn, outerColumn, cycleColumn, instanceColumn,
                    paintMsColumn, settleMsColumn, timeColumn, metricColumn, threadColumn
                },
            };
            tableConfig.AddColumnRole(ColumnRole.StartTime, timeColumn);

            tableBuilder.AddTableConfiguration(tableConfig)
                .SetDefaultTableConfiguration(tableConfig);
        }
    }
}
