// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
//
// Adapted from D:\pcwsnap_parser\KcdataDecoder.cs for the Apple performance plugin.

using System;
using System.Collections.Generic;
using System.Text;

namespace InstrumentsProcessor.Parsing.PcwSnap
{
    // -----------------------------------------------------------------------
    // kcdata constants
    // -----------------------------------------------------------------------

    public static class Kcdata
    {
        // Buffer begin magic values
        public const uint BufferBeginStackshot      = 0x59a25807;
        public const uint BufferBeginDeltaStackshot = 0xDE17A59A;
        public const uint BufferBeginCompressed     = 0x434f4d50;
        public const uint TypeBufferEnd             = 0xF19158ED;

        // Generic types
        public const uint TypeInvalid          = 0x00;
        public const uint TypeStringDesc       = 0x01;
        public const uint TypeUint32Desc       = 0x02;
        public const uint TypeUint64Desc       = 0x03;
        public const uint TypeArrayLegacy      = 0x11;
        public const uint TypeContainerBegin   = 0x13;
        public const uint TypeContainerEnd     = 0x14;
        public const uint TypeArrayPad0        = 0x20;
        public const uint TypeLibraryLoadinfo64 = 0x31;
        public const uint TypeTimebase         = 0x32;
        public const uint TypeMachAbsoluteTime = 0x33;
        public const uint TypeUsecsSinceEpoch  = 0x35;
        public const uint TypePid              = 0x36;
        public const uint TypeProcname         = 0x37;

        // Stackshot types
        public const uint IoStats              = 0x901;
        public const uint GlobalMemStats       = 0x902;
        public const uint ContainerTask        = 0x903;
        public const uint ContainerThread      = 0x904;
        public const uint TaskSnapshot         = 0x905;
        public const uint ThreadSnapshot       = 0x906;
        public const uint DonatingPids         = 0x907;
        public const uint SharedcacheLoadinfo  = 0x908;
        public const uint ThreadName           = 0x909;
        public const uint KernStackframe       = 0x90A;
        public const uint KernStackframe64     = 0x90B;
        public const uint UserStackframe       = 0x90C;
        public const uint UserStackframe64     = 0x90D;
        public const uint BootArgs             = 0x90E;
        public const uint OsVersion            = 0x90F;
        public const uint KernPageSize         = 0x910;
        public const uint JetsamLevel          = 0x911;
        public const uint KernStackLr          = 0x913;
        public const uint KernStackLr64        = 0x914;
        public const uint UserStackLr          = 0x915;
        public const uint UserStackLr64        = 0x916;
        public const uint CpuTimes             = 0x919;
        public const uint StackshotDuration    = 0x91A;
        public const uint KernelcacheLoadinfo  = 0x91C;
        public const uint ThreadWaitinfo       = 0x91D;
        public const uint ThreadGroupSnapshot  = 0x91E;
        public const uint ThreadGroup          = 0x91F;
        public const uint InstrsCycles         = 0x923;
        public const uint UserStacktop         = 0x924;
        public const uint ThreadDispatchQueue  = 0x928;
        public const uint TaskCpuArchitecture  = 0x92A;
        public const uint DyldCompactInfo      = 0x937;
        public const uint TaskDeltaSnapshot    = 0x940;
        public const uint ThreadDeltaSnapshot  = 0x941;
        public const uint ContainerSharedcache = 0x942;
        public const uint SharedcacheInfo      = 0x943;
        public const uint SharedcacheId        = 0x945;
        public const uint CodeSigningInfo      = 0x946;
        public const uint OsBuildVersion       = 0x947;
        public const uint TaskMemorystatus     = 0x958;

        // Flags
        public const byte FlagsPaddingMask   = 0x0F;
        public const byte FlagsHasPadding    = 0x80;

