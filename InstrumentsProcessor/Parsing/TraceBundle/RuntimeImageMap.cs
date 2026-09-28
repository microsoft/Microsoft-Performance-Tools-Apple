using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace InstrumentsProcessor.Parsing.TraceBundle
{
    internal sealed record SymbolContext(int Run, long ProcessId, long Timestamp, bool Kernel = false, Guid? Device = null);

    internal sealed class TraceClock
    {
        private readonly decimal syncTicks, numerator, denominator, startNanoseconds, syncNanoseconds;

        public TraceClock(decimal syncTicks, decimal numerator, decimal denominator,
            decimal startNanoseconds, decimal syncNanoseconds)
        {
            if (numerator <= 0 || denominator <= 0) throw new InvalidDataException("Invalid Mach timebase.");
            this.syncTicks = syncTicks;
            this.numerator = numerator;
            this.denominator = denominator;
            this.startNanoseconds = startNanoseconds;
            this.syncNanoseconds = syncNanoseconds;
        }

        public decimal ToMachTime(long timestamp) =>
            syncTicks + (timestamp + startNanoseconds - syncNanoseconds) * denominator / numerator;
    }

    internal sealed class ImageLoad
    {
        public Guid Uuid { get; }
        public string Path { get; }
        public string Name => Path.Replace('\\', '/').Split('/').Last();
        public uint CpuType { get; }
        public uint CpuSubtype { get; }
        public ulong Start { get; }
        public ulong End { get; }
        public IReadOnlyList<SymbolSegment> Segments { get; }

        public ImageLoad(Guid uuid, string path, uint cpuType, uint cpuSubtype,
            ulong start, ulong end, IEnumerable<SymbolSegment> segments)
        {
            if (end <= start) throw new InvalidDataException("Invalid image load lifetime.");
            Uuid = uuid;
            Path = path ?? uuid.ToString();
            CpuType = cpuType;
            CpuSubtype = cpuSubtype;
            Start = start;
            End = end;
            Segments = segments.ToArray();
            foreach (var segment in Segments)
                if (segment.Size > ulong.MaxValue - segment.Address) throw new InvalidDataException("Image segment overflow.");
        }

        public bool Active(decimal? time) =>
            (Start == 0 || (time.HasValue && time >= Start)) &&
            (End == long.MaxValue || (time.HasValue && time < End));

        public ImageLoad Rebase(ulong baseAddress) => new ImageLoad(Uuid, Path, CpuType, CpuSubtype, Start, End,
            Segments.Select(segment => new SymbolSegment(segment.Name, checked(segment.Address + baseAddress), segment.Size)));
    }

    internal sealed class ImageScope
    {
        public IReadOnlyList<ImageLoad> Images { get; }
        public TraceClock Clock { get; }
        private readonly ulong[] boundaries;
        private readonly (ImageLoad Image, SymbolSegment Segment)[] ranges;
        private readonly ulong[] prefixEnds;

        public ImageScope(IEnumerable<ImageLoad> images, TraceClock clock)
        {
            Images = images.ToArray();
            Clock = clock;
            boundaries = Images.SelectMany(image => new[] { image.Start, image.End })
                .Where(time => time != 0 && time != long.MaxValue).Distinct().OrderBy(time => time).ToArray();
            ranges = Images.SelectMany(image => image.Segments.Where(segment => segment.Name != "__PAGEZERO" && segment.Size != 0)
                .Select(segment => (Image: image, Segment: segment))).OrderBy(range => range.Segment.Address).ToArray();
            prefixEnds = new ulong[ranges.Length];
            ulong end = 0;
            for (int index = 0; index < ranges.Length; index++)
            {
                end = Math.Max(end, ranges[index].Segment.Address + ranges[index].Segment.Size);
                prefixEnds[index] = end;
            }
        }

        public decimal? GetTime(long timestamp)
        {
            try { return Clock?.ToMachTime(timestamp); }
            catch (OverflowException) { return null; }
        }

        public int GetGeneration(long timestamp)
        {
            decimal? time = GetTime(timestamp);
            if (!time.HasValue) return -1;
            int low = 0, high = boundaries.Length;
            while (low < high)
            {
                int middle = low + (high - low) / 2;
                if (boundaries[middle] <= time.Value) low = middle + 1;
                else high = middle;
            }
            return low;
        }

        public (ImageLoad Image, SymbolSegment Segment, bool Ambiguous) Find(ulong address, long timestamp)
        {
            decimal? time = GetTime(timestamp);
            int low = 0, high = ranges.Length;
            while (low < high)
            {
                int middle = low + (high - low) / 2;
                if (ranges[middle].Segment.Address <= address) low = middle + 1;
                else high = middle;
            }
            ImageLoad found = null;
            SymbolSegment foundSegment = null;
            for (int index = low - 1; index >= 0 && prefixEnds[index] > address; index--)
            {
                var candidate = ranges[index];
                if (!candidate.Segment.Contains(address) || !candidate.Image.Active(time)) continue;
                if (found != null && (found.Uuid != candidate.Image.Uuid || found.CpuType != candidate.Image.CpuType ||
                    found.CpuSubtype != candidate.Image.CpuSubtype || foundSegment != candidate.Segment ||
                    found.Start != candidate.Image.Start || found.End != candidate.Image.End)) return (null, null, true);
                found = candidate.Image;
                foundSegment = candidate.Segment;
            }
            return (found, foundSegment, false);
        }
    }

    internal sealed class RuntimeImageMap
    {
        private readonly Dictionary<(int Run, long Pid, bool Kernel), List<(Guid Device, ImageScope Scope)>> scopes = new();
        public IEnumerable<ImageLoad> Images => scopes.Values.SelectMany(entries => entries).SelectMany(entry => entry.Scope.Images);

        public void Add(int run, Guid device, long pid, bool kernel, ImageScope scope)
        {
            var key = (run, kernel ? -1 : pid, kernel);
            if (!scopes.TryGetValue(key, out var entries)) scopes[key] = entries = new List<(Guid, ImageScope)>();
            entries.Add((device, scope));
        }

        public ImageScope GetScope(SymbolContext context)
        {
            if (context == null || !scopes.TryGetValue((context.Run, context.Kernel ? -1 : context.ProcessId, context.Kernel), out var entries)) return null;
            ImageScope result = null;
            foreach (var entry in entries)
            {
                if (context.Device.HasValue && context.Device.Value != entry.Device) continue;
                if (result != null) return null;
                result = entry.Scope;
            }
            return result;
        }
    }
}