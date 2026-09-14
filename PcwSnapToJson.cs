using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using InstrumentsProcessor.Parsing.PcwSnap;

var file = PcwSnapReader.Parse(@"D:\my_trace.pcwsnap");

var options = new JsonSerializerOptions 
{ 
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
};

var output = new
{
    Header = new
    {
        file.Header.Magic,
        file.Header.Version,
        file.Header.Flags,
        file.Header.ProcInfoCount,
        file.Header.SnapshotCount,
        file.Header.FreqChannelCount,
    },
    ProcInfos = file.ProcInfos.ConvertAll(p => new
    {
        p.Pid,
        p.ProcessName
    }),
    FreqChannels = file.FreqChannels.ConvertAll(f => new
    {
        f.Name,
        f.Unit
    }),
    Snapshots = file.Snapshots.ConvertAll((snap) =>
    {
        KcdataDecoded decoded = null;
        try { decoded = KcdataDecoder.Decode(snap.KcdataBlob); } catch { }

        return new
        {
            snap.Index,
            snap.WallTimeUs,
            KcdataSizeBytes = snap.KcdataBlob?.Length ?? 0,
            HasPowerData = snap.PowerData != null,
            HasMarkerData = snap.MarkerData != null,
            FreqRecordCount = snap.FreqRecords?.Count ?? 0,
            Tasks = decoded?.Tasks?.ConvertAll(t => new
            {
                t.Pid,
                t.ProcessName,
                t.UniquePid,
                t.Instructions,
                t.Cycles,
                t.TaskSize,
                t.MaxResidentSize,
                t.Faults,
                t.Pageins,
                t.SuspendCount,
                t.LatencyQos,
                Threads = t.Threads.ConvertAll(th => new
                {
                    th.ThreadId,
                    th.Name,
                    th.DispatchQueueLabel,
                    th.Instructions,
                    th.Cycles,
                    th.UserTime,
                    th.SystemTime,
                    th.State,
                    th.BasePriority,
                    th.SchedPriority,
                    th.Eqos,
                    th.Rqos,
                    th.RqosOverride,
                    KernelStackDepth = th.KernelStack?.Count ?? 0,
                    UserStackDepth = th.UserStack?.Count ?? 0,
                })
            })
        };
    })
};

var json = JsonSerializer.Serialize(output, options);
File.WriteAllText(@"D:\my_trace.json", json);
Console.WriteLine($"Written {json.Length} bytes to D:\\my_trace.json");
Console.WriteLine($"Header: v{file.Header.Version}, {file.Header.SnapshotCount} snapshots, {file.Header.ProcInfoCount} procs");