        public static bool IsArrayType(uint type) => (type & ~0xFu) == 0x20 || type == TypeArrayLegacy;
        public static uint ArrayPadding(uint type) => (type & ~0xFu) == 0x20 ? (type & 0xFu) : 0;

        public static bool IsBufferBegin(uint type) =>
            type == BufferBeginStackshot ||
            type == BufferBeginDeltaStackshot ||
            type == BufferBeginCompressed;
    }

    // -----------------------------------------------------------------------
    // kcdata item (16-byte TLV header)
    // -----------------------------------------------------------------------

    public struct KcdataItem
    {
        public uint Type;
        public uint Size;
        public ulong Flags;
        public int DataOffset; // offset into the buffer where data starts

        public uint GetActualDataSize()
        {
            uint normType = Kcdata.IsArrayType(Type) ? Kcdata.TypeArrayLegacy : Type;

            switch (normType)
            {
                case Kcdata.TypeArrayLegacy:
                case Kcdata.TypeContainerBegin:
                    return Size;

                case Kcdata.ThreadSnapshot:
                    // Legacy v2 special case: 0x68 bytes, no padding flags
                    if (Size == RoundUp16(0x68) && (Flags & 0x8F) == 0)
                        return 0x68;
                    goto default;

                case Kcdata.SharedcacheLoadinfo:
                    if (Size == RoundUp16(24) && (Flags & 0x8F) == 0)
                        return 24;
                    goto default;

                default:
                    uint padding = (uint)(Flags & Kcdata.FlagsPaddingMask);
                    return Size >= padding ? Size - padding : 0;
            }
        }

        private static uint RoundUp16(uint v) => (v + 15) & ~15u;
    }

    // -----------------------------------------------------------------------
    // Decoded structs
    // -----------------------------------------------------------------------

    public class KcMemSnapshot
    {
        public uint Magic { get; set; }
        public uint FreePages { get; set; }
        public uint ActivePages { get; set; }
        public uint InactivePages { get; set; }
        public uint PurgeablePages { get; set; }
        public uint WiredPages { get; set; }
        public uint SpeculativePages { get; set; }
        public uint ThrottledPages { get; set; }
        public uint FilebackedPages { get; set; }
        public uint Compressions { get; set; }
        public uint Decompressions { get; set; }
        public uint CompressorSize { get; set; }
        public int BusyBufferCount { get; set; }
        public uint PagesWanted { get; set; }
        public uint PagesReclaimed { get; set; }
        // v2 fields
        public uint SharedRegionPages { get; set; }
        public uint CompressedPages { get; set; }
        public uint SwappedPages { get; set; }
    }

    public class KcTaskSnapshot
    {
        public ulong UniquePid { get; set; }
        public ulong SsFlags { get; set; }
        public ulong UserTimeTerminated { get; set; }
        public ulong SystemTimeTerminated { get; set; }
        public ulong StartSec { get; set; }
        public ulong TaskSize { get; set; }
        public ulong MaxResidentSize { get; set; }
        public uint SuspendCount { get; set; }
        public uint Faults { get; set; }
        public uint Pageins { get; set; }
        public uint CowFaults { get; set; }
        public uint WasThrottled { get; set; }
        public uint DidThrottle { get; set; }
        public uint LatencyQos { get; set; }
        public int Pid { get; set; }
        public string ProcessName { get; set; } = "";

        // From INSTRS_CYCLES at task level
        public ulong Instructions { get; set; }
        public ulong Cycles { get; set; }

        // CPU architecture
        public int CpuType { get; set; }
        public int CpuSubtype { get; set; }

        // I/O stats
        public KcIoStatsSnapshot IoStats { get; set; }

        // Threads
        public List<KcThreadSnapshot> Threads { get; set; } = new List<KcThreadSnapshot>();
    }

