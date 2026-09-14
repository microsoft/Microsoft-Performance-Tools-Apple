// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace InstrumentsProcessor.Parsing.TraceBundle
{
    /// <summary>
    /// Represents a loaded binary image segment with its address range.
    /// </summary>
    internal class ImageSegment
    {
        public string Uuid { get; }
        public string ImageName { get; internal set; }
        public string SegmentName { get; }
        public ulong VmAddr { get; }
        public ulong VmEnd { get; }

        public ImageSegment(string uuid, string imageName, string segmentName, ulong vmAddr, ulong vmSize)
        {
            Uuid = uuid;
            ImageName = imageName;
            SegmentName = segmentName;
            VmAddr = vmAddr;
            VmEnd = vmAddr + vmSize;
        }
    }

    /// <summary>
    /// A resolved function symbol with its address range within an image.
    /// </summary>
    internal class FunctionSymbol
    {
        public string Name { get; }
        public ulong AbsAddr { get; }
        public ulong AbsEnd { get; }
        /// <summary>
        /// The image (module) this symbol belongs to. Attached at insertion time so
        /// module attribution does not rely on a later segment-range lookup — which
        /// can miss when a dSYM's symbol extent exceeds the segment VmSize recorded
        /// in the trace's <c>.symbolsarchive</c>.
        /// </summary>
        public string ImageName { get; }

        public FunctionSymbol(string name, ulong absAddr, ulong size, string imageName = null)
        {
            Name = name;
            AbsAddr = absAddr;
            AbsEnd = absAddr + size;
            ImageName = imageName;
        }
    }

    /// <summary>
    /// Parses .symbolsarchive files from a .trace bundle's symbols/stores directory
    /// and provides address → function name resolution for backtraces.
    /// 
    /// The .symbolsarchive binary format (version 7):
    ///   [0x00]  u32  version (7)
    ///   [0x04]  u32  total file size
    ///   [0x08]  u32  number of segments
    ///   [0x0C]  u32  number of sub-sections
    ///   [0x10]  u32  number of symbols
    ///   [0x14..0x33] reserved / padding
    ///   [0x34..0x43] UUID (16 bytes)
    ///   [0x44..0x6F] arch/cpu info + more header
    ///   [0x70]  segment entries: each 32 bytes = name(16) + vmaddr(u64) + vmsize(u64)
    ///   [...]   sub-section entries: each 24 bytes = nameStrIdx(u64) + vmaddr(u64) + vmsize(u64)
    ///   [...]   symbol entries at 24-byte stride: strx(u32) + sentinel(u32) + relAddr(u32) + size(u32) + meta(8)
    ///   [...]   4 x u32 string table header
    ///   [...]   combined string table (section names + symbol names)
    /// </summary>
    internal class SymbolCatalog
    {
        private readonly List<ImageSegment> _segments = new List<ImageSegment>();
        private readonly List<FunctionSymbol> _functions = new List<FunctionSymbol>();
        private long _slide = 0;
        private bool _slideComputed = false;

        /// <summary>Number of image segments loaded.</summary>
        public int SegmentCount => _segments.Count;

        /// <summary>Number of function symbols loaded.</summary>
        public int FunctionCount => _functions.Count;

        /// <summary>The current ASLR slide applied globally during resolution
        /// (0 until <see cref="ResolveBacktrace"/> is called and computes it).</summary>
        internal long CurrentSlide => _slide;

        /// <summary>Whether the ASLR slide has been auto-computed.</summary>
        internal bool IsSlideComputed => _slideComputed;

        /// <summary>Diagnostic: find the segment covering an address (trying both raw and slide-adjusted lookup).</summary>
        internal (bool Found, string ImageName, string SegmentName, ulong VmAddr, ulong VmEnd, string Uuid) TryFindSegmentDiag(ulong address)
        {
            int idx = FindSegmentAnySpace(address, out _);
            if (idx < 0) return (false, null, null, 0, 0, null);
            var s = _segments[idx];
            return (true, s.ImageName, s.SegmentName, s.VmAddr, s.VmEnd, s.Uuid);
        }

        /// <summary>Diagnostic: find a function containing an address (trying both raw and slide-adjusted lookup).</summary>
        internal string TryFindFunctionDiag(ulong address)
        {
            return FindFunctionAnySpace(address, out _)?.Name;
        }

        /// <summary>Number of unique image UUIDs with a __TEXT segment.</summary>
        public int TextImageCount
        {
            get
            {
                var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var seg in _segments)
                    if (seg.SegmentName == "__TEXT") set.Add(seg.Uuid);
                return set.Count;
            }
        }

        /// <summary>
        /// Enumerate (UUID, ImageName) for every image with a __TEXT segment.
        /// Used to help users debug UUID mismatches between the trace and their dSYMs.
        /// </summary>
        public IEnumerable<(string Uuid, string ImageName)> EnumerateTextImages()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var seg in _segments)
            {
                if (seg.SegmentName != "__TEXT") continue;
                if (seen.Add(seg.Uuid))
                    yield return (seg.Uuid, seg.ImageName);
            }
        }

        /// <summary>
        /// Load all .symbolsarchive files from the trace bundle's symbols/stores directory.
        /// </summary>
        public static SymbolCatalog Load(string traceDir)
        {
            var catalog = new SymbolCatalog();
            var archiveDir = Path.Combine(traceDir, "symbols", "stores");
            if (!Directory.Exists(archiveDir))
                return catalog;

            foreach (var path in Directory.GetFiles(archiveDir, "*.symbolsarchive"))
            {
                catalog.ParseArchive(path);
            }

            // Sort segments and functions by address for binary search
            catalog._segments.Sort((a, b) => a.VmAddr.CompareTo(b.VmAddr));
            catalog._functions.Sort((a, b) => a.AbsAddr.CompareTo(b.AbsAddr));
            return catalog;
        }

        /// <summary>
        /// Detail about a single dSYM image encountered during a merge attempt.
        /// </summary>
        internal readonly struct DsymMergeDetail
        {
            public string Uuid { get; }
            public string ImageName { get; }
            public bool Matched { get; }
            public int FunctionsAdded { get; }
            /// <summary>__TEXT vmaddr as recorded in the trace's .symbolsarchive.</summary>
            public ulong RuntimeTextBase { get; }
            /// <summary>__TEXT vmaddr as recorded in the dSYM / symbol-store file.</summary>
            public ulong DsymTextBase { get; }

            public DsymMergeDetail(string uuid, string imageName, bool matched, int functionsAdded,
                ulong runtimeTextBase = 0, ulong dsymTextBase = 0)
            {
                Uuid = uuid;
                ImageName = imageName;
                Matched = matched;
                FunctionsAdded = functionsAdded;
                RuntimeTextBase = runtimeTextBase;
                DsymTextBase = dsymTextBase;
            }
        }

        /// <summary>
        /// Result of merging an external symbol source (e.g. a directory of dSYMs)
        /// into an existing catalog.
        /// </summary>
        internal readonly struct MergeResult
        {
            public int DsymImagesSeen { get; }
            public int MatchedImages { get; }
            public int UnmatchedImages { get; }
            public int FunctionsAdded { get; }
            public IReadOnlyList<DsymMergeDetail> Details { get; }

            public MergeResult(int seen, int matched, int unmatched, int added, IReadOnlyList<DsymMergeDetail> details)
            {
                DsymImagesSeen = seen;
                MatchedImages = matched;
                UnmatchedImages = unmatched;
                FunctionsAdded = added;
                Details = details;
            }
        }

        /// <summary>
        /// Merge function symbols from external dSYM bundles (or Mach-O files) under
        /// <paramref name="path"/> into this catalog. Each dSYM image is matched to
        /// an existing loaded image by UUID; unmatched dSYMs are skipped because we
        /// do not know the runtime load address of the corresponding binary.
        ///
        /// The runtime load address comes from the .symbolsarchive entry that
        /// Instruments recorded at capture time — even if that entry has no symbols,
        /// its __TEXT segment vmaddr is sufficient to place dSYM function symbols.
        ///
        /// <paramref name="path"/> may also point at a "symbol store" directory laid
        /// out as <c>&lt;path&gt;/&lt;ImageName&gt;/&lt;UUID&gt;/{manifest.json, symbols.nm}</c>;
        /// that format is auto-detected and loaded in addition to any dSYMs found.
        /// </summary>
        public MergeResult MergeDsyms(string path)
        {
            var details = new List<DsymMergeDetail>();
            if (string.IsNullOrWhiteSpace(path))
                return new MergeResult(0, 0, 0, 0, details);

            // Build UUID → runtime __TEXT vmaddr lookup from existing segments
            var uuidToTextBase = new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);
            foreach (var seg in _segments)
            {
                if (seg.SegmentName == "__TEXT" && !uuidToTextBase.ContainsKey(seg.Uuid))
                    uuidToTextBase[seg.Uuid] = seg.VmAddr;
            }

            int seen = 0, matched = 0, unmatched = 0, added = 0;
            bool anyAdded = false;
            bool anySegmentAdded = false;

            // Combine dSYM images and symbol-store entries under the same UUID-based
            // merge logic. Both loaders yield DsymImage records with an anchor address
            // (TextVmAddr) that we translate to the runtime __TEXT base via subtraction.
            IEnumerable<DsymImage> images = DsymLoader.LoadFromPath(path);
            if (SymbolStoreLoader.LooksLikeSymbolStore(path))
            {
                images = Concat(images, SymbolStoreLoader.LoadFromPath(path));
            }

            foreach (var dsym in images)
            {
                seen++;

                // Pick the runtime __TEXT base for this image. Priority:
                //   1. Manifest `load_addr` (when the source is an RVA-format store
                //      entry). This is the authoritative post-slide runtime address
                //      as observed on the Mac. The trace's own segment for the same
                //      UUID often stores a shared-cache *file offset* here (e.g.
                //      0xF2C0 for libsystem_pthread), which is NOT a runtime
                //      address — anchoring against it would place merged symbols
                //      in a range no sample ever sees.
                //   2. The trace's __TEXT segment VmAddr (for dSYM sources without
                //      a load_addr, or standalone Mach-Os where the trace records
                //      the true runtime address).
                //   3. Skip: cannot anchor.
                ulong runtimeTextBase;
                bool haveTraceSegment = uuidToTextBase.TryGetValue(dsym.Uuid, out ulong traceVmAddr);

                if (dsym.LoadAddr.HasValue)
                {
                    runtimeTextBase = dsym.LoadAddr.Value;

                    // Ensure a runtime-addressed segment exists so FindSegment can
                    // attribute raw samples to this image even when the trace's
                    // .symbolsarchive lists no segment (or lists only the shared-
                    // cache file-offset version at a different VmAddr).
                    bool needsSyntheticSegment = true;
                    foreach (var seg in _segments)
                    {
                        if (seg.SegmentName == "__TEXT" &&
                            string.Equals(seg.Uuid, dsym.Uuid, StringComparison.OrdinalIgnoreCase) &&
                            seg.VmAddr == runtimeTextBase)
                        {
                            needsSyntheticSegment = false;
                            break;
                        }
                    }
                    if (needsSyntheticSegment)
                    {
                        ulong segSize = dsym.TextVmSize > 0 ? dsym.TextVmSize : 0x1000UL;
                        _segments.Add(new ImageSegment(dsym.Uuid, dsym.ImageName, "__TEXT",
                                                        runtimeTextBase, segSize));
                        anySegmentAdded = true;
                    }
                }
                else if (haveTraceSegment)
                {
                    runtimeTextBase = traceVmAddr;
                }
                else
                {
                    unmatched++;
                    details.Add(new DsymMergeDetail(dsym.Uuid, dsym.ImageName, matched: false, functionsAdded: 0));
                    continue;
                }

                matched++;
                int addedForThisImage = 0;
                foreach (var f in dsym.Functions)
                {
                    // Translate the source's link-time (or RVA-space) address to a
                    // runtime absolute address using the delta between runtime and
                    // source __TEXT bases. For RVA-based symbol-store entries,
                    // TextVmAddr is 0 so rel == RVA and absAddr == load_addr + RVA.
                    ulong rel = f.Address - dsym.TextVmAddr;
                    ulong absAddr = runtimeTextBase + rel;
                    // Demangle Itanium C++ names once at merge time so the Stack
                    // column doesn't have to redo the work per frame render.
                    string displayName = ItaniumDemangler.TryDemangle(f.Name);
                    _functions.Add(new FunctionSymbol(displayName, absAddr, f.Size, dsym.ImageName));
                    added++;
                    addedForThisImage++;
                    anyAdded = true;
                }

                // Overwrite the catalog's image name with the dSYM's friendly name
                // (e.g. "Microsoft Edge Framework") for every segment sharing this UUID.
                // Instruments often leaves the image name blank for 3rd-party binaries
                // it did not have symbols for, which caused the Module column to render
                // as a bare UUID even though frames were resolved from the dSYM.
                if (!string.IsNullOrEmpty(dsym.ImageName))
                {
                    foreach (var seg in _segments)
                    {
                        if (string.Equals(seg.Uuid, dsym.Uuid, StringComparison.OrdinalIgnoreCase))
                        {
                            seg.ImageName = dsym.ImageName;
                        }
                    }
                }
                details.Add(new DsymMergeDetail(dsym.Uuid, dsym.ImageName, matched: true, functionsAdded: addedForThisImage,
                    runtimeTextBase: runtimeTextBase, dsymTextBase: dsym.TextVmAddr));
            }

            if (anyAdded)
            {
                _functions.Sort((a, b) => a.AbsAddr.CompareTo(b.AbsAddr));
            }
            if (anySegmentAdded)
            {
                _segments.Sort((a, b) => a.VmAddr.CompareTo(b.VmAddr));
            }

            return new MergeResult(seen, matched, unmatched, added, details);
        }

        private static IEnumerable<T> Concat<T>(IEnumerable<T> a, IEnumerable<T> b)
        {
            foreach (var x in a) yield return x;
            foreach (var x in b) yield return x;
        }

        private void ParseArchive(string path)
        {
            byte[] data;
            try { data = File.ReadAllBytes(path); }
            catch { return; }

            if (data.Length < 0x70) return;

            uint version = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(0));
            uint nSeg = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(8));
            uint nSubSec = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(0x0C));

            if (version != 7 || nSeg == 0 || nSeg > 1024) return;
            if (0x70 + nSeg * 32 > data.Length) return;

            // UUID at offset 0x34 (16 bytes)
            string uuid = FormatUuid(data.AsSpan(0x34, 16));

            // Extract source path from tail of file
            string sourcePath = ExtractSourcePath(data);
            string imageName = !string.IsNullOrEmpty(sourcePath)
                ? Path.GetFileName(sourcePath)
                : uuid;

            // Parse segment entries starting at offset 0x70
            ulong textSegBase = 0;
            for (int i = 0; i < (int)nSeg; i++)
            {
                int off = 0x70 + i * 32;
                string segName = Encoding.ASCII.GetString(data, off, 16).TrimEnd('\0');
                ulong vmAddr = BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(off + 16));
                ulong vmSize = BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(off + 24));

                if (segName == "__TEXT" && vmSize > 0)
                    textSegBase = vmAddr;

                if (vmSize > 0)
                {
                    _segments.Add(new ImageSegment(uuid, imageName, segName, vmAddr, vmSize));
                }
            }

            // Parse function symbols from the nlist region
            ParseFunctionSymbols(data, nSeg, nSubSec, textSegBase, imageName);
        }

        private void ParseFunctionSymbols(byte[] data, uint nSeg, uint nSubSec,
            ulong textSegBase, string imageName)
        {
            int afterSegs = 0x70 + (int)nSeg * 32;

            // Find the string table by searching for "MACH_HEADER\0" which always
            // starts the combined section+symbol string table.
            int strTableStart = FindStringTable(data, afterSegs);
            if (strTableStart < 0) return;

            // 4 x u32 header sits right before the string table
            int searchEnd = strTableStart - 16;
            if (searchEnd <= afterSegs) return;

            // The nlist region between segments and string table header uses a
            // variable-size entry format. Each function symbol entry contains:
            //   [strx:u32] [0xFFFFFFFF sentinel] [relAddr:u32] [size:u32]
            // We scan for the 0xFFFFFFFF sentinel and read strx from the
            // preceding 4 bytes, then addr/size from the following 8 bytes.
            for (int pos = afterSegs + 4; pos + 12 <= searchEnd; pos += 4)
            {
                uint val = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(pos));
                if (val != 0xFFFFFFFF) continue;

                uint strx = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(pos - 4));
                int strOff = strTableStart + (int)strx;
                if (strOff < strTableStart || strOff >= data.Length) continue;

                // Valid symbol names start with '_', '+', or '-' but not '__' (section names)
                byte firstByte = data[strOff];
                if (firstByte != (byte)'_' && firstByte != (byte)'+' && firstByte != (byte)'-')
                    continue;
                if (firstByte == (byte)'_' && strOff + 1 < data.Length && data[strOff + 1] == (byte)'_')
                    continue;

                // Read the function name
                int strEnd = strOff;
                while (strEnd < data.Length && data[strEnd] != 0) strEnd++;
                if (strEnd <= strOff) continue;

                uint relAddr = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(pos + 4));
                uint funcSize = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(pos + 8));

                // Skip only truly-invalid records (size zero). Very large sizes
                // are saturated below instead of dropped, so that range lookup
                // still succeeds for addresses inside oversized functions.
                if (funcSize == 0) continue;
                if (funcSize > 0x8000000) funcSize = 0x8000000; // 128 MB saturation

                string funcName = Encoding.UTF8.GetString(data, strOff, strEnd - strOff);

                // Compute absolute address: relAddr is relative to __TEXT segment base
                ulong absAddr = textSegBase + relAddr;

                // Demangle Itanium C++ names once at ingest so the Stack column
                // renders human-readable names without per-frame overhead.
                string displayName = ItaniumDemangler.TryDemangle(funcName);
                _functions.Add(new FunctionSymbol(displayName, absAddr, funcSize, imageName));
            }
        }

        private static int FindStringTable(byte[] data, int searchStart)
        {
            // The combined string table always starts with "MACH_HEADER\0"
            byte[] magic = Encoding.ASCII.GetBytes("MACH_HEADER");
            for (int i = searchStart; i < data.Length - magic.Length - 1; i++)
            {
                bool match = true;
                for (int j = 0; j < magic.Length; j++)
                {
                    if (data[i + j] != magic[j]) { match = false; break; }
                }
                if (match && data[i + magic.Length] == 0)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Resolve a raw address to a function name, or "ImageName+0xOffset" notation.
        /// Returns null if the address doesn't fall in any known image.
        /// <summary>
        /// Resolve a raw address to a function name, or "ImageName+0xOffset" notation.
        /// Returns null if the address doesn't fall in any known image.
        /// </summary>
        public string Resolve(ulong address)
        {
            // .symbolsarchive stores TWO kinds of segment VmAddrs simultaneously:
            //   - Shared-cache dylibs: link-time file offset (needs _slide subtracted)
            //   - Standalone Mach-Os (main exec / private frameworks): actual runtime
            //     address (already correct — needs no slide).
            // A single global _slide only fits one class, so try both spaces and
            // return whichever matches.
            FunctionSymbol fn = FindFunctionAnySpace(address, out ulong resolvedLookup);
            if (fn != null)
                return fn.Name;

            int idx = FindSegmentAnySpace(address, out ulong segLookup);
            if (idx < 0)
                return null;

            var seg = _segments[idx];
            ulong offset = segLookup - seg.VmAddr;
            return $"{seg.ImageName}+0x{offset:x}";
        }

        /// <summary>
        /// Resolve a full backtrace (array of addresses) into Frame objects.
        /// Uses function names when available, falls back to image+offset.
        /// On first call, auto-detects ASLR slide if shared cache segments don't
        /// match the backtrace addresses directly.
        /// </summary>
        public DataModels.Frame[] ResolveBacktrace(ulong[] addresses)
        {
            if (addresses == null || addresses.Length == 0)
                return Array.Empty<DataModels.Frame>();

            if (!_slideComputed)
                ComputeSlide(addresses);

            var frames = new DataModels.Frame[addresses.Length];
            for (int i = 0; i < addresses.Length; i++)
            {
                ulong rawAddr = addresses[i];
                string addr = $"0x{rawAddr:x}";

                // Try function-level resolution across both address spaces
                FunctionSymbol fn = FindFunctionAnySpace(rawAddr, out ulong matchedLookup);
                if (fn != null)
                {
                    // Prefer the ImageName recorded on the symbol itself. This is set
                    // at merge time and survives cases where the trace's segment
                    // VmSize is smaller than the dSYM's actual __TEXT extent, so
                    // FindSegment would otherwise report no segment for the address.
                    string modName = fn.ImageName;
                    if (string.IsNullOrEmpty(modName))
                    {
                        int segIdx = FindSegment(matchedLookup);
                        modName = segIdx >= 0 ? _segments[segIdx].ImageName : null;
                    }
                    DataModels.Module module = !string.IsNullOrEmpty(modName)
                        ? new DataModels.Module(modName) : null;
                    frames[i] = new DataModels.Frame(
                        new DataModels.Function(fn.Name, addr),
                        module);
                    continue;
                }

                // Fall back to image+offset (also across both address spaces)
                int segIdx2 = FindSegmentAnySpace(rawAddr, out ulong segLookup);
                if (segIdx2 >= 0)
                {
                    var seg = _segments[segIdx2];
                    ulong offset = segLookup - seg.VmAddr;
                    string resolved = $"{seg.ImageName}+0x{offset:x}";
                    frames[i] = new DataModels.Frame(
                        new DataModels.Function(resolved, addr),
                        new DataModels.Module(seg.ImageName));
                }
                else
                {
                    frames[i] = new DataModels.Frame(
                        new DataModels.Function(addr, addr));
                }
            }
            return frames;
        }

        /// <summary>
        /// Look up a function containing <paramref name="rawAddr"/>, trying both
        /// the runtime address space (raw) and the shared-cache file-offset space
        /// (raw - _slide). Returns the found function symbol and the lookup key that
        /// matched, or null if neither space contains the address.
        /// </summary>
        private FunctionSymbol FindFunctionAnySpace(ulong rawAddr, out ulong matchedLookup)
        {
            // First try raw (standalone Mach-Os whose segments were recorded as
            // runtime addresses match here without adjustment).
            FunctionSymbol fn = FindFunction(rawAddr);
            if (fn != null)
            {
                matchedLookup = rawAddr;
                return fn;
            }

            if (_slide != 0)
            {
                ulong slidLookup = (ulong)((long)rawAddr - _slide);
                fn = FindFunction(slidLookup);
                if (fn != null)
                {
                    matchedLookup = slidLookup;
                    return fn;
                }
            }

            matchedLookup = rawAddr;
            return null;
        }

        /// <summary>Segment-level analogue of <see cref="FindFunctionAnySpace"/>.</summary>
        private int FindSegmentAnySpace(ulong rawAddr, out ulong matchedLookup)
        {
            int idx = FindSegment(rawAddr);
            if (idx >= 0)
            {
                matchedLookup = rawAddr;
                return idx;
            }

            if (_slide != 0)
            {
                ulong slidLookup = (ulong)((long)rawAddr - _slide);
                idx = FindSegment(slidLookup);
                if (idx >= 0)
                {
                    matchedLookup = slidLookup;
                    return idx;
                }
            }

            matchedLookup = rawAddr;
            return -1;
        }

        /// <summary>
        /// Auto-detect the ASLR slide by finding the offset that makes the most
        /// backtrace addresses land in known segments.
        /// The iOS dyld shared cache is loaded at a base address that differs from
        /// the file offsets stored in .symbolsarchive files by a constant slide.
        /// </summary>
        private void ComputeSlide(ulong[] sampleAddresses)
        {
            _slideComputed = true;
            if (_segments.Count == 0 || sampleAddresses == null || sampleAddresses.Length == 0)
                return;

            // Check if addresses already match without slide
            int directMatches = 0;
            foreach (ulong addr in sampleAddresses)
            {
                if (FindSegment(addr) >= 0) directMatches++;
            }
            if (directMatches > sampleAddresses.Length / 3)
                return; // addresses match directly, no slide needed

            // Find the largest __TEXT segment for slide detection
            ImageSegment largestText = null;
            foreach (var seg in _segments)
            {
                if (seg.SegmentName == "__TEXT" && seg.VmAddr < 0x100000000)
                {
                    if (largestText == null || (seg.VmEnd - seg.VmAddr) > (largestText.VmEnd - largestText.VmAddr))
                        largestText = seg;
                }
            }
            if (largestText == null) return;

            // Collect unique addresses that might be in the shared cache
            // (high addresses that don't match standalone segments)
            var candidates = new List<ulong>();
            foreach (ulong addr in sampleAddresses)
            {
                if (addr > 0x100000000 && addr < 0x300000000 && FindSegment(addr) < 0)
                    candidates.Add(addr);
                if (candidates.Count >= 100) break;
            }
            if (candidates.Count == 0) return;

            // Try each candidate address as a potential slide source
            // slide = runtime_addr - file_offset, page-aligned (16K on arm64)
            long bestSlide = 0;
            int bestCount = 0;

            var testedSlides = new HashSet<long>();
            foreach (ulong addr in candidates)
            {
                // The address could be in any shared cache segment
                // Try the largest TEXT segment as the most likely hit
                long possibleSlide = (long)(addr - largestText.VmAddr);
                possibleSlide &= ~0x3FFFL; // page-align (16K)

                if (!testedSlides.Add(possibleSlide)) continue;

                int count = CountMatchesWithSlide(sampleAddresses, possibleSlide);
                if (count > bestCount)
                {
                    bestCount = count;
                    bestSlide = possibleSlide;
                }
            }

            // Fine-tune around the best slide
            if (bestCount > 0)
            {
                for (long delta = -0x40000; delta <= 0x40000; delta += 0x4000)
                {
                    long testSlide = bestSlide + delta;
                    if (testSlide == bestSlide) continue;
                    int count = CountMatchesWithSlide(sampleAddresses, testSlide);
                    if (count > bestCount)
                    {
                        bestCount = count;
                        bestSlide = testSlide;
                    }
                }
            }

            if (bestCount > sampleAddresses.Length / 10)
            {
                _slide = bestSlide;
            }
        }

        private int CountMatchesWithSlide(ulong[] addresses, long slide)
        {
            int count = 0;
            foreach (ulong addr in addresses)
            {
                ulong lookupAddr = (ulong)((long)addr - slide);
                if (FindSegment(lookupAddr) >= 0) count++;
            }
            return count;
        }

        /// <summary>Binary search for a function containing the given address.</summary>
        private FunctionSymbol FindFunction(ulong address)
        {
            int lo = 0, hi = _functions.Count - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                if (address < _functions[mid].AbsAddr)
                    hi = mid - 1;
                else if (address >= _functions[mid].AbsEnd)
                    lo = mid + 1;
                else
                    return _functions[mid];
            }
            return null;
        }

        /// <summary>Binary search for the segment containing this address.</summary>
        private int FindSegment(ulong address)
        {
            int lo = 0, hi = _segments.Count - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                if (address < _segments[mid].VmAddr)
                    hi = mid - 1;
                else if (address >= _segments[mid].VmEnd)
                    lo = mid + 1;
                else
                    return mid;
            }
            return -1;
        }

        private static string ExtractSourcePath(byte[] data)
        {
            // Source path is stored as a null-terminated string near the end of the file
            int tailStart = Math.Max(0, data.Length - 1024);
            var text = Encoding.UTF8.GetString(data, tailStart, data.Length - tailStart);
            var tokens = text.Split('\0');
            for (int i = tokens.Length - 1; i >= 0; i--)
            {
                var s = tokens[i];
                if (s.Contains('/') && s.Length > 8)
                {
                    bool allPrintable = true;
                    foreach (char c in s)
                        if (c < 32 || c > 126) { allPrintable = false; break; }
                    if (allPrintable) return s;
                }
            }
            return "";
        }

        private static string FormatUuid(ReadOnlySpan<byte> b) =>
            $"{b[0]:X2}{b[1]:X2}{b[2]:X2}{b[3]:X2}-" +
            $"{b[4]:X2}{b[5]:X2}-{b[6]:X2}{b[7]:X2}-" +
            $"{b[8]:X2}{b[9]:X2}-" +
            $"{b[10]:X2}{b[11]:X2}{b[12]:X2}{b[13]:X2}{b[14]:X2}{b[15]:X2}";
    }
}