// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace InstrumentsProcessor.Parsing.TraceBundle
{
    /// <summary>
    /// Loads image symbols from a directory tree in the "symbol store" format used
    /// alongside Instruments traces from Microsoft build pipelines. Layout:
    ///
    ///   &lt;root&gt;/
    ///     &lt;ImageName&gt;/
    ///       &lt;UUID&gt;/
    ///         manifest.json          — JSON with fields: module, uuid, arch, ...
    ///         symbols.nm             — text: "&lt;hex_addr&gt; &lt;nm_type&gt; &lt;mangled&gt;" per line
    ///                                  (sorted by address, as emitted by `nm -n --defined-only`)
    ///
    /// Only text-section symbols (nm type 't' or 'T') are kept. Symbol addresses are
    /// link-time absolute addresses (e.g. starting at 0x100000000 for 64-bit main
    /// executables); the link-time __TEXT base is inferred as the lowest text address
    /// in the file (equivalent to `__mh_*_header`).
    ///
    /// Produces <see cref="DsymImage"/> instances so callers can pass results through
    /// the same UUID-based merge logic that <see cref="DsymLoader"/> uses.
    /// </summary>
    internal static class SymbolStoreLoader
    {
        private const string ManifestFileName = "manifest.json";
        private const string SymbolsFileName = "symbols.nm";

        /// <summary>
        /// Recursively find symbol-store entries under <paramref name="rootPath"/>.
        /// An "entry" is any directory containing both <c>manifest.json</c> and
        /// <c>symbols.nm</c>. Missing/unreadable files are skipped silently.
        /// </summary>
        public static IEnumerable<DsymImage> LoadFromPath(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
                yield break;

            IEnumerable<string> manifests;
            try
            {
                manifests = Directory.EnumerateFiles(rootPath, ManifestFileName, SearchOption.AllDirectories);
            }
            catch
            {
                yield break;
            }

            foreach (var manifestPath in manifests)
            {
                var dir = Path.GetDirectoryName(manifestPath);
                if (string.IsNullOrEmpty(dir)) continue;

                var symbolsPath = Path.Combine(dir, SymbolsFileName);
                if (!File.Exists(symbolsPath)) continue;

                DsymImage image;
                try { image = ParseEntry(manifestPath, symbolsPath); }
                catch { continue; }

                if (image != null) yield return image;
            }
        }

        /// <summary>
        /// Detect whether <paramref name="path"/> looks like a symbol store root.
        /// The heuristic: at least one descendant matches the pattern
        /// <c>&lt;path&gt;/*/*/manifest.json</c>.
        /// </summary>
        public static bool LooksLikeSymbolStore(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                return false;

            try
            {
                // Cheap probe: enumerate a small number of manifest.json files.
                using var e = Directory.EnumerateFiles(path, ManifestFileName, SearchOption.AllDirectories).GetEnumerator();
                return e.MoveNext();
            }
            catch
            {
                return false;
            }
        }

        private static DsymImage ParseEntry(string manifestPath, string symbolsPath)
        {
            string uuid = null;
            string module = null;
            ulong? loadAddr = null;

            try
            {
                using var stream = File.OpenRead(manifestPath);
                using var doc = JsonDocument.Parse(stream);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    if (doc.RootElement.TryGetProperty("uuid", out var u) && u.ValueKind == JsonValueKind.String)
                        uuid = u.GetString();
                    if (doc.RootElement.TryGetProperty("module", out var m) && m.ValueKind == JsonValueKind.String)
                        module = m.GetString();
                    // load_addr may be either a hex string (e.g. "0x187d9b000") or a
                    // decimal number. When present, it is the runtime __TEXT vmaddr
                    // observed for this image in the traced process.
                    if (doc.RootElement.TryGetProperty("load_addr", out var la))
                    {
                        if (la.ValueKind == JsonValueKind.String)
                        {
                            string s = la.GetString();
                            if (!string.IsNullOrEmpty(s))
                            {
                                if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
                                    s.StartsWith("0X", StringComparison.Ordinal))
                                    s = s.Substring(2);
                                if (ulong.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong parsed))
                                    loadAddr = parsed;
                            }
                        }
                        else if (la.ValueKind == JsonValueKind.Number)
                        {
                            if (la.TryGetUInt64(out ulong parsed)) loadAddr = parsed;
                        }
                    }
                }
            }
            catch
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(uuid))
            {
                // Fall back to the containing directory name if manifest.json has no UUID.
                uuid = Path.GetFileName(Path.GetDirectoryName(manifestPath));
            }
            if (string.IsNullOrWhiteSpace(uuid)) return null;

            // Normalize UUID to the canonical uppercase format used by SymbolCatalog
            // and DsymLoader so lookups compare directly.
            uuid = NormalizeUuid(uuid);

            if (string.IsNullOrWhiteSpace(module))
            {
                // Directory name two levels up ("<ImageName>/<UUID>/manifest.json").
                var parentDir = Path.GetDirectoryName(Path.GetDirectoryName(manifestPath));
                module = !string.IsNullOrEmpty(parentDir) ? Path.GetFileName(parentDir) : uuid;
            }

            // Two symbol layouts exist in the store:
            //   * RVA-based entries (system libs) — manifest carries `load_addr` and
            //     symbols.nm addresses are RVAs from that load_addr.
            //   * `nm -n` file addresses (Edge/Chromium binaries) — no `load_addr`;
            //     addresses are link-time (e.g. 0x100000000 for a main exec).
            // Anchor:
            //   * RVA-based → TextVmAddr = 0 (addresses ARE offsets from the base)
            //   * nm file-address → TextVmAddr = ordinary base (0 for dylib,
            //     0x100000000 for exec) inferred from the min address.
            bool isRvaFormat = loadAddr.HasValue;

            var functions = ParseSymbolsNm(symbolsPath, isRvaFormat, out ulong textVmAddr, out ulong textVmSize);
            if (functions.Count == 0) return null;

            return new DsymImage(uuid, module, textVmAddr, textVmSize, functions, loadAddr);
        }

        /// <summary>
        /// Parse a symbols.nm file. Format is
        /// <c>&lt;hex_addr&gt; &lt;type&gt; &lt;name&gt;</c> per line.
        /// Returns text-section symbols with sizes computed as the delta to the next
        /// distinct address.
        /// </summary>
        /// <param name="rvaFormat">When true, addresses are RVAs (offsets from a
        /// runtime load address) and <c>TextVmAddr</c> is forced to 0. When false,
        /// addresses are treated as link-time and the base is inferred from the
        /// minimum address (exec: <c>0x100000000</c>, dylib: <c>0</c>).</param>
        private static List<DsymFunction> ParseSymbolsNm(string path, bool rvaFormat, out ulong textVmAddr, out ulong textVmSize)
        {
            textVmAddr = 0;
            textVmSize = 0;

            var raw = new List<(ulong Addr, string Name)>();
            ulong minAddr = ulong.MaxValue;
            ulong maxAddr = 0;

            using (var reader = new StreamReader(path))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    // Expected format: "<hex_addr> <type> <name>" separated by whitespace.
                    // We don't split on all whitespace via string.Split with default args to
                    // avoid allocating extra empties; do it manually.
                    int p1 = IndexOfWhitespace(line, 0);
                    if (p1 <= 0) continue;
                    int p2 = SkipWhitespace(line, p1);
                    if (p2 >= line.Length) continue;
                    int p3 = IndexOfWhitespace(line, p2);
                    if (p3 <= p2) continue;
                    int p4 = SkipWhitespace(line, p3);
                    if (p4 >= line.Length) continue;

                    // Only text-section symbols
                    char typeChar = line[p2];
                    if (typeChar != 't' && typeChar != 'T') continue;
                    if (p3 - p2 != 1) continue; // type is a single char

                    string addrStr = line.Substring(0, p1);
                    if (!ulong.TryParse(addrStr, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong addr))
                        continue;

                    string name = line.Substring(p4).Trim();
                    if (name.Length == 0) continue;

                    if (addr < minAddr) minAddr = addr;
                    if (addr > maxAddr) maxAddr = addr;

                    raw.Add((addr, name));
                }
            }

            var results = new List<DsymFunction>();
            if (raw.Count == 0) return results;

            // The link-time __TEXT.vmaddr equals the address of the Mach-O header
            // (`__mh_execute_header` / `__mh_dylib_header` / `__mh_bundle_header`).
            // symbols.nm from `nm -n --defined-only` on a stripped binary usually
            // omits these headers, so infer from the minimum address:
            //   * RVA-format entries → 0 (addresses are already offsets from load_addr)
            //   * If minAddr >= 0x100000000 → main executable, __TEXT.vmaddr = 0x100000000
            //     (nm output for an exec starts at 0x100000000 with __mh_execute_header)
            //   * Otherwise → dylib / bundle whose __TEXT.vmaddr is 0. The first symbol
            //     sits at some offset inside __TEXT (e.g. 0x4000 for Chromium Framework);
            //     using minAddr here would drop that offset and mis-place every symbol.
            const ulong MainExecBase = 0x100000000UL;
            if (rvaFormat)
                textVmAddr = 0UL;
            else
                textVmAddr = (minAddr >= MainExecBase) ? MainExecBase : 0UL;
            // TextVmSize is a loose upper bound (last symbol - base). Anchored to
            // the delta between runtime and link-time __TEXT base during merge, so
            // this only needs to cover the range of symbols we're adding.
            textVmSize = maxAddr >= textVmAddr ? (maxAddr - textVmAddr) + 0x1000UL : 0UL;

            raw.Sort((a, b) => a.Addr.CompareTo(b.Addr));

            for (int i = 0; i < raw.Count; i++)
            {
                ulong addr = raw[i].Addr;
                if (i > 0 && raw[i - 1].Addr == addr) continue; // skip exact duplicates

                ulong nextAddr = textVmAddr + textVmSize;
                for (int j = i + 1; j < raw.Count; j++)
                {
                    if (raw[j].Addr > addr) { nextAddr = raw[j].Addr; break; }
                }
                ulong size = nextAddr - addr;
                // Symbol-store entries typically contain only observed symbols
                // (a sparse subset of the binary's symbol table), so consecutive
                // symbols may be many megabytes apart. Saturate rather than
                // clamping-to-4 so range lookup at an offset within an
                // over-attributed range still succeeds. Cap at 128 MB — larger
                // than any real Mach-O function, small enough to prevent
                // absurd cross-image over-attribution.
                if (size == 0) size = 4;
                else if (size > 128UL * 1024 * 1024) size = 128UL * 1024 * 1024;

                results.Add(new DsymFunction(raw[i].Name, addr, size));
            }

            return results;
        }

        private static int IndexOfWhitespace(string s, int start)
        {
            for (int i = start; i < s.Length; i++)
                if (s[i] == ' ' || s[i] == '\t') return i;
            return -1;
        }

        private static int SkipWhitespace(string s, int start)
        {
            int i = start;
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t')) i++;
            return i;
        }

        private static string NormalizeUuid(string s)
        {
            // Accept forms like "4c4c44a7-5555-3144-a148-ba6c6273b39f" or
            // "4C4C44A75555314BA148BA6C6273B39F" — output the dashed uppercase form.
            var digits = new char[32];
            int n = 0;
            foreach (char c in s)
            {
                if (IsHex(c))
                {
                    if (n >= 32) return s; // too long — return raw
                    digits[n++] = char.ToUpperInvariant(c);
                }
            }
            if (n != 32) return s;
            return
                new string(digits, 0, 8) + "-" +
                new string(digits, 8, 4) + "-" +
                new string(digits, 12, 4) + "-" +
                new string(digits, 16, 4) + "-" +
                new string(digits, 20, 12);
        }

        private static bool IsHex(char c) =>
            (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
    }
}
