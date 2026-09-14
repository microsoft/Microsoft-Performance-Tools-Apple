// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
//
// Adapted from D:\pcwsnap_parser\PcwSnapReader.cs for the Apple performance plugin.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace InstrumentsProcessor.Parsing.PcwSnap
{
    // -----------------------------------------------------------------------
    // Constants
    // -----------------------------------------------------------------------

    public static class PcwSnapConstants
    {
        public const string Magic = "PCWSNAP";
        public const uint Version = 1;

        // File-level flags
        public const uint FlagLight    = 0x01;
        public const uint FlagCompress = 0x02;

        // Record types
        public const uint RecEnd          = 0x00;
        public const uint RecKcdata       = 0x01;
        public const uint RecTimestamp    = 0x02;
        public const uint RecSysInfo     = 0x03;
        public const uint RecProcInfo    = 0x04;
        public const uint RecFreqBaseline = 0x05;
        public const uint RecFreqDelta   = 0x06;
        public const uint RecPower       = 0x07;
        public const uint RecMarker      = 0x08;
        public const uint RecThreadInfo  = 0x09;

        // Record-level flags
        public const uint RecFlagCompressed = 0x01;
    }

    // -----------------------------------------------------------------------
    // File Header (32 bytes)
    // -----------------------------------------------------------------------

    public class PcwSnapHeader
    {
        public string Magic { get; set; } = "";
        public uint Version { get; set; }
        public uint Flags { get; set; }
        public ulong TimeUs { get; set; }
        public ulong Timestamp { get; set; }

        public bool IsLight => (Flags & PcwSnapConstants.FlagLight) != 0;
        public bool IsCompressed => (Flags & PcwSnapConstants.FlagCompress) != 0;

        public DateTimeOffset StartTime =>
            DateTimeOffset.FromUnixTimeMilliseconds((long)(TimeUs / 1000));
    }

    // -----------------------------------------------------------------------
    // Record Header (16 bytes)
    // -----------------------------------------------------------------------

    public class PcwRecordHeader
    {
        public uint Type { get; set; }
        public uint Flags { get; set; }
        public ulong Size { get; set; }

        public bool IsCompressed => (Flags & PcwSnapConstants.RecFlagCompressed) != 0;

        public static bool IsBoundary(uint type) =>
            type == PcwSnapConstants.RecEnd ||
            type == PcwSnapConstants.RecTimestamp ||
            type == PcwSnapConstants.RecKcdata;
    }

    // -----------------------------------------------------------------------
    // Per-process enrichment entry (388 bytes)
    // -----------------------------------------------------------------------

    public class ProcInfoEntry
    {
        public uint Pid { get; set; }

        // TASK_VM_INFO (13 × uint64)
        public ulong PhysFootprint { get; set; }
        public ulong Internal { get; set; }
        public ulong InternalPeak { get; set; }
        public ulong External { get; set; }
        public ulong ExternalPeak { get; set; }
        public ulong Compressed { get; set; }
        public ulong CompressedPeak { get; set; }
        public ulong CompressedLifetime { get; set; }
        public ulong Reusable { get; set; }
        public ulong PurgeableVolRes { get; set; }
        public ulong Resident { get; set; }
        public ulong ResidentPeak { get; set; }
        public ulong Virtual { get; set; }

        // TASK_VM_INFO extras (2 × uint32)
        public uint Decompressions { get; set; }
        public uint RegionCount { get; set; }

        // BSD CPU times (2 × float64)
        public double SystemCpuSec { get; set; }
        public double UserCpuSec { get; set; }

        // Rusage core (8 × uint64)
        public ulong Instructions { get; set; }
        public ulong Cycles { get; set; }
        public ulong PInstructions { get; set; }
        public ulong PCycles { get; set; }
        public ulong EnergyNj { get; set; }
        public ulong PEnergyNj { get; set; }
        public ulong BilledEnergy { get; set; }
        public ulong ServicedEnergy { get; set; }

        // BSD extended (5 × uint64)
        public ulong StartSec { get; set; }
        public ulong StartUsec { get; set; }
        public ulong ContextSwitches { get; set; }
        public ulong MsgSent { get; set; }
        public ulong MsgRecv { get; set; }

        // Rusage extended (13 × uint64)
        public ulong RunnableTime { get; set; }
        public ulong UserPTime { get; set; }
        public ulong SystemPTime { get; set; }
        public ulong PkgIdleWakeups { get; set; }
        public ulong InterruptWakeups { get; set; }
        public ulong QosDefault { get; set; }
        public ulong QosMaintenance { get; set; }
        public ulong QosBackground { get; set; }
        public ulong QosUtility { get; set; }
        public ulong QosUserInitiated { get; set; }
        public ulong QosUserInteractive { get; set; }
        public ulong LogicalWrites { get; set; }
        public ulong LifetimeMaxPhysFootprint { get; set; }

        // BSD syscalls (1 × uint64)
        public ulong Syscalls { get; set; }

        // Thread counts (2 × uint32)
        public uint Threads { get; set; }
        public uint ThreadsRunning { get; set; }

        // Graphics footprint (2 × uint64)
        public ulong GraphicsFootprint { get; set; }
        public ulong GraphicsFootprintCompressed { get; set; }

        // Purgeable nonvolatile (2 × uint64)
        public ulong PurgeableNonvolatile { get; set; }
        public ulong PurgeableNonvolatileCompressed { get; set; }
    }

    // -----------------------------------------------------------------------
    // PROCINFO record (header + entries)
    // -----------------------------------------------------------------------

    public class ProcInfoRecord
    {
        public uint EntryCount { get; set; }
        public uint EntrySize { get; set; }
        public ulong Timestamp { get; set; }
        public List<ProcInfoEntry> Entries { get; set; } = new List<ProcInfoEntry>();
    }

    // -----------------------------------------------------------------------
    // Per-thread enrichment entry
    // -----------------------------------------------------------------------

    public class ThreadInfoEntry
    {
        public ulong Tid { get; set; }
        public double UserCpuSec { get; set; }
        public double SystemCpuSec { get; set; }
        public uint CpuUsage { get; set; }
        public uint RunState { get; set; }
        public uint Flags { get; set; }
        public uint SleepTime { get; set; }
        public uint Priority { get; set; }
        public uint BasePriority { get; set; }
        public uint MaxPriority { get; set; }
        public uint QosClass { get; set; }
        public uint LatencyQos { get; set; }
        public uint ThroughputQos { get; set; }
        public string Name { get; set; } = "";
    }

    public class ThreadInfoProcess
    {
        public uint Pid { get; set; }
        public List<ThreadInfoEntry> Threads { get; set; } = new List<ThreadInfoEntry>();
    }

    public class ThreadInfoRecord
    {
        public uint ProcessCount { get; set; }
        public uint EntrySize { get; set; }
        public ulong Timestamp { get; set; }
        public List<ThreadInfoProcess> Processes { get; set; } = new List<ThreadInfoProcess>();
    }

    // -----------------------------------------------------------------------
    // Frequency channel data
    // -----------------------------------------------------------------------

    public class FreqChannel
    {
        public uint FreqCount { get; set; }
        public double ActivePct { get; set; }
        public double AvgFreqMhz { get; set; }
        public double[] ResidencyPct { get; set; } = Array.Empty<double>();
    }

    public class FreqRecord
    {
        public uint Version { get; set; }
        public uint ChannelCount { get; set; }
        public ulong IntervalNs { get; set; }
        public List<FreqChannel> Channels { get; set; } = new List<FreqChannel>();
    }

    // -----------------------------------------------------------------------
    // A single snapshot (one iteration of the trace)
    // -----------------------------------------------------------------------

    public class Snapshot
    {
        public ulong TimestampNs { get; set; }
        public byte[] KcdataBlob { get; set; }
        public ProcInfoRecord ProcInfo { get; set; }
        public ThreadInfoRecord ThreadInfo { get; set; }
        public FreqRecord FreqDelta { get; set; }
        public JsonDocument Power { get; set; }
        public JsonDocument Marker { get; set; }
    }

    // -----------------------------------------------------------------------
    // Top-level parse result
    // -----------------------------------------------------------------------

    public class PcwSnapFile
    {
        public PcwSnapHeader Header { get; set; } = new PcwSnapHeader();
        public JsonDocument SysInfo { get; set; }
        public FreqRecord FreqBaseline { get; set; }
        public List<Snapshot> Snapshots { get; set; } = new List<Snapshot>();
    }

    // -----------------------------------------------------------------------
    // Reader
    // -----------------------------------------------------------------------

    public static class PcwSnapReader
    {
        public static PcwSnapFile Parse(string path)
        {
            using var fs = File.OpenRead(path);
            return Parse(fs);
        }

        public static PcwSnapFile Parse(Stream stream)
        {
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            var file = new PcwSnapFile();

            // --- Header (32 bytes) ---
            file.Header = ReadHeader(reader);

            // --- File-level metadata records ---
            while (true)
            {
                var rec = ReadRecordHeader(reader);

                if (rec.Type == PcwSnapConstants.RecSysInfo)
                {
                    var payload = ReadPayload(reader, rec);
                    var json = Encoding.UTF8.GetString(payload);
                    file.SysInfo = JsonDocument.Parse(json);
                }
                else if (rec.Type == PcwSnapConstants.RecFreqBaseline)
                {
                    var payload = ReadPayload(reader, rec);
                    file.FreqBaseline = ParseFreqRecord(payload);
                }
                else if (PcwRecordHeader.IsBoundary(rec.Type))
                {
                    // Rewind the 16-byte record header
                    stream.Seek(-16, SeekOrigin.Current);
                    break;
                }
                else
                {
                    // Unknown file-level record — skip
                    SkipPayload(reader, rec);
                }
            }

            // --- Snapshot records ---
            while (true)
            {
                var rec = ReadRecordHeader(reader);

                if (rec.Type == PcwSnapConstants.RecEnd)
                    break;

                if (rec.Type == PcwSnapConstants.RecTimestamp)
                {
                    var snap = new Snapshot();
                    var tsPayload = ReadPayload(reader, rec);
                    snap.TimestampNs = BitConverter.ToUInt64(tsPayload, 0);

                    // Expect KCDATA next
                    var kcrec = ReadRecordHeader(reader);
                    if (kcrec.Type == PcwSnapConstants.RecKcdata)
                    {
                        snap.KcdataBlob = ReadPayload(reader, kcrec);
                    }
                    else
                    {
                        // Unexpected — skip payload and continue
                        SkipPayload(reader, kcrec);
                    }

                    // Consume sub-records until next boundary
                    while (true)
                    {
                        if (stream.Position >= stream.Length)
                            break;

                        var sub = ReadRecordHeader(reader);

                        if (PcwRecordHeader.IsBoundary(sub.Type))
                        {
                            stream.Seek(-16, SeekOrigin.Current);
                            break;
                        }

                        var payload = ReadPayload(reader, sub);

                        switch (sub.Type)
                        {
                            case PcwSnapConstants.RecProcInfo:
                                snap.ProcInfo = ParseProcInfoRecord(payload);
                                break;
                            case PcwSnapConstants.RecThreadInfo:
                                snap.ThreadInfo = ParseThreadInfoRecord(payload);
                                break;
                            case PcwSnapConstants.RecFreqDelta:
                                snap.FreqDelta = ParseFreqRecord(payload);
                                break;
                            case PcwSnapConstants.RecPower:
                                snap.Power = JsonDocument.Parse(
                                    Encoding.UTF8.GetString(payload));
                                break;
                            case PcwSnapConstants.RecMarker:
                                snap.Marker = JsonDocument.Parse(
                                    Encoding.UTF8.GetString(payload));
                                break;
                            default:
                                // Unknown sub-record — skip
                                break;
                        }
                    }

                    file.Snapshots.Add(snap);
                }
                else
                {
                    // Unexpected top-level record — skip
                    SkipPayload(reader, rec);
                }
            }

            return file;
        }

        // -------------------------------------------------------------------
        // Header
        // -------------------------------------------------------------------

        private static PcwSnapHeader ReadHeader(BinaryReader r)
        {
            var magicBytes = r.ReadBytes(8);
            var magic = Encoding.ASCII.GetString(magicBytes, 0, 7);

            if (magic != PcwSnapConstants.Magic)
                throw new InvalidDataException(
                    $"Bad magic: expected '{PcwSnapConstants.Magic}', got '{magic}'");

            var version = r.ReadUInt32();
            if (version > PcwSnapConstants.Version)
                throw new InvalidDataException(
                    $"Unsupported version: {version} (max {PcwSnapConstants.Version})");

            return new PcwSnapHeader
            {
                Magic = magic,
                Version = version,
                Flags = r.ReadUInt32(),
                TimeUs = r.ReadUInt64(),
                Timestamp = r.ReadUInt64()
            };
        }

        // -------------------------------------------------------------------
        // Record header
        // -------------------------------------------------------------------

        private static PcwRecordHeader ReadRecordHeader(BinaryReader r)
        {
            return new PcwRecordHeader
            {
                Type = r.ReadUInt32(),
                Flags = r.ReadUInt32(),
                Size = r.ReadUInt64()
            };
        }

        // -------------------------------------------------------------------
        // Payload (with optional zlib decompression)
        // -------------------------------------------------------------------

        private static byte[] ReadPayload(BinaryReader r, PcwRecordHeader rec)
        {
            if (rec.Size == 0)
                return Array.Empty<byte>();

            var raw = r.ReadBytes((int)rec.Size);

            if (!rec.IsCompressed)
                return raw;

            return ZlibDecompress(raw);
        }

        private static void SkipPayload(BinaryReader r, PcwRecordHeader rec)
        {
            if (rec.Size > 0)
                r.BaseStream.Seek((long)rec.Size, SeekOrigin.Current);
        }

        // -------------------------------------------------------------------
        // Zlib decompression (raw deflate wrapped in zlib header)
        // -------------------------------------------------------------------

        private static byte[] ZlibDecompress(byte[] compressed)
        {
            // zlib format: 2-byte header, deflate stream, 4-byte checksum
            // Skip the 2-byte zlib header, DeflateStream handles the rest
            using var input = new MemoryStream(compressed, 2, compressed.Length - 2);
            using var deflate = new DeflateStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            deflate.CopyTo(output);
            return output.ToArray();
        }

        // -------------------------------------------------------------------
        // PROCINFO binary parser
        // -------------------------------------------------------------------

        private static ProcInfoRecord ParseProcInfoRecord(byte[] data)
        {
            var result = new ProcInfoRecord();
            int pos = 0;

            result.EntryCount = BitConverter.ToUInt32(data, pos); pos += 4;
            result.EntrySize = BitConverter.ToUInt32(data, pos); pos += 4;
            result.Timestamp = BitConverter.ToUInt64(data, pos); pos += 8;

            int entrySize = (int)result.EntrySize;

            for (int i = 0; i < result.EntryCount; i++)
            {
                int entryStart = pos;
                var e = new ProcInfoEntry();
                int off = 0;

                e.Pid = ReadU32(data, entryStart, ref off, entrySize);

                // TASK_VM_INFO (13 × uint64)
                e.PhysFootprint = ReadU64(data, entryStart, ref off, entrySize);
                e.Internal = ReadU64(data, entryStart, ref off, entrySize);
                e.InternalPeak = ReadU64(data, entryStart, ref off, entrySize);
                e.External = ReadU64(data, entryStart, ref off, entrySize);
                e.ExternalPeak = ReadU64(data, entryStart, ref off, entrySize);
                e.Compressed = ReadU64(data, entryStart, ref off, entrySize);
                e.CompressedPeak = ReadU64(data, entryStart, ref off, entrySize);
                e.CompressedLifetime = ReadU64(data, entryStart, ref off, entrySize);
                e.Reusable = ReadU64(data, entryStart, ref off, entrySize);
                e.PurgeableVolRes = ReadU64(data, entryStart, ref off, entrySize);
                e.Resident = ReadU64(data, entryStart, ref off, entrySize);
                e.ResidentPeak = ReadU64(data, entryStart, ref off, entrySize);
                e.Virtual = ReadU64(data, entryStart, ref off, entrySize);

                // TASK_VM_INFO extras (2 × uint32)
                e.Decompressions = ReadU32(data, entryStart, ref off, entrySize);
                e.RegionCount = ReadU32(data, entryStart, ref off, entrySize);

                // BSD CPU times (2 × float64)
                e.SystemCpuSec = ReadF64(data, entryStart, ref off, entrySize);
                e.UserCpuSec = ReadF64(data, entryStart, ref off, entrySize);

                // Rusage core (8 × uint64)
                e.Instructions = ReadU64(data, entryStart, ref off, entrySize);
                e.Cycles = ReadU64(data, entryStart, ref off, entrySize);
                e.PInstructions = ReadU64(data, entryStart, ref off, entrySize);
                e.PCycles = ReadU64(data, entryStart, ref off, entrySize);
                e.EnergyNj = ReadU64(data, entryStart, ref off, entrySize);
                e.PEnergyNj = ReadU64(data, entryStart, ref off, entrySize);
                e.BilledEnergy = ReadU64(data, entryStart, ref off, entrySize);
                e.ServicedEnergy = ReadU64(data, entryStart, ref off, entrySize);

                // BSD extended (5 × uint64)
                e.StartSec = ReadU64(data, entryStart, ref off, entrySize);
                e.StartUsec = ReadU64(data, entryStart, ref off, entrySize);
                e.ContextSwitches = ReadU64(data, entryStart, ref off, entrySize);
                e.MsgSent = ReadU64(data, entryStart, ref off, entrySize);
                e.MsgRecv = ReadU64(data, entryStart, ref off, entrySize);

                // Rusage extended (13 × uint64)
                e.RunnableTime = ReadU64(data, entryStart, ref off, entrySize);
                e.UserPTime = ReadU64(data, entryStart, ref off, entrySize);
                e.SystemPTime = ReadU64(data, entryStart, ref off, entrySize);
                e.PkgIdleWakeups = ReadU64(data, entryStart, ref off, entrySize);
                e.InterruptWakeups = ReadU64(data, entryStart, ref off, entrySize);
                e.QosDefault = ReadU64(data, entryStart, ref off, entrySize);
                e.QosMaintenance = ReadU64(data, entryStart, ref off, entrySize);
                e.QosBackground = ReadU64(data, entryStart, ref off, entrySize);
                e.QosUtility = ReadU64(data, entryStart, ref off, entrySize);
                e.QosUserInitiated = ReadU64(data, entryStart, ref off, entrySize);
                e.QosUserInteractive = ReadU64(data, entryStart, ref off, entrySize);
                e.LogicalWrites = ReadU64(data, entryStart, ref off, entrySize);
                e.LifetimeMaxPhysFootprint = ReadU64(data, entryStart, ref off, entrySize);

                // BSD syscalls (1 × uint64)
                e.Syscalls = ReadU64(data, entryStart, ref off, entrySize);

                // Thread counts (2 × uint32)
                e.Threads = ReadU32(data, entryStart, ref off, entrySize);
                e.ThreadsRunning = ReadU32(data, entryStart, ref off, entrySize);

                // Graphics footprint (2 × uint64)
                e.GraphicsFootprint = ReadU64(data, entryStart, ref off, entrySize);
                e.GraphicsFootprintCompressed = ReadU64(data, entryStart, ref off, entrySize);

                // Purgeable nonvolatile (2 × uint64)
                e.PurgeableNonvolatile = ReadU64(data, entryStart, ref off, entrySize);
                e.PurgeableNonvolatileCompressed = ReadU64(data, entryStart, ref off, entrySize);

                result.Entries.Add(e);
                pos = entryStart + entrySize;
            }

            return result;
        }

        // Forward-compatible field readers: return 0 if entry is too short
        private static uint ReadU32(byte[] data, int entryStart, ref int off, int entrySize)
        {
            if (off + 4 > entrySize) return 0;
            var val = BitConverter.ToUInt32(data, entryStart + off);
            off += 4;
            return val;
        }

        private static ulong ReadU64(byte[] data, int entryStart, ref int off, int entrySize)
        {
            if (off + 8 > entrySize) return 0;
            var val = BitConverter.ToUInt64(data, entryStart + off);
            off += 8;
            return val;
        }

        private static double ReadF64(byte[] data, int entryStart, ref int off, int entrySize)
        {
            if (off + 8 > entrySize) return 0;
            var val = BitConverter.ToDouble(data, entryStart + off);
            off += 8;
            return val;
        }

        // -------------------------------------------------------------------
        // THREADINFO binary parser
        // -------------------------------------------------------------------

        private static ThreadInfoRecord ParseThreadInfoRecord(byte[] data)
        {
            var result = new ThreadInfoRecord();
            int pos = 0;

            result.ProcessCount = BitConverter.ToUInt32(data, pos); pos += 4;
            result.EntrySize = BitConverter.ToUInt32(data, pos); pos += 4;
            result.Timestamp = BitConverter.ToUInt64(data, pos); pos += 8;

            for (int p = 0; p < result.ProcessCount; p++)
            {
                var proc = new ThreadInfoProcess();
                proc.Pid = BitConverter.ToUInt32(data, pos); pos += 4;
                uint threadCount = BitConverter.ToUInt32(data, pos); pos += 4;

                for (int t = 0; t < threadCount; t++)
                {
                    var entry = new ThreadInfoEntry();
                    entry.Tid = BitConverter.ToUInt64(data, pos); pos += 8;
                    entry.UserCpuSec = BitConverter.ToDouble(data, pos); pos += 8;
                    entry.SystemCpuSec = BitConverter.ToDouble(data, pos); pos += 8;
                    entry.CpuUsage = BitConverter.ToUInt32(data, pos); pos += 4;
                    entry.RunState = BitConverter.ToUInt32(data, pos); pos += 4;
                    entry.Flags = BitConverter.ToUInt32(data, pos); pos += 4;
                    entry.SleepTime = BitConverter.ToUInt32(data, pos); pos += 4;
                    entry.Priority = BitConverter.ToUInt32(data, pos); pos += 4;
                    entry.BasePriority = BitConverter.ToUInt32(data, pos); pos += 4;
                    entry.MaxPriority = BitConverter.ToUInt32(data, pos); pos += 4;
                    entry.QosClass = BitConverter.ToUInt32(data, pos); pos += 4;
                    entry.LatencyQos = BitConverter.ToUInt32(data, pos); pos += 4;
                    entry.ThroughputQos = BitConverter.ToUInt32(data, pos); pos += 4;

                    // 64-byte null-padded name
                    int nameLen = 0;
                    for (int i = 0; i < 64 && pos + i < data.Length; i++)
                    {
                        if (data[pos + i] == 0) break;
                        nameLen++;
                    }
                    entry.Name = Encoding.UTF8.GetString(data, pos, nameLen);
                    pos += 64;

                    proc.Threads.Add(entry);
                }

                result.Processes.Add(proc);
            }

            return result;
        }

        // -------------------------------------------------------------------
        // Freq record binary parser
        // -------------------------------------------------------------------

        private static FreqRecord ParseFreqRecord(byte[] data)
        {
            int pos = 0;
            var rec = new FreqRecord
            {
                Version = BitConverter.ToUInt32(data, pos),
                ChannelCount = BitConverter.ToUInt32(data, pos + 4),
                IntervalNs = BitConverter.ToUInt64(data, pos + 8)
            };
            pos += 16;

            for (int ch = 0; ch < rec.ChannelCount; ch++)
            {
                var channel = new FreqChannel();
                channel.FreqCount = BitConverter.ToUInt32(data, pos); pos += 4;
                pos += 4; // padding
                channel.ActivePct = BitConverter.ToDouble(data, pos); pos += 8;
                channel.AvgFreqMhz = BitConverter.ToDouble(data, pos); pos += 8;

                channel.ResidencyPct = new double[channel.FreqCount];
                for (int f = 0; f < channel.FreqCount; f++)
                {
                    channel.ResidencyPct[f] = BitConverter.ToDouble(data, pos);
                    pos += 8;
                }

                rec.Channels.Add(channel);
            }

            return rec;
        }
    }
}
