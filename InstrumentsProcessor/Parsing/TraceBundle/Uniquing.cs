// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Claunia.PropertyList;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace InstrumentsProcessor.Parsing.TraceBundle
{
    internal sealed record DecodedBacktrace(ulong[] Addresses, long? ProcessId = null);

    /// <summary>
    /// Loads the uniquing data (strings and arrays) from a .trace bundle's
    /// corespace/run/core/uniquing directory. Used to resolve reference indices
    /// in bulkstore rows to actual string values, thread IDs, process IDs, etc.
    ///
    /// Array entries are parsed lazily on first access to avoid loading
    /// hundreds of megabytes of pre-parsed ulong[] arrays upfront.
    /// </summary>
    internal class Uniquing
    {
        private const int DataHeaderSize = 32;

        public List<string> Strings { get; } = new List<string>();

        // Reference offsets from the block-addressed index when present.
        // Entry i is at _arrayData[_entryOffsets[i]].
        private byte[] _arrayData;
        private int[] _entryOffsets;

        // Typed array uniquer — same format, used for engineering type-specific data.
        // References with hi=1 in the integer array point into this typed array.
        private byte[] _typedArrayData;
        private int[] _typedEntryOffsets;

        public Uniquing(string uniquingDir)
        {
            // Load strings
            var stringsPath = Path.Combine(uniquingDir, "strings");
            if (File.Exists(stringsPath))
            {
                var data = Decompressor.ReadCompressed(stringsPath);
                try
                {
                    var plist = PropertyListParser.Parse(data);
                    if (plist is NSArray arr)
                        foreach (var item in arr)
                            Strings.Add(item?.ToString() ?? "");
                }
                catch { }
            }

            // Load arrayUniquer by scanning the data file sequentially
            var arrDir = Path.Combine(uniquingDir, "arrayUniquer");
            if (Directory.Exists(arrDir))
                LoadFromDataFile(arrDir, out _arrayData, out _entryOffsets);

            // Load typedArrayUniquer
            var typedDir = Path.Combine(uniquingDir, "typedArrayUniquer");
            if (Directory.Exists(typedDir))
                LoadFromDataFile(typedDir, out _typedArrayData, out _typedEntryOffsets);
        }

        private void LoadFromDataFile(string basePath, out byte[] data, out int[] offsets)
        {
            data = null;
            offsets = null;

            var datPath = Path.Combine(basePath, "integeruniquer.data");
            if (!File.Exists(datPath))
                return;

            data = Decompressor.ReadCompressed(datPath);
            if (data.Length <= DataHeaderSize)
                return;

            var offsetList = new List<int>();
            var indexPath = Path.Combine(basePath, "integeruniquer.index");
            if (File.Exists(indexPath) && new FileInfo(indexPath).Length > 0)
            {
                var indexData = Decompressor.ReadCompressed(indexPath);
                if (indexData.Length < 40 || (indexData.Length - 40) % 8 != 0 ||
                    BinaryPrimitives.ReadUInt32LittleEndian(indexData) != 0x01234567 ||
                    BinaryPrimitives.ReadUInt64LittleEndian(indexData.AsSpan(32)) != 0)
                    throw new InvalidDataException("Unsupported integer uniquer index.");
                uint blockSize = BinaryPrimitives.ReadUInt32LittleEndian(indexData.AsSpan(28));
                if (blockSize < DataHeaderSize || blockSize > int.MaxValue)
                    throw new InvalidDataException("Invalid integer uniquer block size.");
                for (int index = 40; index < indexData.Length; index += 8)
                {
                    uint withinBlock = BinaryPrimitives.ReadUInt32LittleEndian(indexData.AsSpan(index));
                    uint block = BinaryPrimitives.ReadUInt32LittleEndian(indexData.AsSpan(index + 4));
                    ulong offset = (ulong)block * blockSize + withinBlock;
                    if (withinBlock >= blockSize || offset < DataHeaderSize || offset + 4 > (ulong)data.Length)
                        throw new InvalidDataException("Integer uniquer reference outside data.");
                    uint count = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan((int)offset));
                    ulong entrySize = 4UL + count * 8UL;
                    if (count > 10_000_000 || offset + entrySize > (ulong)data.Length || withinBlock + entrySize > blockSize)
                        throw new InvalidDataException("Invalid indexed integer uniquer entry.");
                    offsetList.Add((int)offset);
                }
                offsets = offsetList.ToArray();
                return;
            }

            int pos = DataHeaderSize;
            while (pos + 4 <= data.Length)
            {
                uint count = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(pos));
                int entrySize = 4 + (int)count * 8;
                if (count > 10_000_000 || pos + entrySize > data.Length)
                    break;

                offsetList.Add(pos);
                pos += entrySize;
            }
            offsets = offsetList.ToArray();
        }

        public string GetString(int idx) =>
            idx >= 0 && idx < Strings.Count ? Strings[idx] : $"<str:{idx}>";

        internal int GetTypedArrayCount() => _typedEntryOffsets?.Length ?? 0;
        internal int GetTypedArrayDataSize() => _typedArrayData?.Length ?? 0;
        internal ulong[] GetTypedArrayDirect(int idx) => GetTypedArray(idx);

        /// <summary>
        /// Parse and return the array entry at the given index, reading directly
        /// from the raw decompressed data. Each call parses the binary data fresh;
        /// callers (InternCache) are expected to cache results for hot entries.
        /// </summary>
        public ulong[] GetArray(int idx)
        {
            if (_entryOffsets == null || _arrayData == null)
                return null;
            if (idx < 0 || idx >= _entryOffsets.Length)
                return null;

            int pos = _entryOffsets[idx];
            if (pos + 4 > _arrayData.Length)
                return Array.Empty<ulong>();

            uint count = BinaryPrimitives.ReadUInt32LittleEndian(_arrayData.AsSpan(pos));
            pos += 4;
            if (count == 0 || pos + (int)count * 8 > _arrayData.Length)
                return Array.Empty<ulong>();

            var vals = new ulong[count];
            for (int j = 0; j < (int)count; j++)
            {
                uint lo = BinaryPrimitives.ReadUInt32LittleEndian(_arrayData.AsSpan(pos));
                uint hi = BinaryPrimitives.ReadUInt32LittleEndian(_arrayData.AsSpan(pos + 4));
                vals[j] = lo | ((ulong)hi << 32);
                pos += 8;
            }
            return vals;
        }

        /// <summary>Resolve thread reference → (tid, pid).</summary>
        public (ulong tid, long pid) ResolveThread(int refIdx)
        {
            var entry = GetArray(refIdx);
            if (entry == null || entry.Length < 2) return ((ulong)refIdx, -1);
            ulong tid = entry[0];
            var procEntry = GetArray((int)entry[1]);
            long pid = procEntry is { Length: >= 1 } ? (long)(uint)(procEntry[0] & 0xFFFFFFFF) : -1;
            return (tid, pid);
        }

        /// <summary>Resolve process reference → pid (32-bit).</summary>
        public long ResolveProcess(int refIdx)
        {
            var entry = GetArray(refIdx);
            // PIDs are 32-bit; entry[0] may carry extra bits in the high dword
            return entry is { Length: >= 1 } ? (long)(uint)(entry[0] & 0xFFFFFFFF) : refIdx;
        }

        /// <summary>
        /// Resolve process reference → process name string.
        /// For 5-element process entries, entry[1] and entry[4] have hi=1
        /// with lo being a byte offset into the typed array data (bit 31
        /// may be a flag and is masked off). The typed array entry at that
        /// offset contains string references for the process name.
        /// Returns null if the name cannot be resolved.
        /// </summary>
        public string ResolveProcessName(int refIdx)
        {
            var entry = GetArray(refIdx);
            if (entry == null || entry.Length < 2) return null;

            // For entries with hi=1 typed array references, interpret
            // lo & 0x7FFFFFFF as a byte offset into the typed array data.
            if (_typedArrayData != null)
            {
                // Try entry[1] first; for 5-element entries also try entry[4]
                int[] positions = entry.Length >= 5 ? new[] { 1, 4 } : new[] { 1 };
                foreach (int pos in positions)
                {
                    if (pos >= entry.Length) continue;
                    uint lo = (uint)(entry[pos] & 0xFFFFFFFF);
                    uint hi = (uint)(entry[pos] >> 32);
                    if (hi != 1) continue;

                    uint dataOffset = lo & 0x7FFFFFFF; // clear potential flag bit
                    var typedEntry = ReadTypedEntryAtOffset(dataOffset);
                    if (typedEntry != null)
                    {
                        foreach (ulong v in typedEntry)
                        {
                            int strIdx = (int)(v & 0xFFFFFFFF);
                            if (strIdx >= 0 && strIdx < Strings.Count)
                            {
                                string candidate = Strings[strIdx];
                                if (!string.IsNullOrEmpty(candidate))
                                    return candidate;
                            }
                        }
                    }
                }
            }

            // For 2-element entries, entry[1] may be a direct string ref
            if (entry.Length == 2)
            {
                int strIdx = (int)entry[1];
                if (strIdx >= 0 && strIdx < Strings.Count)
                    return Strings[strIdx];
            }

            return null;
        }

        /// <summary>
        /// Read a typed array entry directly at the given byte offset
        /// in the decompressed typed array data.
        /// </summary>
        private ulong[] ReadTypedEntryAtOffset(uint byteOffset)
        {
            if (_typedArrayData == null) return null;
            if (byteOffset + 4 > (uint)_typedArrayData.Length) return null;

            uint count = BinaryPrimitives.ReadUInt32LittleEndian(
                _typedArrayData.AsSpan((int)byteOffset));
            int pos = (int)byteOffset + 4;
            if (count == 0 || count > 10_000 || pos + (int)count * 8 > _typedArrayData.Length)
                return null;

            var vals = new ulong[count];
            for (int j = 0; j < (int)count; j++)
            {
                uint lo2 = BinaryPrimitives.ReadUInt32LittleEndian(_typedArrayData.AsSpan(pos));
                uint hi2 = BinaryPrimitives.ReadUInt32LittleEndian(_typedArrayData.AsSpan(pos + 4));
                vals[j] = lo2 | ((ulong)hi2 << 32);
                pos += 8;
            }
            return vals;
        }

        /// <summary>
        /// Read an entry from the typed array uniquer.
        /// </summary>
        private ulong[] GetTypedArray(int idx)
        {
            if (_typedEntryOffsets == null || _typedArrayData == null)
                return null;
            if (idx < 0 || idx >= _typedEntryOffsets.Length)
                return null;

            int pos = _typedEntryOffsets[idx];
            if (pos + 4 > _typedArrayData.Length)
                return null;

            uint count = BinaryPrimitives.ReadUInt32LittleEndian(_typedArrayData.AsSpan(pos));
            pos += 4;
            if (count == 0 || pos + (int)count * 8 > _typedArrayData.Length)
                return Array.Empty<ulong>();

            var vals = new ulong[count];
            for (int j = 0; j < (int)count; j++)
            {
                uint lo2 = BinaryPrimitives.ReadUInt32LittleEndian(_typedArrayData.AsSpan(pos));
                uint hi2 = BinaryPrimitives.ReadUInt32LittleEndian(_typedArrayData.AsSpan(pos + 4));
                vals[j] = lo2 | ((ulong)hi2 << 32);
                pos += 8;
            }
            return vals;
        }

        public DecodedBacktrace DecodeBacktrace(int refIdx, string engineeringType)
        {
            var entry = GetArray(refIdx);
            if (entry == null || entry.Length == 0)
                return new DecodedBacktrace(Array.Empty<ulong>());
            if (engineeringType == "XRCoreProfileCallstackTypeID")
            {
                if ((entry.Length != 4 && entry.Length != 5 && entry.Length != 8) || entry[0] > int.MaxValue ||
                    (entry[2] > int.MaxValue && entry[2] != uint.MaxValue))
                    return new DecodedBacktrace(Array.Empty<ulong>());
                var process = entry[2] == uint.MaxValue ? null : GetArray((int)entry[2]);
                long? pid = process != null && process.Length >= 1 ? (long)(uint)process[0] : null;
                return new DecodedBacktrace(GetArray((int)entry[0]) ?? Array.Empty<ulong>(), pid);
            }
            if (engineeringType?.IndexOf("TaggedBacktrace", StringComparison.OrdinalIgnoreCase) >= 0 || engineeringType == "tagged-backtrace")
            {
                if (entry.Length != 2 || entry[0] > int.MaxValue)
                    return new DecodedBacktrace(Array.Empty<ulong>());
                var chunks = GetArray((int)entry[0]);
                if (chunks == null || chunks.Length > 4096) return new DecodedBacktrace(Array.Empty<ulong>());
                var addresses = new List<ulong>();
                long? owner = null;
                for (int chunkIndex = 0; chunkIndex < chunks.Length; chunkIndex++)
                {
                    ulong chunk = chunks[chunkIndex];
                    uint frameRef = (uint)chunk;
                    uint processRef = (uint)(chunk >> 32);
                    var process = processRef <= int.MaxValue ? GetArray((int)processRef) : null;
                    if (process != null && process.Length > 0)
                    {
                        long pid = (uint)process[0];
                        if (owner.HasValue && owner != pid) return new DecodedBacktrace(Array.Empty<ulong>());
                        owner = pid;
                    }
                    if (frameRef == uint.MaxValue)
                    {
                        if (chunkIndex != chunks.Length - 1) return new DecodedBacktrace(Array.Empty<ulong>());
                        break;
                    }
                    if (frameRef > int.MaxValue) return new DecodedBacktrace(Array.Empty<ulong>());
                    var frames = GetArray((int)frameRef);
                    if (frames == null || addresses.Count + (long)frames.Length > 65536)
                        return new DecodedBacktrace(Array.Empty<ulong>());
                    addresses.AddRange(frames);
                }
                return new DecodedBacktrace(addresses.ToArray(), owner);
            }
            if (engineeringType == "backtrace" || engineeringType == "XRBacktraceTypeID")
                return new DecodedBacktrace(entry);
            return new DecodedBacktrace(Array.Empty<ulong>());
        }

        public ulong[] ResolveBacktrace(int refIdx) => DecodeBacktrace(refIdx, "backtrace").Addresses;
    }
}