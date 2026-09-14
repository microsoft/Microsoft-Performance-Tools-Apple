// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
//
// Symbol resolution diagnostic for a .trace bundle.
//
// Usage:
//   DiagTrace.exe <trace-path> [<INSTRUMENTS_SYMBOL_PATH>]
//
// Prints:
//   - Number of images loaded from .symbolsarchive
//   - Merge summary from external symbols (dSYMs or SymbolStore)
//   - Per-matched-image: runtime __TEXT vmaddr vs file __TEXT vmaddr (shows slide)
//   - Sample backtraces from cpu-profile stores, with per-address resolution status
//     (raw address, matching segment, function name)
//   - Whether the global ASLR slide had to be computed and its final value

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using InstrumentsProcessor.Parsing.TraceBundle;

class DiagTrace
{
    static int Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.Error.WriteLine("usage: DiagTrace <trace-path> [<symbol-path>]");
            return 2;
        }

        string tracePath = args[0];
        string symbolPath = args.Length >= 2 ? args[1] : null;

        if (!Directory.Exists(tracePath))
        {
            Console.Error.WriteLine($"trace path not found: {tracePath}");
            return 2;
        }

        string corespace = Path.Combine(tracePath, "corespace");
        var runDir = Directory.EnumerateDirectories(corespace, "run*")
                              .Where(d => Path.GetFileName(d) != "currentRun")
                              .OrderBy(d => d)
                              .FirstOrDefault();
        if (runDir == null)
        {
            Console.Error.WriteLine("no run directory found");
            return 2;
        }

        string corePath = Path.Combine(runDir, "core");
        string tablesPlist = Path.Combine(corePath, "table-manager", "tables.plist");
        string corespaceDir = Path.GetDirectoryName(Path.GetDirectoryName(corePath));
        string uniquingDir = Path.Combine(corePath, "uniquing");

        Console.WriteLine($"trace     : {tracePath}");
        Console.WriteLine($"run       : {Path.GetFileName(runDir)}");
        Console.WriteLine();

        // Load the SymbolCatalog exactly like the plugin does.
        var symbols = SymbolCatalog.Load(tracePath);
        Console.WriteLine($"catalog.SegmentCount = {symbols.SegmentCount}");
        Console.WriteLine($"catalog.FunctionCount (pre-merge) = {symbols.FunctionCount}");
        Console.WriteLine();

        if (!string.IsNullOrEmpty(symbolPath))
        {
            Console.WriteLine($"Merging external symbols from: {symbolPath}");
            var result = symbols.MergeDsyms(symbolPath);
            Console.WriteLine($"  images seen     = {result.DsymImagesSeen}");
            Console.WriteLine($"  matched by UUID = {result.MatchedImages}");
            Console.WriteLine($"  unmatched       = {result.UnmatchedImages}");
            Console.WriteLine($"  symbols added   = {result.FunctionsAdded}");
            Console.WriteLine();
            foreach (var d in result.Details)
            {
                if (d.Matched)
                {
                    long slide = unchecked((long)d.RuntimeTextBase - (long)d.DsymTextBase);
                    Console.WriteLine(
                        $"  MATCH  {d.ImageName,-40} [{d.Uuid}] " +
                        $"runtime=0x{d.RuntimeTextBase:X12} file=0x{d.DsymTextBase:X12} " +
                        $"slide=0x{slide:X} ({d.FunctionsAdded} symbols)");
                }
                else
                {
                    Console.WriteLine($"  NO MATCH  {d.ImageName,-40} [{d.Uuid}]");
                }
            }
            Console.WriteLine();
        }

        Console.WriteLine($"catalog.FunctionCount (post-merge) = {symbols.FunctionCount}");
        Console.WriteLine();

        var stores = TableManager.ParseTablesPlist(tablesPlist, corespaceDir);
        var uniquing = new Uniquing(uniquingDir);

        // Find any store whose schema has a backtrace field.
        var candidateStores = new System.Collections.Generic.List<(StoreInfo si, StoreSchema schema, string btField)>();
        foreach (var s in stores)
        {
            if (s.Side != 0 || s.SchemaName == null) continue;
            StoreSchema sch;
            try { sch = new StoreSchema(s.StorePath, s.SchemaName); }
            catch { continue; }
            var bt = sch.Fields.FirstOrDefault(f => f.Name.Contains("backtrace"));
            if (bt == null) continue;
            candidateStores.Add((s, sch, bt.Name));
        }

        Console.WriteLine("Candidate stores with a backtrace field:");
        foreach (var c in candidateStores)
            Console.WriteLine($"  {c.si.SchemaName}  (field: {c.btField})");
        Console.WriteLine();

        if (candidateStores.Count == 0)
        {
            Console.WriteLine("No stores with a backtrace field.");
            return 0;
        }

        // Prefer time-profile / cpu-sample stores; fall back to the first candidate.
        var chosen = candidateStores.FirstOrDefault(c =>
            c.si.SchemaName.Contains("time-profile") ||
            c.si.SchemaName.Contains("cpu-sample") ||
            c.si.SchemaName.Contains("time-sample") ||
            c.si.SchemaName.Contains("cpu-profile"));
        if (chosen.si == null) chosen = candidateStores[0];

        var (storeInfo, schema, backtraceFieldName) = chosen;
        Console.WriteLine($"Sampling backtraces from store: {storeInfo.SchemaName} (field: {backtraceFieldName})");

        int sampleCount = 0;
        const int MaxSamples = 5;
        const int MaxFramesPerSample = 12;

        int totalFramesSeen = 0;
        int totalFramesResolvedFn = 0;
        int totalFramesInSegment = 0;

        foreach (var row in BulkstoreReader.ReadRows(storeInfo.StorePath, schema))
        {
            if (!row.TryGetValue(backtraceFieldName, out var bt) || bt == null) continue;
            int refIdx;
            try { refIdx = (int)Convert.ToInt64(bt); }
            catch { continue; }
            if (refIdx <= 0) continue;

            ulong[] addresses;
            try { addresses = uniquing.ResolveBacktrace(refIdx); }
            catch { continue; }
            if (addresses == null || addresses.Length == 0) continue;

            if (sampleCount < MaxSamples)
            {
                Console.WriteLine($"\nBacktrace #{sampleCount + 1} ({addresses.Length} frames):");
                int shown = Math.Min(MaxFramesPerSample, addresses.Length);
                for (int i = 0; i < shown; i++)
                {
                    ulong a = addresses[i];
                    var seg = symbols.TryFindSegmentDiag(a);
                    string fn = symbols.TryFindFunctionDiag(a);
                    string segStr = seg.Found
                        ? $"{seg.ImageName}({seg.SegmentName})"
                        : "<no segment>";
                    string fnStr = fn ?? "<no function>";
                    Console.WriteLine($"  [{i,2}] raw=0x{a:X12}  seg={segStr,-60}  fn={fnStr}");
                }
                if (addresses.Length > shown)
                    Console.WriteLine($"  ... {addresses.Length - shown} more frames");
            }

            foreach (ulong a in addresses)
            {
                totalFramesSeen++;
                if (symbols.TryFindFunctionDiag(a) != null) totalFramesResolvedFn++;
                if (symbols.TryFindSegmentDiag(a).Found) totalFramesInSegment++;
            }

            sampleCount++;
            if (sampleCount >= 500) break;
        }

        Console.WriteLine();
        Console.WriteLine("=== Aggregate over sampled backtraces ===");
        Console.WriteLine($"Samples inspected     : {sampleCount}");
        Console.WriteLine($"Total frames          : {totalFramesSeen}");
        Console.WriteLine($"Frames in a segment   : {totalFramesInSegment}");
        Console.WriteLine($"Frames with function  : {totalFramesResolvedFn}");
        Console.WriteLine();
        Console.WriteLine($"catalog.CurrentSlide  = 0x{symbols.CurrentSlide:X} (isSlideComputed={symbols.IsSlideComputed})");

        return 0;
    }
}