    public class KcThreadSnapshot
    {
        public ulong ThreadId { get; set; }
        public ulong WaitEvent { get; set; }
        public ulong Continuation { get; set; }
        public ulong TotalSyscalls { get; set; }
        public ulong VoucherIdentifier { get; set; }
        public ulong DispatchQueueSerial { get; set; }
        public ulong UserTime { get; set; }
        public ulong SystemTime { get; set; }
        public ulong SsFlags { get; set; }
        public ulong LastRunTime { get; set; }
        public ulong LastMadeRunnableTime { get; set; }
        public uint State { get; set; }
        public uint SchedFlags { get; set; }
        public short BasePriority { get; set; }
        public short SchedPriority { get; set; }
        public byte Eqos { get; set; }
        public byte Rqos { get; set; }
        public byte RqosOverride { get; set; }
        public byte IoTier { get; set; }

        // v3+ field
        public ulong ThreadT { get; set; }

        // Per-thread INSTRS_CYCLES
        public ulong Instructions { get; set; }
        public ulong Cycles { get; set; }

        // Thread name (if present)
        public string Name { get; set; }

        // Dispatch queue label
        public string DispatchQueueLabel { get; set; }

        // Stack frames (return addresses)
        public ulong[] KernelStack { get; set; } = Array.Empty<ulong>();
        public ulong[] UserStack { get; set; } = Array.Empty<ulong>();
    }

    public class KcIoStatsSnapshot
    {
        public ulong DiskReadsCount { get; set; }
        public ulong DiskReadsSize { get; set; }
        public ulong DiskWritesCount { get; set; }
        public ulong DiskWritesSize { get; set; }
        public ulong PagingCount { get; set; }
        public ulong PagingSize { get; set; }
        public ulong NonPagingCount { get; set; }
        public ulong NonPagingSize { get; set; }
        public ulong MetadataCount { get; set; }
        public ulong MetadataSize { get; set; }
        public ulong DataCount { get; set; }
        public ulong DataSize { get; set; }
        public ulong[] IoPriorityCount { get; set; } = new ulong[4];
        public ulong[] IoPrioritySize { get; set; } = new ulong[4];
    }

    // -----------------------------------------------------------------------
    // Top-level kcdata decode result
    // -----------------------------------------------------------------------

    public class KcdataDecoded
    {
        /// <summary>True when the kcdata blob is a delta stackshot (values are already deltas).</summary>
        public bool IsDelta { get; set; }
        public uint KernPageSize { get; set; }
        public uint JetsamLevel { get; set; }
        public ulong MachAbsoluteTime { get; set; }
        public uint TimebaseNumer { get; set; }
        public uint TimebaseDenom { get; set; }
        public string OsVersion { get; set; }
        public string OsBuildVersion { get; set; }
        public string BootArgs { get; set; }
        public KcMemSnapshot MemStats { get; set; }
        public List<KcTaskSnapshot> Tasks { get; set; } = new List<KcTaskSnapshot>();
    }

    // -----------------------------------------------------------------------
    // Decoder
    // -----------------------------------------------------------------------

