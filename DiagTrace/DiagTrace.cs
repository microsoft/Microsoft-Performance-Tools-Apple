// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using InstrumentsProcessor.Parsing.TraceBundle;

class DiagTrace
{
    static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("usage: DiagTrace <trace-path> [symbol-path] [--run N] [--pid N] [--stack-ref N] [--limit N] [--kernel]");
            return 2;
        }
        try { return Run(args); }
        catch (Exception error)
        {
            Console.Error.WriteLine(error.Message);
            return 1;
        }
    }

    static int Run(string[] args)
    {
        string tracePath = Path.GetFullPath(args[0]);
        int run = 1, limit = 500;
        long? processFilter = null;
        int? referenceFilter = null;
        bool kernelOnly = false;
        string symbolPath = null;
        for (int index = 1; index < args.Length; index++)
        {
            string option = args[index];
            if (option == "--kernel") { kernelOnly = true; continue; }
            if (!option.StartsWith("--", StringComparison.Ordinal) && symbolPath == null) { symbolPath = option; continue; }
            if (index + 1 >= args.Length) throw new ArgumentException($"Missing value for {option}.");
            string value = args[++index];
            switch (option)
            {
                case "--run": run = int.Parse(value, CultureInfo.InvariantCulture); break;
                case "--limit": limit = int.Parse(value, CultureInfo.InvariantCulture); break;
                case "--pid": processFilter = long.Parse(value, CultureInfo.InvariantCulture); break;
                case "--stack-ref": referenceFilter = int.Parse(value, CultureInfo.InvariantCulture); break;
                default: throw new ArgumentException($"Unknown option {option}.");
            }
        }
        if (run < 1 || limit < 1) throw new ArgumentException("Run and limit must be positive.");
        string corespace = Path.Combine(tracePath, "corespace");
        string core = Path.Combine(corespace, $"run{run}", "core");
        var catalog = SymbolCatalog.Load(tracePath);
        foreach (string message in catalog.Diagnostics) Console.WriteLine($"WARNING: {message}");
        Console.WriteLine($"Trace: {Path.GetFileName(tracePath)}, run {run}");
        Console.WriteLine($"Images in runtime maps: {catalog.TextImageCount}; archive segments: {catalog.SegmentCount}; symbol records: {catalog.FunctionCount}");
        if (symbolPath != null)
        {
            var merge = catalog.MergeDsyms(symbolPath);
            Console.WriteLine($"External images: {merge.MatchedImages}/{merge.DsymImagesSeen} matched; {merge.FunctionsAdded} symbols added");
        }
        var uniquing = new Uniquing(Path.Combine(core, "uniquing"));
        var stores = TableManager.ParseTablesPlist(Path.Combine(core, "table-manager", "tables.plist"), corespace);
        NameResolver.BuildPidNameMap(stores, uniquing, out var processRefs);
        var threadRefs = NameResolver.BuildThreadRefMap(stores, uniquing);
        int samples = 0, frames = 0;
        var counts = new Dictionary<SymbolStatus, int>();
        foreach (string directory in Directory.EnumerateDirectories(Path.Combine(core, "stores")).OrderBy(path => path, StringComparer.Ordinal))
        {
            string schemaName = stores.FirstOrDefault(store => Path.GetFullPath(store.StorePath) == Path.GetFullPath(directory) && !string.IsNullOrEmpty(store.SchemaName))?.SchemaName ?? "raw";
            var schema = new StoreSchema(directory, schemaName);
            var columns = schema.Columns.Where(column =>
                column.EngineeringType.IndexOf("Backtrace", StringComparison.OrdinalIgnoreCase) >= 0 ||
                column.EngineeringType == "XRCoreProfileCallstackTypeID").ToArray();
            foreach (var row in BulkstoreReader.ReadRows(directory, schema))
            {
                foreach (var column in columns)
                {
                    bool kernel = column.Mnemonic == "cp-kernel-callstack";
                    if (kernelOnly != kernel) continue;
                    if (!row.TryGetValue(column.Mnemonic, out var raw)) continue;
                    uint reference = Convert.ToUInt32(raw);
                    if (reference > int.MaxValue || (referenceFilter.HasValue && reference != referenceFilter.Value)) continue;
                    var decoded = uniquing.DecodeBacktrace((int)reference, column.EngineeringType);
                    if (decoded.Addresses.Length == 0) continue;
                    var context = TraceBundleEventFactory.GetSymbolContext(row, schema, uniquing, run, processRefs, threadRefs) with { Kernel = kernel };
                    context = TraceBundleEventFactory.ContextForBacktrace(decoded, context);
                    if (processFilter.HasValue && context.ProcessId != processFilter.Value) continue;
                    if (samples < 5) Console.WriteLine($"Stack ref={reference}, process={context.ProcessId}, time={context.Timestamp}, field={column.Mnemonic}, frames={decoded.Addresses.Length}");
                    for (int frameIndex = 0; frameIndex < decoded.Addresses.Length; frameIndex++)
                    {
                        ulong address = decoded.Addresses[frameIndex];
                        var resolution = catalog.ResolveFrame(address, context, frameIndex);
                        counts.TryGetValue(resolution.Status, out int count);
                        counts[resolution.Status] = count + 1;
                        frames++;
                        if (samples < 5)
                        {
                            string name = resolution.Symbol?.Name ?? "<unresolved>";
                            ulong offset = resolution.Symbol != null ? resolution.Coordinate - resolution.Symbol.Address : 0;
                            Console.WriteLine($"  0x{address:X} {resolution.Status} {resolution.Image?.Name} [{resolution.Image?.Uuid}] coordinate=0x{resolution.Coordinate:X} {name}+0x{offset:X}");
                        }
                    }
                    samples++;
                    if (samples >= limit) goto Complete;
                }
            }
        }
    Complete:
        Console.WriteLine($"Samples: {samples}; frames: {frames}");
        foreach (var count in counts.OrderBy(pair => pair.Key)) Console.WriteLine($"{count.Key}: {count.Value}");
        return samples == 0 && referenceFilter.HasValue ? 1 : 0;
    }
}