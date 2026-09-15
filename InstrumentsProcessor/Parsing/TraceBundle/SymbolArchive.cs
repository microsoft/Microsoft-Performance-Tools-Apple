using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace InstrumentsProcessor.Parsing.TraceBundle
{
    internal sealed record SymbolSegment(string Name, ulong Address, ulong Size)
    {
        public bool Contains(ulong address) => address >= Address && address - Address < Size;
        public bool IsExecutable => Name == "__TEXT" || Name == "__TEXT_EXEC" || Name == "__TEXT_BOOT_EXEC";
    }

    internal sealed record SymbolEntry(string Name, ulong Address, ulong Size)
    {
        public bool IsPlaceholder => string.IsNullOrEmpty(Name) || Name.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
    }

    internal sealed class SymbolArchive
    {
        public Guid Uuid { get; }
        public uint CpuType { get; }
        public uint CpuSubtype { get; }
        public IReadOnlyList<SymbolSegment> Segments { get; }
        public IReadOnlyList<SymbolEntry> Symbols { get; }
        private readonly ulong[] prefixEnds;

        internal SymbolArchive(Guid uuid, uint cpuType, uint cpuSubtype,
            IEnumerable<SymbolSegment> segments, IEnumerable<SymbolEntry> symbols)
        {
            Uuid = uuid;
            CpuType = cpuType;
            CpuSubtype = cpuSubtype;
            Segments = segments.ToArray();
            Symbols = symbols.OrderBy(symbol => symbol.Address).ThenBy(symbol => symbol.Name, StringComparer.Ordinal).ToArray();
            prefixEnds = new ulong[Symbols.Count];
            ulong end = 0;
            for (int index = 0; index < Symbols.Count; index++)
            {
                var symbol = Symbols[index];
                if (symbol.Size > ulong.MaxValue - symbol.Address) throw new InvalidDataException("Symbol range overflow.");
                end = Math.Max(end, symbol.Address + symbol.Size);
                prefixEnds[index] = end;
            }
        }

        public SymbolEntry Find(ulong address, out bool ambiguous)
        {
            ambiguous = false;
            int low = 0, high = Symbols.Count;
            while (low < high)
            {
                int middle = low + (high - low) / 2;
                if (Symbols[middle].Address <= address) low = middle + 1;
                else high = middle;
            }
            SymbolEntry found = null;
            for (int index = low - 1; index >= 0 && prefixEnds[index] > address; index--)
            {
                var candidate = Symbols[index];
                if (address - candidate.Address >= candidate.Size) continue;
                if (found != null && (found.Address != candidate.Address || found.Size != candidate.Size))
                {
                    ambiguous = true;
                    return null;
                }
                if (found == null || found.IsPlaceholder || (!candidate.IsPlaceholder &&
                    string.CompareOrdinal(candidate.Name, found.Name) < 0)) found = candidate;
            }
            return found;
        }

        internal static Guid ReadUuid(byte[] bytes, int offset) =>
            Guid.ParseExact(BitConverter.ToString(bytes, offset, 16).Replace("-", ""), "N");

        internal static SymbolArchive Parse(byte[] bytes, Guid? expectedUuid = null)
        {
            if (bytes.Length < 0x60) throw new InvalidDataException("Truncated symbol archive.");
            uint Read32(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
            ulong Read64(int offset) => BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(offset, 8));
            if (Read32(0) != 7 || Read32(4) != bytes.Length) throw new InvalidDataException("Unsupported symbol archive header.");
            uint segmentCount = Read32(8), sectionCount = Read32(12), symbolCount = Read32(16);
            long symbolsOffset = 0x60L + segmentCount * 32L + sectionCount * 24L;
            uint stringSize = Read32(0x54);
            long stringsOffset = bytes.Length - (long)stringSize;
            if (segmentCount > 1024 || stringSize == 0 || stringsOffset < symbolsOffset + symbolCount * 24L)
                throw new InvalidDataException("Invalid symbol archive table bounds.");
            Guid uuid = ReadUuid(bytes, 0x34);
            if (expectedUuid.HasValue && expectedUuid.Value != uuid) throw new InvalidDataException("Symbol archive UUID mismatch.");
            var segments = new List<SymbolSegment>();
            for (int index = 0; index < segmentCount; index++)
            {
                int offset = 0x60 + index * 32;
                ulong address = Read64(offset), size = Read64(offset + 8);
                if (size > ulong.MaxValue - address) throw new InvalidDataException("Segment range overflow.");
                segments.Add(new SymbolSegment(Encoding.ASCII.GetString(bytes, offset + 16, 16).TrimEnd('\0'), address, size));
            }
            var symbols = new List<SymbolEntry>();
            string ReadName(uint nameIndex)
            {
                if (nameIndex >= stringSize) throw new InvalidDataException("Symbol name outside string table.");
                int nameStart = (int)stringsOffset + (int)nameIndex;
                int nameEnd = Array.IndexOf(bytes, (byte)0, nameStart);
                if (nameEnd < 0) throw new InvalidDataException("Unterminated symbol name.");
                return Encoding.UTF8.GetString(bytes, nameStart, nameEnd - nameStart);
            }
            for (int index = 0; index < symbolCount; index++)
            {
                int offset = (int)symbolsOffset + index * 24;
                string name = ReadName(Read32(offset + 16));
                uint displayNameIndex = Read32(offset + 12);
                if (displayNameIndex != 0)
                {
                    string displayName = ReadName(displayNameIndex);
                    if (displayName.Length != 0) name = displayName;
                }
                symbols.Add(new SymbolEntry(ItaniumDemangler.TryDemangle(name), Read32(offset), Read32(offset + 4)));
            }
            return new SymbolArchive(uuid, Read32(0x44), Read32(0x48), segments, symbols);
        }
    }
}