    public static class KcdataDecoder
    {
        public static KcdataDecoded Decode(byte[] blob)
        {
            var result = new KcdataDecoded();
            int pos = 0;

            // Read buffer begin header item
            var header = ReadItem(blob, ref pos);
            if (!Kcdata.IsBufferBegin(header.Type))
                throw new InvalidOperationException(
                    $"Not a kcdata buffer: magic=0x{header.Type:X8}");

            result.IsDelta = header.Type == Kcdata.BufferBeginDeltaStackshot;

            // Iterate top-level items
            while (pos < blob.Length)
            {
                var item = ReadItem(blob, ref pos);

                if (item.Type == Kcdata.TypeBufferEnd)
                    break;

                switch (item.Type)
                {
                    case Kcdata.TypeMachAbsoluteTime:
                        result.MachAbsoluteTime = BitConverter.ToUInt64(blob, item.DataOffset);
                        break;

                    case Kcdata.TypeTimebase:
                        result.TimebaseNumer = BitConverter.ToUInt32(blob, item.DataOffset);
                        result.TimebaseDenom = BitConverter.ToUInt32(blob, item.DataOffset + 4);
                        break;

                    case Kcdata.KernPageSize:
                        result.KernPageSize = BitConverter.ToUInt32(blob, item.DataOffset);
                        break;

                    case Kcdata.JetsamLevel:
                        result.JetsamLevel = BitConverter.ToUInt32(blob, item.DataOffset);
                        break;

                    case Kcdata.OsVersion:
                        result.OsVersion = ReadCString(blob, item.DataOffset, item.GetActualDataSize());
                        break;

                    case Kcdata.OsBuildVersion:
                        result.OsBuildVersion = ReadCString(blob, item.DataOffset, item.GetActualDataSize());
                        break;

                    case Kcdata.BootArgs:
                        result.BootArgs = ReadCString(blob, item.DataOffset, item.GetActualDataSize());
                        break;

                    case Kcdata.GlobalMemStats:
                        result.MemStats = DecodeMemSnapshot(blob, item);
                        break;

                    case Kcdata.TypeContainerBegin:
                        uint containerType = BitConverter.ToUInt32(blob, item.DataOffset);
                        if (containerType == Kcdata.ContainerTask)
                        {
                            var task = DecodeTask(blob, ref pos, item.Flags);
                            result.Tasks.Add(task);
                        }
                        else
                        {
                            SkipContainer(blob, ref pos);
                        }
                        break;

                    default:
                        // Array or unknown — skip
                        break;
                }
            }

            return result;
        }

        // -------------------------------------------------------------------
        // Task container
        // -------------------------------------------------------------------

        private static KcTaskSnapshot DecodeTask(byte[] blob, ref int pos, ulong containerId)
        {
            var task = new KcTaskSnapshot { UniquePid = containerId };

            while (pos < blob.Length)
            {
                var item = ReadItem(blob, ref pos);

                if (item.Type == Kcdata.TypeContainerEnd)
                    break;

                if (item.Type == Kcdata.TypeBufferEnd)
                    break;

                switch (item.Type)
                {
                    case Kcdata.TaskSnapshot:
                        DecodeTaskSnapshotStruct(blob, item, task);
                        break;

                    case Kcdata.TaskDeltaSnapshot:
                        // Delta snapshots have a subset of fields; extract pid + name
                        if (item.GetActualDataSize() >= 88 + 32)
                        {
                            task.Pid = BitConverter.ToInt32(blob, item.DataOffset + 84);
                            task.ProcessName = ReadCString(blob, item.DataOffset + 88, 32);
                        }
                        break;

                    case Kcdata.InstrsCycles:
                        uint icSize = item.GetActualDataSize();
                        if (icSize >= 16)
                        {
                            task.Instructions = BitConverter.ToUInt64(blob, item.DataOffset);
                            task.Cycles = BitConverter.ToUInt64(blob, item.DataOffset + 8);
                        }
                        break;

                    case Kcdata.TaskCpuArchitecture:
                        if (item.GetActualDataSize() >= 8)
                        {
                            task.CpuType = BitConverter.ToInt32(blob, item.DataOffset);
                            task.CpuSubtype = BitConverter.ToInt32(blob, item.DataOffset + 4);
                        }
                        break;

                    case Kcdata.IoStats:
                        task.IoStats = DecodeIoStats(blob, item);
                        break;

                    case Kcdata.TypeContainerBegin:
                        uint subType = BitConverter.ToUInt32(blob, item.DataOffset);
                        if (subType == Kcdata.ContainerThread)
                        {
                            var thread = DecodeThread(blob, ref pos, item.Flags);
                            task.Threads.Add(thread);
                        }
                        else
                        {
                            SkipContainer(blob, ref pos);
                        }
                        break;

                    default:
                        // Array or unknown — skip
                        break;
                }
            }

            return task;
        }

