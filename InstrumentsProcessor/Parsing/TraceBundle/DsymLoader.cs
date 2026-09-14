// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace InstrumentsProcessor.Parsing.TraceBundle
{
    /// <summary>
    /// A single image (architecture slice) extracted from a dSYM's DWARF companion
    /// Mach-O file. Contains the image UUID and function symbols with addresses in
    /// the dSYM's own (link-time) coordinate space.
    /// </summary>
    internal class DsymImage
    {
        public string Uuid { get; }
        public string ImageName { get; }
        /// <summary>The __TEXT segment vmaddr as recorded in the dSYM (or 0 for RVA-based sources).</summary>
        public ulong TextVmAddr { get; }
        /// <summary>The __TEXT segment vmsize as recorded in the dSYM.</summary>
        public ulong TextVmSize { get; }
        /// <summary>
        /// Optional: the runtime load address of the image as observed on the Mac at
        /// capture time (from the symbol-store manifest's <c>load_addr</c> field).
        /// Used as a fallback runtime <c>__TEXT</c> base when the trace's
        /// <c>.symbolsarchive</c> has no entry for this UUID.
        /// </summary>
        public ulong? LoadAddr { get; }
        public IReadOnlyList<DsymFunction> Functions { get; }

        public DsymImage(string uuid, string imageName, ulong textVmAddr, ulong textVmSize,
                         IReadOnlyList<DsymFunction> functions, ulong? loadAddr = null)
        {
            Uuid = uuid;
            ImageName = imageName;
            TextVmAddr = textVmAddr;
            TextVmSize = textVmSize;
            Functions = functions;
            LoadAddr = loadAddr;
        }
    }

    /// <summary>
    /// A single function symbol read from a dSYM's Mach-O symbol table.
    /// Address is in the dSYM's link-time coordinate space (n_value from nlist).
    /// Size is derived from the delta to the next symbol.
    /// </summary>
    internal readonly struct DsymFunction
    {
        public string Name { get; }
        public ulong Address { get; }
        public ulong Size { get; }

        public DsymFunction(string name, ulong address, ulong size)
        {
            Name = name;
            Address = address;
            Size = size;
        }
    }

    /// <summary>
    /// Parses Apple dSYM bundles and standalone Mach-O files to extract function
    /// symbols keyed by image UUID. Used to resolve backtrace addresses from images
    /// that Instruments did not have symbols for at capture time.
    ///
    /// A dSYM is a directory bundle whose layout is:
    ///   Foo.dSYM/Contents/Resources/DWARF/Foo
    ///
    /// The inner file is a Mach-O (possibly fat/universal) that contains:
    ///   - LC_UUID: 16-byte image UUID (matches the original binary)
    ///   - LC_SEGMENT_64 for __TEXT (giving link-time vmaddr / vmsize)
    ///   - LC_SYMTAB pointing at nlist_64 symbol entries and a string table
    ///
    /// Only 64-bit Mach-O files are supported (all modern macOS binaries).
    /// </summary>
    internal static class DsymLoader
    {
        // Mach-O magic numbers
        private const uint MH_MAGIC_64 = 0xFEEDFACF;
        private const uint MH_CIGAM_64 = 0xCFFAEDFE;
        private const uint FAT_MAGIC   = 0xCAFEBABE;
        private const uint FAT_CIGAM   = 0xBEBAFECA;
        private const uint FAT_MAGIC_64 = 0xCAFEBABF;
        private const uint FAT_CIGAM_64 = 0xBFBAFECA;

        // Load command types (with LC_REQ_DYLD bit stripped)
        private const uint LC_SEGMENT_64 = 0x19;
        private const uint LC_SYMTAB     = 0x02;
        private const uint LC_UUID       = 0x1B;
        private const uint LC_REQ_DYLD   = 0x80000000;

        // nlist n_type masks
        private const byte N_STAB = 0xE0;
        private const byte N_TYPE = 0x0E;
        private const byte N_SECT = 0x0E;
        private const byte N_FUN  = 0x24; // stab: function

        // Cap on individual function size when the size is inferred from the
        // delta to the next symbol. Real-world Mach-O functions never exceed
        // this in practice (largest Chromium code-gen functions are ~1 MB).
        // When the computed delta exceeds this, we saturate to the cap so that
        // range lookup at addresses inside the function still succeeds —
        // clamping to a tiny value (e.g. 4 bytes) would silently drop every
        // frame that is more than a couple of bytes past the symbol's start.
        private const ulong MaxReasonableFunctionSize = 128UL * 1024 * 1024;

        /// <summary>
        /// Recursively enumerate dSYM images from a path. The path may point to:
        ///   - a directory containing one or more .dSYM bundles (searched recursively)
        ///   - a single .dSYM bundle directory
        ///   - a Mach-O file directly (dSYM DWARF file or unstripped binary)
        /// </summary>
        public static IEnumerable<DsymImage> LoadFromPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) yield break;

            if (File.Exists(path))
            {
                foreach (var img in LoadMachO(path))
                    yield return img;
                yield break;
            }

            if (!Directory.Exists(path)) yield break;

            // If the directory itself is a .dSYM bundle, load only its DWARF file
            if (path.EndsWith(".dSYM", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var img in LoadDsymBundle(path))
                    yield return img;
                yield break;
            }

            // Otherwise, recursively find all .dSYM bundles under this directory
            IEnumerable<string> bundles;
            try
            {
                bundles = Directory.EnumerateDirectories(path, "*.dSYM", SearchOption.AllDirectories);
            }
            catch
            {
                yield break;
            }

            foreach (var bundle in bundles)
            {
                IEnumerable<DsymImage> images;
                try { images = LoadDsymBundle(bundle); }
                catch { continue; }

                foreach (var img in images)
                    yield return img;
            }
        }

        /// <summary>
        /// Load all architecture slices from a single .dSYM bundle directory.
        /// </summary>
        private static IEnumerable<DsymImage> LoadDsymBundle(string bundlePath)
        {
            var dwarfDir = Path.Combine(bundlePath, "Contents", "Resources", "DWARF");
            if (!Directory.Exists(dwarfDir))
                yield break;

            foreach (var file in Directory.EnumerateFiles(dwarfDir))
            {
                foreach (var img in LoadMachO(file))
                    yield return img;
            }
        }

        /// <summary>
        /// Parse a Mach-O file (thin or fat) and yield one DsymImage per 64-bit slice.
        /// </summary>
        private static IEnumerable<DsymImage> LoadMachO(string filePath)
        {
            byte[] data;
            try { data = File.ReadAllBytes(filePath); }
            catch { yield break; }

            if (data.Length < 4) yield break;

            var imageName = Path.GetFileName(filePath);
            uint magic = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(0));

            if (magic == FAT_MAGIC || magic == FAT_CIGAM ||
                magic == FAT_MAGIC_64 || magic == FAT_CIGAM_64)
            {
                foreach (var img in ParseFat(data, imageName, magic))
                    yield return img;
            }
            else
            {
                var img = ParseThin(data, 0, (uint)data.Length, imageName);
                if (img != null) yield return img;
            }
        }

        /// <summary>
        /// Iterate slices in a fat/universal binary and parse each thin slice.
        /// </summary>
        private static IEnumerable<DsymImage> ParseFat(byte[] data, string imageName, uint magic)
        {
            bool is64 = (magic == FAT_MAGIC_64 || magic == FAT_CIGAM_64);
            // Fat headers are always big-endian on disk
            if (data.Length < 8) yield break;

            uint nfat = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(4));
            if (nfat == 0 || nfat > 32) yield break;

            int entrySize = is64 ? 32 : 20;
            int headerEnd = 8 + (int)nfat * entrySize;
            if (headerEnd > data.Length) yield break;

            for (int i = 0; i < (int)nfat; i++)
            {
                int off = 8 + i * entrySize;
                ulong sliceOffset;
                ulong sliceSize;

                if (is64)
                {
                    // fat_arch_64: cputype(4) cpusubtype(4) offset(8) size(8) align(4) reserved(4)
                    sliceOffset = BinaryPrimitives.ReadUInt64BigEndian(data.AsSpan(off + 8));
                    sliceSize   = BinaryPrimitives.ReadUInt64BigEndian(data.AsSpan(off + 16));
                }
                else
                {
                    // fat_arch: cputype(4) cpusubtype(4) offset(4) size(4) align(4)
                    sliceOffset = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(off + 8));
                    sliceSize   = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(off + 12));
                }

                if (sliceOffset > (ulong)data.Length) continue;
                if (sliceSize == 0 || sliceOffset + sliceSize > (ulong)data.Length) continue;

                var img = ParseThin(data, (int)sliceOffset, (uint)sliceSize, imageName);
                if (img != null) yield return img;
            }
        }

        /// <summary>
        /// Parse a single thin (non-fat) 64-bit Mach-O slice starting at <paramref name="baseOff"/>.
        /// Returns null if the slice is not a 64-bit Mach-O or lacks required load commands.
        /// </summary>
        private static DsymImage ParseThin(byte[] data, int baseOff, uint sliceSize, string imageName)
        {
            if (baseOff < 0 || baseOff + 32 > data.Length) return null;

            uint magic = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(baseOff));
            bool bigEndian;

            if (magic == MH_MAGIC_64) bigEndian = false;
            else if (magic == MH_CIGAM_64) bigEndian = true;
            else return null; // 32-bit or unknown — skip

            // mach_header_64 (32 bytes): magic(4) cputype(4) cpusubtype(4) filetype(4)
            //                            ncmds(4) sizeofcmds(4) flags(4) reserved(4)
            uint ncmds       = ReadU32(data, baseOff + 16, bigEndian);
            uint sizeofcmds  = ReadU32(data, baseOff + 20, bigEndian);
            const int headerSize = 32;

            if (ncmds == 0 || ncmds > 4096) return null;
            if ((ulong)headerSize + sizeofcmds > sliceSize) return null;

            string uuid = null;
            ulong textVmAddr = 0;
            ulong textVmSize = 0;
            uint symOff = 0, nsyms = 0, strOff = 0, strSize = 0;

            int cmdPos = baseOff + headerSize;
            int cmdEnd = cmdPos + (int)sizeofcmds;

            for (uint i = 0; i < ncmds; i++)
            {
                if (cmdPos + 8 > cmdEnd) break;
                uint cmd = ReadU32(data, cmdPos, bigEndian) & ~LC_REQ_DYLD;
                uint cmdSize = ReadU32(data, cmdPos + 4, bigEndian);
                if (cmdSize < 8 || cmdPos + cmdSize > cmdEnd) break;

                if (cmd == LC_UUID && cmdSize >= 24)
                {
                    uuid = FormatUuid(data.AsSpan(cmdPos + 8, 16));
                }
                else if (cmd == LC_SEGMENT_64 && cmdSize >= 72)
                {
                    // segment_command_64: cmd(4) cmdsize(4) segname(16) vmaddr(8) vmsize(8)
                    //                     fileoff(8) filesize(8) maxprot(4) initprot(4) nsects(4) flags(4)
                    string segName = Encoding.ASCII.GetString(data, cmdPos + 8, 16).TrimEnd('\0');
                    if (segName == "__TEXT")
                    {
                        textVmAddr = ReadU64(data, cmdPos + 24, bigEndian);
                        textVmSize = ReadU64(data, cmdPos + 32, bigEndian);
                    }
                }
                else if (cmd == LC_SYMTAB && cmdSize >= 24)
                {
                    // symtab_command: cmd(4) cmdsize(4) symoff(4) nsyms(4) stroff(4) strsize(4)
                    symOff  = ReadU32(data, cmdPos + 8, bigEndian);
                    nsyms   = ReadU32(data, cmdPos + 12, bigEndian);
                    strOff  = ReadU32(data, cmdPos + 16, bigEndian);
                    strSize = ReadU32(data, cmdPos + 20, bigEndian);
                }

                cmdPos += (int)cmdSize;
            }

            if (uuid == null || nsyms == 0 || strSize == 0 || textVmSize == 0)
                return null;

            var functions = ParseSymbols(
                data, baseOff, sliceSize, bigEndian,
                symOff, nsyms, strOff, strSize,
                textVmAddr, textVmSize);

            if (functions.Count == 0)
                return null;

            return new DsymImage(uuid, imageName, textVmAddr, textVmSize, functions);
        }

        /// <summary>
        /// Read the nlist_64 table + string table and produce sorted, sized function symbols
        /// that fall within the image's __TEXT segment.
        /// </summary>
        private static List<DsymFunction> ParseSymbols(
            byte[] data, int baseOff, uint sliceSize, bool bigEndian,
            uint symOff, uint nsyms, uint strOff, uint strSize,
            ulong textVmAddr, ulong textVmSize)
        {
            var results = new List<DsymFunction>();

            // nlist_64 stride = 16 bytes: strx(4) type(1) sect(1) desc(2) value(8)
            const int NlistSize = 16;
            long symBytes = (long)nsyms * NlistSize;
            if (symOff + symBytes > sliceSize) return results;
            if ((long)strOff + strSize > sliceSize) return results;

            int symBase = baseOff + (int)symOff;
            int strBase = baseOff + (int)strOff;
            ulong textVmEnd = textVmAddr + textVmSize;

            // Collect raw (addr, name) pairs first, then sort + compute sizes.
            var raw = new List<(ulong Addr, string Name)>();

            for (uint i = 0; i < nsyms; i++)
            {
                int e = symBase + (int)(i * NlistSize);
                uint nStrx  = ReadU32(data, e, bigEndian);
                byte nType  = data[e + 4];
                byte nSect  = data[e + 5];
                ulong nValue = ReadU64(data, e + 8, bigEndian);

                // Accept: N_SECT non-stab symbols with a section, or N_FUN stabs (function entries)
                bool isStab = (nType & N_STAB) != 0;
                if (isStab)
                {
                    if (nType != N_FUN) continue;
                }
                else
                {
                    if ((nType & N_TYPE) != N_SECT) continue;
                    if (nSect == 0) continue;
                }

                if (nValue < textVmAddr || nValue >= textVmEnd) continue;
                if (nStrx == 0 || nStrx >= strSize) continue;

                int nameOff = strBase + (int)nStrx;
                if (nameOff >= data.Length) continue;

                int nameEnd = nameOff;
                int nameMax = strBase + (int)strSize;
                if (nameMax > data.Length) nameMax = data.Length;
                while (nameEnd < nameMax && data[nameEnd] != 0) nameEnd++;
                if (nameEnd <= nameOff) continue;

                // N_FUN stab pairs use a second N_FUN with empty name to carry the size —
                // skip those size-carrier entries; we recover sizes below via next-address delta.
                if (isStab && nameEnd == nameOff) continue;

                string name = Encoding.UTF8.GetString(data, nameOff, nameEnd - nameOff);
                if (name.Length == 0) continue;

                raw.Add((nValue, name));
            }

            if (raw.Count == 0) return results;

            // Sort by address (stable for equal addresses) and dedupe.
            raw.Sort((a, b) => a.Addr.CompareTo(b.Addr));

            for (int i = 0; i < raw.Count; i++)
            {
                ulong addr = raw[i].Addr;
                string name = raw[i].Name;

                // Skip exact-duplicate addresses (keep the first)
                if (i > 0 && raw[i - 1].Addr == addr) continue;

                // Compute size = distance to next distinct-address symbol,
                // clamped to __TEXT end and a sanity cap.
                ulong nextAddr = textVmEnd;
                for (int j = i + 1; j < raw.Count; j++)
                {
                    if (raw[j].Addr > addr) { nextAddr = raw[j].Addr; break; }
                }
                ulong size = nextAddr - addr;
                if (size == 0) size = 4;
                else if (size > MaxReasonableFunctionSize) size = MaxReasonableFunctionSize;

                results.Add(new DsymFunction(name, addr, size));
            }

            return results;
        }

        private static uint ReadU32(byte[] data, int off, bool bigEndian) =>
            bigEndian
                ? BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(off))
                : BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(off));

        private static ulong ReadU64(byte[] data, int off, bool bigEndian) =>
            bigEndian
                ? BinaryPrimitives.ReadUInt64BigEndian(data.AsSpan(off))
                : BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(off));

        private static string FormatUuid(ReadOnlySpan<byte> b) =>
            $"{b[0]:X2}{b[1]:X2}{b[2]:X2}{b[3]:X2}-" +
            $"{b[4]:X2}{b[5]:X2}-{b[6]:X2}{b[7]:X2}-" +
            $"{b[8]:X2}{b[9]:X2}-" +
            $"{b[10]:X2}{b[11]:X2}{b[12]:X2}{b[13]:X2}{b[14]:X2}{b[15]:X2}";
    }
}
