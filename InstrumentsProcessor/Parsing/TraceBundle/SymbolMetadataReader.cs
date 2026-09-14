using Claunia.PropertyList;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace InstrumentsProcessor.Parsing.TraceBundle
{
    internal sealed record ImageSignature(uint ProcessId, IReadOnlyList<ImageLoad> Images, Guid? CacheUuid, ulong CacheBase);

    internal sealed class SymbolMetadataReader
    {
        private readonly NSArray objects;
        private readonly NSDictionary top;
        private readonly Action<string> diagnostic;
        private readonly CancellationToken cancellation;

        private SymbolMetadataReader(NSDictionary document, Action<string> diagnostic, CancellationToken cancellation)
        {
            objects = document.ObjectForKey("$objects") as NSArray ?? throw new InvalidDataException("Missing archive objects.");
            top = document.ObjectForKey("$top") as NSDictionary ?? throw new InvalidDataException("Missing archive root.");
            this.diagnostic = diagnostic;
            this.cancellation = cancellation;
        }

        public static RuntimeImageMap Load(string tracePath, Action<string> diagnostic, CancellationToken cancellation = default)
        {
            var result = new RuntimeImageMap();
            string path = Path.Combine(tracePath, "form.template");
            if (!File.Exists(path)) return result;
            try
            {
                using var stream = Decompressor.OpenDecompressedStream(path);
                using var buffer = new MemoryStream();
                var chunk = new byte[65536];
                int count;
                while ((count = stream.Read(chunk, 0, chunk.Length)) != 0)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (buffer.Length + count > 512L * 1024 * 1024) throw new InvalidDataException("Symbol metadata exceeds size limit.");
                    buffer.Write(chunk, 0, count);
                }
                var document = PropertyListParser.Parse(buffer.ToArray()) as NSDictionary;
                if (document == null) throw new InvalidDataException("Invalid saved document.");
                new SymbolMetadataReader(document, diagnostic, cancellation).Read(result);
            }
            catch (Exception error) when (IsDataError(error)) { diagnostic?.Invoke($"Symbol metadata: {error.Message}"); }
            return result;
        }

        private static bool IsDataError(Exception error) => error is not OperationCanceledException &&
            error is not OutOfMemoryException && error is not StackOverflowException;

        private NSObject Resolve(NSObject value)
        {
            if (value is not UID reference) return value;
            ulong index = reference.ToUInt64();
            if (index == 0) return null;
            if (index >= (ulong)objects.Count) throw new InvalidDataException("Invalid plist object reference.");
            return objects[(int)index];
        }

        private NSObject Field(NSObject value, string key) =>
            Resolve((Resolve(value) as NSDictionary)?.ObjectForKey(key));

        private IEnumerable<NSObject> Items(NSObject value)
        {
            value = Resolve(value);
            var array = value as NSArray ?? Field(value, "NS.objects") as NSArray;
            if (array == null)
            {
                if (value != null) throw new InvalidDataException("Unsupported plist array.");
                yield break;
            }
            foreach (var item in array) yield return Resolve(item);
        }

        private IEnumerable<(string Key, NSObject Value)> Pairs(NSObject value)
        {
            value = Resolve(value);
            if (value == null) yield break;
            if (value is not NSDictionary dictionary) throw new InvalidDataException("Unsupported plist dictionary.");
            if (!dictionary.ContainsKey("NS.keys"))
            {
                foreach (var key in dictionary.Keys) yield return (key, Resolve(dictionary.ObjectForKey(key)));
                yield break;
            }
            var keys = Field(dictionary, "NS.keys") as NSArray;
            var values = Field(dictionary, "NS.objects") as NSArray;
            if (keys == null || values == null || keys.Count != values.Count) throw new InvalidDataException("Invalid plist key/value arrays.");
            for (int index = 0; index < keys.Count; index++)
            {
                var key = Resolve(keys[index]);
                string text = Field(key, "NS.uuidbytes") is NSData uuid
                    ? SymbolArchive.ReadUuid(uuid.Bytes, 0).ToString() : key?.ToString();
                if (text == null) throw new InvalidDataException("Missing plist key.");
                yield return (text, Resolve(values[index]));
            }
        }

        private decimal Number(NSObject value) => decimal.Parse(Resolve(value)?.ToString()
            ?? throw new InvalidDataException("Missing clock value."), NumberStyles.Float, CultureInfo.InvariantCulture);

        private byte[] Data(NSObject value) => (Resolve(value) as NSData ?? Field(value, "NS.data") as NSData)?.Bytes
            ?? throw new InvalidDataException("Missing symbol signature data.");

        private void Read(RuntimeImageMap result)
        {
            var clocks = new Dictionary<(int Run, Guid Device), TraceClock>();
            foreach (var run in Pairs(Field(Field(top, "com.apple.xray.run.data"), "$1")))
            {
                if (!int.TryParse(run.Key, out int runNumber)) continue;
                try
                {
                    var info = Pairs(run.Value).ToDictionary(pair => pair.Key, pair => pair.Value);
                    decimal origin = Number(info["startTime"]);
                    foreach (var device in Pairs(info["mach_time_info"]))
                    {
                        var clock = Items(device.Value).ToArray();
                        if (clock.Length < 5) throw new InvalidDataException("Truncated Mach calibration.");
                        clocks[(runNumber, Guid.Parse(device.Key))] = new TraceClock(Number(clock[3]), Number(clock[1]),
                            Number(clock[2]), origin, Number(clock[4]) * 1000000000m);
                    }
                }
                catch (Exception error) when (IsDataError(error)) { diagnostic?.Invoke($"Run {runNumber} clock: {error.Message}"); }
            }

            var caches = new Dictionary<Guid, Dictionary<string, NSObject>>();
            foreach (var cache in Pairs(Field(top, "com.apple.xray.symbolstore.sharedcache.signatures")))
                caches[Guid.Parse(cache.Key)] = Pairs(cache.Value).ToDictionary(pair => pair.Key, pair => pair.Value);
            var decodedCacheImages = new Dictionary<(Guid Cache, string Key), ImageSignature>();

            foreach (var run in Pairs(Field(top, "com.apple.xray.symbolstoremanager.symbolstores")))
            {
                if (!int.TryParse(run.Key, out int runNumber)) continue;
                foreach (var process in Pairs(run.Value))
                {
                    cancellation.ThrowIfCancellationRequested();
                    try
                    {
                        int separator = process.Key.LastIndexOf('.');
                        if (separator < 0) throw new InvalidDataException("Invalid process symbol key.");
                        Guid device = Guid.Parse(process.Key.Substring(0, separator));
                        long pid = long.Parse(process.Key.Substring(separator + 1), CultureInfo.InvariantCulture);
                        if (Field(process.Value, "timeline-type")?.ToString() != "mach-absolute")
                            throw new InvalidDataException("Unsupported image timeline clock.");
                        if (Items(Field(process.Value, "$1")).Any())
                            throw new InvalidDataException("Unsupported supplemental symbol history; leaving process unresolved.");
                        var signature = ParseSignature(Data(Field(process.Value, "com.apple.xray.symbolstore.signature")));
                        if (pid == 0 && signature.ProcessId == uint.MaxValue) continue;
                        if (signature.ProcessId != pid) throw new InvalidDataException("Process signature identity mismatch.");
                        var images = signature.Images.ToList();
                        var cacheKeys = Items(Field(process.Value, "dsc_load_addresses")).ToArray();
                        if (cacheKeys.Length != 0)
                        {
                            if (!signature.CacheUuid.HasValue || !caches.TryGetValue(signature.CacheUuid.Value, out var cache))
                                throw new InvalidDataException("Missing matching shared-cache signature.");
                            foreach (var key in cacheKeys)
                            {
                                var cacheKey = (signature.CacheUuid.Value, key.ToString());
                                if (!decodedCacheImages.TryGetValue(cacheKey, out var cached))
                                {
                                    if (!cache.TryGetValue(cacheKey.Item2, out var cacheData)) throw new InvalidDataException("Missing shared-cache image.");
                                    decodedCacheImages[cacheKey] = cached = ParseSignature(Data(cacheData));
                                }
                                images.AddRange(cached.Images.Select(image => image.Rebase(signature.CacheBase)));
                            }
                        }
                        clocks.TryGetValue((runNumber, device), out var clock);
                        result.Add(runNumber, device, pid, false, new ImageScope(images, clock));
                    }
                    catch (Exception error) when (IsDataError(error)) { diagnostic?.Invoke($"Process {process.Key}: {error.Message}"); }
                }
            }

            foreach (var run in Pairs(Field(top, "com.apple.xray.symbolstore.kern.signatures")))
            {
                cancellation.ThrowIfCancellationRequested();
                if (!int.TryParse(run.Key, out int runNumber)) continue;
                try
                {
                    var devices = clocks.Keys.Where(key => key.Run == runNumber).ToArray();
                    if (devices.Length != 1) throw new InvalidDataException("Kernel device identity is ambiguous or missing.");
                    var signature = ParseSignature(Data(run.Value));
                    result.Add(runNumber, devices[0].Device, -1, true, new ImageScope(signature.Images, clocks[devices[0]]));
                }
                catch (Exception error) when (IsDataError(error)) { diagnostic?.Invoke($"Kernel run {runNumber}: {error.Message}"); }
            }
        }

        internal static ImageSignature ParseSignature(byte[] bytes)
        {
            if (bytes.Length < 24 || BinaryPrimitives.ReadUInt32LittleEndian(bytes) != 0xFF01FF02)
                throw new InvalidDataException("Unsupported image signature header.");
            uint Read32(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
            ulong Read64(int offset) => BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(offset, 8));
            if (Read32(4) != 1) throw new InvalidDataException("Unsupported image signature version.");
            uint count = Read32(20);
            if (count > 100000) throw new InvalidDataException("Excessive image count.");
            var images = new List<ImageLoad>();
            int position = 24;
            for (int index = 0; index < count; index++)
            {
                if ((long)position + 56 > bytes.Length) throw new InvalidDataException("Truncated image record.");
                uint segmentCount = Read32(position + 48), pathSize = Read32(position + 52);
                long next = position + 56L + pathSize + segmentCount * 32L;
                if (segmentCount > 1024 || next > bytes.Length) throw new InvalidDataException("Invalid image record bounds.");
                string path = Encoding.UTF8.GetString(bytes, position + 56, (int)pathSize).TrimEnd('\0');
                var segments = new List<SymbolSegment>();
                for (int segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
                {
                    int offset = position + 56 + (int)pathSize + segmentIndex * 32;
                    segments.Add(new SymbolSegment(Encoding.ASCII.GetString(bytes, offset, 16).TrimEnd('\0'), Read64(offset + 16), Read64(offset + 24)));
                }
                images.Add(new ImageLoad(SymbolArchive.ReadUuid(bytes, position), path, Read32(position + 40), Read32(position + 44),
                    Read64(position + 24), Read64(position + 32), segments));
                position = (int)next;
            }
            Guid? cacheUuid = null;
            ulong cacheBase = 0;
            if ((long)position + 32 <= bytes.Length && Read32(position) == 0x00C0FFEE)
            {
                if (Read32(position + 4) != 4) throw new InvalidDataException("Unsupported shared-cache signature version.");
                cacheUuid = SymbolArchive.ReadUuid(bytes, position + 8);
                cacheBase = Read64(position + 24);
            }
            return new ImageSignature(Read32(8), images, cacheUuid, cacheBase);
        }
    }
}