        private static void DecodeTaskSnapshotStruct(byte[] blob, KcdataItem item, KcTaskSnapshot task)
        {
            int o = item.DataOffset;
            uint sz = item.GetActualDataSize();
            if (sz < 120) return;

            task.UniquePid = BitConverter.ToUInt64(blob, o);
            task.SsFlags = BitConverter.ToUInt64(blob, o + 8);
            task.UserTimeTerminated = BitConverter.ToUInt64(blob, o + 16);
            task.SystemTimeTerminated = BitConverter.ToUInt64(blob, o + 24);
            task.StartSec = BitConverter.ToUInt64(blob, o + 32);
            task.TaskSize = BitConverter.ToUInt64(blob, o + 40);
            task.MaxResidentSize = BitConverter.ToUInt64(blob, o + 48);
            task.SuspendCount = BitConverter.ToUInt32(blob, o + 56);
            task.Faults = BitConverter.ToUInt32(blob, o + 60);
            task.Pageins = BitConverter.ToUInt32(blob, o + 64);
            task.CowFaults = BitConverter.ToUInt32(blob, o + 68);
            task.WasThrottled = BitConverter.ToUInt32(blob, o + 72);
            task.DidThrottle = BitConverter.ToUInt32(blob, o + 76);
            task.LatencyQos = BitConverter.ToUInt32(blob, o + 80);
            task.Pid = BitConverter.ToInt32(blob, o + 84);
            task.ProcessName = ReadCString(blob, o + 88, 32);
        }

        // -------------------------------------------------------------------
        // Thread container
        // -------------------------------------------------------------------

        private static KcThreadSnapshot DecodeThread(byte[] blob, ref int pos, ulong containerId)
        {
            var thread = new KcThreadSnapshot { ThreadId = containerId };

            while (pos < blob.Length)
            {
                var item = ReadItem(blob, ref pos);

                if (item.Type == Kcdata.TypeContainerEnd)
                    break;

                if (item.Type == Kcdata.TypeBufferEnd)
                    break;

                switch (item.Type)
                {
                    case Kcdata.ThreadSnapshot:
                        DecodeThreadSnapshotStruct(blob, item, thread);
                        break;

                    case Kcdata.ThreadDeltaSnapshot:
                        // Minimal delta decode
                        if (item.GetActualDataSize() >= 104)
                            DecodeThreadSnapshotStruct(blob, item, thread);
                        break;

                    case Kcdata.InstrsCycles:
                        uint icSize = item.GetActualDataSize();
                        if (icSize >= 16)
                        {
                            thread.Instructions = BitConverter.ToUInt64(blob, item.DataOffset);
                            thread.Cycles = BitConverter.ToUInt64(blob, item.DataOffset + 8);
                        }
                        break;

                    case Kcdata.ThreadName:
                        thread.Name = ReadCString(blob, item.DataOffset, item.GetActualDataSize());
                        break;

                    case Kcdata.ThreadDispatchQueue:
                        thread.DispatchQueueLabel = ReadCString(blob, item.DataOffset, item.GetActualDataSize());
                        break;

                    default:
                        if (Kcdata.IsArrayType(item.Type))
                        {
                            uint elemType = (uint)(item.Flags >> 32);
                            uint elemCount = (uint)(item.Flags & 0xFFFFFFFF);
                            uint padding = Kcdata.ArrayPadding(item.Type);

                            switch (elemType)
                            {
                                case Kcdata.KernStackLr64:
                                    thread.KernelStack = ReadUInt64Array(blob, item.DataOffset, elemCount, item.Size - padding);
                                    break;
                                case Kcdata.UserStackLr64:
                                    thread.UserStack = ReadUInt64Array(blob, item.DataOffset, elemCount, item.Size - padding);
                                    break;
                                case Kcdata.KernStackLr:
                                    thread.KernelStack = ReadUInt32AsUInt64Array(blob, item.DataOffset, elemCount, item.Size - padding);
                                    break;
                                case Kcdata.UserStackLr:
                                    thread.UserStack = ReadUInt32AsUInt64Array(blob, item.DataOffset, elemCount, item.Size - padding);
                                    break;
                            }
                        }
                        break;
                }
            }

            return thread;
        }

