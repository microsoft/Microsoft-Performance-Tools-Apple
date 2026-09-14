using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using InstrumentsProcessor.Parsing.PcwSnap;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.Error.WriteLine("Usage: DumpThreadInfo <path.pcwsnap> [output.json]");
            return;
        }

        string inputPath = args[0];
        string outputPath = args.Length >= 2 ? args[1] : Path.ChangeExtension(inputPath, ".threadinfo.json");

        var file = PcwSnapReader.Parse(inputPath);

        var output = new System.Collections.Generic.List<object>();

        for (int i = 0; i < file.Snapshots.Count; i++)
        {
            var snap = file.Snapshots[i];
            if (snap.ThreadInfo == null) continue;

            var ti = snap.ThreadInfo;
            var processes = new System.Collections.Generic.List<object>();

            foreach (var proc in ti.Processes)
            {
                var threads = new System.Collections.Generic.List<object>();
                foreach (var t in proc.Threads)
                {
                    threads.Add(new
                    {
                        tid = t.Tid,
                        name = t.Name,
                        userCpuSec = t.UserCpuSec,
                        systemCpuSec = t.SystemCpuSec,
                        cpuUsage = t.CpuUsage,
                        runState = t.RunState,
                        flags = t.Flags,
                        sleepTime = t.SleepTime,
                        priority = t.Priority,
                        basePriority = t.BasePriority,
                        maxPriority = t.MaxPriority,
                        qosClass = t.QosClass,
                        latencyQos = t.LatencyQos,
                        throughputQos = t.ThroughputQos
                    });
                }

                processes.Add(new
                {
                    pid = proc.Pid,
                    threadCount = proc.Threads.Count,
                    threads
                });
            }

            output.Add(new
            {
                snapshotIndex = i,
                timestampNs = snap.TimestampNs,
                threadInfoTimestamp = ti.Timestamp,
                processCount = ti.ProcessCount,
                entrySize = ti.EntrySize,
                processes
            });
        }

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };

        string json = JsonSerializer.Serialize(output, options);
        File.WriteAllText(outputPath, json);
        Console.WriteLine($"Wrote {output.Count} snapshots with ThreadInfo to: {outputPath}");
    }
}
