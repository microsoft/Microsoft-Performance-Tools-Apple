// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace InstrumentsProcessor.Parsing.TraceBundle
{
    internal enum SymbolStatus { Named, ModuleOnly, MissingMapping, MissingArchive, UnsupportedCoordinates, Ambiguous }

    internal sealed record SymbolResolution(SymbolStatus Status, ImageLoad Image = null,
        ulong Coordinate = 0, SymbolEntry Symbol = null, string Source = null);

    internal sealed class SymbolCatalog
    {
        private const uint CpuSubtypeMask = 0xFF000000;
        private readonly Dictionary<(Guid Uuid, uint Cpu, uint Subtype), SymbolArchive> archives = new();
        private readonly Dictionary<(Guid Uuid, uint Cpu, uint Subtype), List<SymbolArchive>> external = new();
        private readonly List<string> diagnostics = new();
        public RuntimeImageMap Mappings { get; private set; }
        public IReadOnlyList<string> Diagnostics => diagnostics;
        public int SegmentCount => archives.Values.Sum(image => image.Segments.Count);
        public int FunctionCount => archives.Values.Sum(image => image.Symbols.Count) + external.Values.SelectMany(images => images).Sum(image => image.Symbols.Count);
        public int TextImageCount => Mappings.Images.Select(image => image.Uuid).Distinct().Count();

        internal SymbolCatalog(RuntimeImageMap mappings, IEnumerable<SymbolArchive> images = null)
        {
            Mappings = mappings;
            if (images != null) foreach (var image in images) archives[(image.Uuid, image.CpuType, image.CpuSubtype)] = image;
        }

        private void Report(string message)
        {
            if (diagnostics.Count < 100) diagnostics.Add(message);
        }

        public static SymbolCatalog Load(string traceDir, CancellationToken cancellation = default)
        {
            var result = new SymbolCatalog(new RuntimeImageMap());
            result.Mappings = SymbolMetadataReader.Load(traceDir, result.Report, cancellation);
            string archiveDirectory = Path.Combine(traceDir, "symbols", "stores");
            if (!Directory.Exists(archiveDirectory)) return result;
            foreach (string path in Directory.EnumerateFiles(archiveDirectory, "*.symbolsarchive"))
            {
                cancellation.ThrowIfCancellationRequested();
                try
                {
                    if (!Guid.TryParse(Path.GetFileNameWithoutExtension(path), out var uuid)) throw new InvalidDataException("Invalid archive filename UUID.");
                    if (new FileInfo(path).Length > 128L * 1024 * 1024) throw new InvalidDataException("Symbol archive exceeds size limit.");
                    var image = SymbolArchive.Parse(File.ReadAllBytes(path), uuid);
                    result.archives[(image.Uuid, image.CpuType, image.CpuSubtype)] = image;
                }
                catch (Exception error) when (error is IOException || error is InvalidDataException || error is ArgumentException || error is OverflowException)
                {
                    result.Report($"Archive {Path.GetFileName(path)}: {error.Message}");
                }
            }
            return result;
        }

        public IEnumerable<(string Uuid, string ImageName)> EnumerateTextImages() =>
            Mappings.Images.GroupBy(image => image.Uuid).Select(group => (group.Key.ToString().ToUpperInvariant(), group.First().Name));

        public SymbolResolution ResolveAddress(ulong address, SymbolContext context)
        {
            var scope = Mappings.GetScope(context);
            if (scope == null) return new SymbolResolution(SymbolStatus.MissingMapping);
            var mapping = scope.Find(address, context.Timestamp);
            if (mapping.Ambiguous) return new SymbolResolution(SymbolStatus.Ambiguous);
            if (mapping.Image == null) return new SymbolResolution(SymbolStatus.MissingMapping);
            var image = mapping.Image;
            var key = (image.Uuid, image.CpuType, image.CpuSubtype);
            bool hasArchive = archives.TryGetValue(key, out var archive);
            ulong coordinate = address - mapping.Segment.Address;
            if (hasArchive)
            {
                var segments = archive.Segments.Where(segment => segment.Name == mapping.Segment.Name).ToArray();
                if (segments.Length != 1 || coordinate >= segments[0].Size || coordinate > ulong.MaxValue - segments[0].Address)
                    return new SymbolResolution(SymbolStatus.UnsupportedCoordinates, image, coordinate);
                coordinate += segments[0].Address;
                var symbol = archive.Find(coordinate, out bool ambiguous);
                if (ambiguous) return new SymbolResolution(SymbolStatus.Ambiguous, image, coordinate);
                if (symbol != null && !symbol.IsPlaceholder) return new SymbolResolution(SymbolStatus.Named, image, coordinate, symbol, "bundle");
            }
            if (external.TryGetValue(key, out var sources))
            {
                foreach (var source in sources)
                {
                    var symbol = FindExternalSymbol(source, mapping.Segment, address, out ulong sourceCoordinate, out bool ambiguous);
                    if (ambiguous) continue;
                    if (symbol != null && !symbol.IsPlaceholder)
                        return new SymbolResolution(SymbolStatus.Named, image, sourceCoordinate, symbol, "external");
                }
            }
            return new SymbolResolution(hasArchive ? SymbolStatus.ModuleOnly : SymbolStatus.MissingArchive, image, coordinate);
        }

        public SymbolResolution ResolveFrame(ulong address, SymbolContext context, int frameIndex)
        {
            if (frameIndex == 0 || address == 0) return ResolveAddress(address, context);
            var result = ResolveAddress(address - 1, context);
            return result.Image != null && result.Coordinate < ulong.MaxValue
                ? result with { Coordinate = result.Coordinate + 1 } : result;
        }

        public DataModels.Frame[] ResolveBacktrace(ulong[] addresses, SymbolContext context = null)
        {
            if (addresses == null) return Array.Empty<DataModels.Frame>();
            return addresses.Select((address, index) =>
            {
                var result = ResolveFrame(address, context, index);
                string raw = $"0x{address:x}";
                string name = result.Status == SymbolStatus.Named ? result.Symbol.Name :
                    result.Image != null ? $"{result.Image.Name}+0x{result.Coordinate:x}" : raw;
                return new DataModels.Frame(new DataModels.Function(name, raw),
                    result.Image != null ? new DataModels.Module(result.Image.Name) : null);
            }).ToArray();
        }

        internal sealed record DsymMergeDetail(string Uuid, string ImageName, bool Matched, int FunctionsAdded,
            ulong RuntimeTextBase = 0, ulong DsymTextBase = 0);
        internal sealed record MergeResult(int DsymImagesSeen, int MatchedImages, int UnmatchedImages,
            int FunctionsAdded, IReadOnlyList<DsymMergeDetail> Details);

        internal static bool CpuSubtypesMatch(uint recorded, uint source) =>
            (recorded & ~CpuSubtypeMask) == (source & ~CpuSubtypeMask);

        internal static SymbolEntry FindExternalSymbol(SymbolArchive source, SymbolSegment runtimeSegment,
            ulong address, out ulong sourceCoordinate, out bool ambiguous)
        {
            sourceCoordinate = 0;
            ambiguous = false;
            if (address < runtimeSegment.Address) return null;
            ulong offset = address - runtimeSegment.Address;
            var segments = source.Segments.Where(segment => segment.Name == runtimeSegment.Name &&
                offset < segment.Size && segment.Address <= ulong.MaxValue - offset).ToArray();
            if (segments.Length != 1) return null;
            sourceCoordinate = segments[0].Address + offset;
            return source.Find(sourceCoordinate, out ambiguous);
        }

        public MergeResult MergeDsyms(string path)
        {
            var details = new List<DsymMergeDetail>();
            int added = 0, matched = 0;
            var loads = Mappings.Images.GroupBy(image => image.Uuid).ToDictionary(group => group.Key, group => group.ToArray());
            var sources = DsymLoader.LoadFromPath(path).Concat(SymbolStoreLoader.LoadFromPath(path));
            foreach (var source in sources)
            {
                if (!Guid.TryParse(source.Uuid, out var uuid) || !loads.TryGetValue(uuid, out var candidates))
                {
                    details.Add(new DsymMergeDetail(source.Uuid, source.ImageName, false, 0));
                    continue;
                }
                var architectures = candidates.Where(image => (!source.CpuType.HasValue || image.CpuType == source.CpuType) &&
                    (!source.CpuSubtype.HasValue || CpuSubtypesMatch(image.CpuSubtype, source.CpuSubtype.Value)))
                    .GroupBy(image => (image.CpuType, image.CpuSubtype)).ToArray();
                if (architectures.Length != 1)
                {
                    details.Add(new DsymMergeDetail(source.Uuid, source.ImageName, false, 0));
                    continue;
                }
                var architecture = architectures[0].Key;
                var symbols = source.Functions.Where(symbol => symbol.Address >= source.TextVmAddr &&
                    symbol.Size <= ulong.MaxValue - (symbol.Address - source.TextVmAddr))
                    .Select(symbol => new SymbolEntry(ItaniumDemangler.TryDemangle(symbol.Name), symbol.Address - source.TextVmAddr, symbol.Size)).ToArray();
                var segments = source.Segments.Where(segment => segment.Address >= source.TextVmAddr)
                    .Select(segment => new SymbolSegment(segment.Name, segment.Address - source.TextVmAddr, segment.Size)).ToArray();
                var key = (uuid, architecture.CpuType, architecture.CpuSubtype);
                if (!external.TryGetValue(key, out var entries)) external[key] = entries = new List<SymbolArchive>();
                entries.Add(new SymbolArchive(uuid, key.CpuType, key.CpuSubtype, segments, symbols));
                matched++;
                added += symbols.Length;
                details.Add(new DsymMergeDetail(source.Uuid, source.ImageName, true, symbols.Length,
                    architectures[0].First().Segments.FirstOrDefault(segment => segment.Name == "__TEXT")?.Address ?? 0, source.TextVmAddr));
            }
            return new MergeResult(details.Count, matched, details.Count - matched, added, details);
        }
    }
}