        private static void DecodeThreadSnapshotStruct(byte[] blob, KcdataItem item, KcThreadSnapshot t)
        {
            int o = item.DataOffset;
            uint sz = item.GetActualDataSize();
            if (sz < 104) return;

            t.ThreadId = BitConverter.ToUInt64(blob, o);
            t.WaitEvent = BitConverter.ToUInt64(blob, o + 8);
            t.Continuation = BitConverter.ToUInt64(blob, o + 16);
            t.TotalSyscalls = BitConverter.ToUInt64(blob, o + 24);
            t.VoucherIdentifier = BitConverter.ToUInt64(blob, o + 32);
            t.DispatchQueueSerial = BitConverter.ToUInt64(blob, o + 40);
            t.UserTime = BitConverter.ToUInt64(blob, o + 48);
            t.SystemTime = BitConverter.ToUInt64(blob, o + 56);
            t.SsFlags = BitConverter.ToUInt64(blob, o + 64);
            t.LastRunTime = BitConverter.ToUInt64(blob, o + 72);
            t.LastMadeRunnableTime = BitConverter.ToUInt64(blob, o + 80);
            t.State = BitConverter.ToUInt32(blob, o + 88);
            t.SchedFlags = BitConverter.ToUInt32(blob, o + 92);
            t.BasePriority = BitConverter.ToInt16(blob, o + 96);
            t.SchedPriority = BitConverter.ToInt16(blob, o + 98);
            t.Eqos = blob[o + 100];
            t.Rqos = blob[o + 101];
            t.RqosOverride = blob[o + 102];
            t.IoTier = blob[o + 103];

            // v3+: thread_t pointer
            if (sz >= 112)
                t.ThreadT = BitConverter.ToUInt64(blob, o + 104);
        }

        // -------------------------------------------------------------------
        // I/O stats snapshot
        // -------------------------------------------------------------------

        private static KcIoStatsSnapshot DecodeIoStats(byte[] blob, KcdataItem item)
        {
            int o = item.DataOffset;
            uint sz = item.GetActualDataSize();
            var io = new KcIoStatsSnapshot();
            // 20 x uint64 = 160 bytes minimum
            if (sz < 160) return io;

            io.DiskReadsCount = BitConverter.ToUInt64(blob, o);
            io.DiskReadsSize = BitConverter.ToUInt64(blob, o + 8);
            io.DiskWritesCount = BitConverter.ToUInt64(blob, o + 16);
            io.DiskWritesSize = BitConverter.ToUInt64(blob, o + 24);
            io.PagingCount = BitConverter.ToUInt64(blob, o + 32);
            io.PagingSize = BitConverter.ToUInt64(blob, o + 40);
            io.NonPagingCount = BitConverter.ToUInt64(blob, o + 48);
            io.NonPagingSize = BitConverter.ToUInt64(blob, o + 56);
            io.MetadataCount = BitConverter.ToUInt64(blob, o + 64);
            io.MetadataSize = BitConverter.ToUInt64(blob, o + 72);
            io.DataCount = BitConverter.ToUInt64(blob, o + 80);
            io.DataSize = BitConverter.ToUInt64(blob, o + 88);
            for (int i = 0; i < 4; i++)
            {
                io.IoPriorityCount[i] = BitConverter.ToUInt64(blob, o + 96 + i * 8);
            }
            for (int i = 0; i < 4; i++)
            {
                io.IoPrioritySize[i] = BitConverter.ToUInt64(blob, o + 128 + i * 8);
            }
            return io;
        }

        // -------------------------------------------------------------------
        // Memory snapshot
        // -------------------------------------------------------------------

        private static KcMemSnapshot DecodeMemSnapshot(byte[] blob, KcdataItem item)
        {
            int o = item.DataOffset;
            uint sz = item.GetActualDataSize();
            var m = new KcMemSnapshot();
            if (sz < 61) return m;

            m.Magic = BitConverter.ToUInt32(blob, o);
            m.FreePages = BitConverter.ToUInt32(blob, o + 4);
            m.ActivePages = BitConverter.ToUInt32(blob, o + 8);
            m.InactivePages = BitConverter.ToUInt32(blob, o + 12);
            m.PurgeablePages = BitConverter.ToUInt32(blob, o + 16);
            m.WiredPages = BitConverter.ToUInt32(blob, o + 20);
            m.SpeculativePages = BitConverter.ToUInt32(blob, o + 24);
            m.ThrottledPages = BitConverter.ToUInt32(blob, o + 28);
            m.FilebackedPages = BitConverter.ToUInt32(blob, o + 32);
            m.Compressions = BitConverter.ToUInt32(blob, o + 36);
            m.Decompressions = BitConverter.ToUInt32(blob, o + 40);
            m.CompressorSize = BitConverter.ToUInt32(blob, o + 44);
            m.BusyBufferCount = BitConverter.ToInt32(blob, o + 48);
            m.PagesWanted = BitConverter.ToUInt32(blob, o + 52);
            m.PagesReclaimed = BitConverter.ToUInt32(blob, o + 56);

            // v2
            if (sz >= 73)
            {
                m.SharedRegionPages = BitConverter.ToUInt32(blob, o + 61);
                m.CompressedPages = BitConverter.ToUInt32(blob, o + 65);
                m.SwappedPages = BitConverter.ToUInt32(blob, o + 69);
            }

            return m;
        }

        // -------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------

        private static KcdataItem ReadItem(byte[] blob, ref int pos)
        {
            var item = new KcdataItem
            {
                Type = BitConverter.ToUInt32(blob, pos),
                Size = BitConverter.ToUInt32(blob, pos + 4),
                Flags = BitConverter.ToUInt64(blob, pos + 8),
                DataOffset = pos + 16
            };
            pos = item.DataOffset + (int)item.Size;
            return item;
        }

        private static void SkipContainer(byte[] blob, ref int pos)
        {
            int depth = 1;
            while (pos < blob.Length && depth > 0)
            {
                var item = ReadItem(blob, ref pos);
                if (item.Type == Kcdata.TypeContainerBegin)
                    depth++;
                else if (item.Type == Kcdata.TypeContainerEnd)
                    depth--;
                else if (item.Type == Kcdata.TypeBufferEnd)
                    break;
            }
        }

        private static string ReadCString(byte[] data, int offset, uint maxLen)
        {
            int end = offset + (int)maxLen;
            if (end > data.Length) end = data.Length;

            int len = 0;
            for (int i = offset; i < end; i++)
            {
                if (data[i] == 0) break;
                len++;
            }
            return Encoding.UTF8.GetString(data, offset, len);
        }

        private static ulong[] ReadUInt64Array(byte[] data, int offset, uint count, uint dataSize)
        {
            uint actualCount = Math.Min(count, dataSize / 8);
            var arr = new ulong[actualCount];
            for (int i = 0; i < (int)actualCount; i++)
                arr[i] = BitConverter.ToUInt64(data, offset + i * 8);
            return arr;
        }

        private static ulong[] ReadUInt32AsUInt64Array(byte[] data, int offset, uint count, uint dataSize)
        {
            uint actualCount = Math.Min(count, dataSize / 4);
            var arr = new ulong[actualCount];
            for (int i = 0; i < (int)actualCount; i++)
                arr[i] = BitConverter.ToUInt32(data, offset + i * 4);
            return arr;
        }
    }
}
