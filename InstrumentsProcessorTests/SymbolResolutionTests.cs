using InstrumentsProcessor.Parsing.TraceBundle;
using Claunia.PropertyList;
using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace InstrumentsProcessorTests
{
    public class SymbolResolutionTests
    {
        private static readonly Guid ImageUuid = Guid.Parse("F071EFE4-299F-3089-ACC4-0025B8FFB52A");

        [Theory]
        [InlineData(4, false)]
        [InlineData(4, true)]
        [InlineData(5, false)]
        [InlineData(5, true)]
        [InlineData(8, false)]
        [InlineData(8, true)]
        public void CoreProfileAcceptsShortWrappersAndAbsentProcess(int length, bool absentProcess)
        {
            string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(Path.Combine(directory, "arrayUniquer"));
            try
            {
                using (var writer = new BinaryWriter(File.Create(Path.Combine(directory, "arrayUniquer", "integeruniquer.data"))))
                {
                    writer.Write(new byte[32]);
                    writer.Write(1U);
                    writer.Write(0x18EDECC08UL);
                    writer.Write((uint)length);
                    writer.Write(0UL);
                    writer.Write(0x18EDECC08UL);
                    writer.Write(absentProcess ? (ulong)uint.MaxValue : 2UL);
                    writer.Write(0UL);
                    if (length >= 5) writer.Write(0x18EDECC08UL);
                    if (length == 8)
                    {
                        writer.Write(4UL);
                        writer.Write(11UL);
                        writer.Write(1UL);
                    }
                    writer.Write(2U);
                    writer.Write(42UL);
                    writer.Write(0UL);
                }
                var decoded = new Uniquing(directory).DecodeBacktrace(1, "XRCoreProfileCallstackTypeID");
                Assert.Equal(0x18EDECC08UL, Assert.Single(decoded.Addresses));
                Assert.Equal(absentProcess ? (long?)null : 42L, decoded.ProcessId);
            }
            finally { Directory.Delete(directory, true); }
        }

        [Fact]
        public void TaggedBacktraceExpandsReferencedFrameArrays()
        {
            string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(Path.Combine(directory, "arrayUniquer"));
            try
            {
                using (var writer = new BinaryWriter(File.Create(Path.Combine(directory, "arrayUniquer", "integeruniquer.data"))))
                {
                    writer.Write(new byte[32]);
                    foreach (ulong[] entry in new[] {
                        new[] { 0x1000UL, 0x2000UL },
                        new[] { 0x3000UL },
                        new[] { 0x608C00000000UL, 0x608C00000001UL, 0x4000UL },
                        new[] { 2UL, 0UL }
                    })
                    {
                        writer.Write((uint)entry.Length);
                        foreach (ulong value in entry) writer.Write(value);
                    }
                }

                var decoded = new Uniquing(directory).DecodeBacktrace(3, "XRTaggedBacktraceTypeID");
                Assert.Equal(new[] { 0x1000UL, 0x2000UL, 0x3000UL, 0x4000UL }, decoded.Addresses);
            }
            finally { Directory.Delete(directory, true); }
        }

        [Fact]
        public void UniquerIndexSkipsBlockPaddingWithoutShiftingReferences()
        {
            string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            string arrayPath = Path.Combine(directory, "arrayUniquer");
            Directory.CreateDirectory(arrayPath);
            try
            {
                var data = new byte[128];
                BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(32), 1);
                BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(36), 0x18EDECC08);
                BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(64), 4);
                BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(84), 2);
                BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(100), 2);
                BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(104), 813);
                File.WriteAllBytes(Path.Combine(arrayPath, "integeruniquer.data"), data);
                var index = new byte[64];
                BinaryPrimitives.WriteUInt32LittleEndian(index, 0x01234567);
                BinaryPrimitives.WriteUInt32LittleEndian(index.AsSpan(28), 64);
                BinaryPrimitives.WriteUInt64LittleEndian(index.AsSpan(40), 32);
                BinaryPrimitives.WriteUInt64LittleEndian(index.AsSpan(48), 1UL << 32);
                BinaryPrimitives.WriteUInt64LittleEndian(index.AsSpan(56), (1UL << 32) | 36);
                File.WriteAllBytes(Path.Combine(arrayPath, "integeruniquer.index"), index);
                var uniquer = new Uniquing(directory);
                var decoded = uniquer.DecodeBacktrace(1, "XRCoreProfileCallstackTypeID");
                Assert.Equal(0x18EDECC08UL, Assert.Single(decoded.Addresses));
                Assert.Equal(813L, decoded.ProcessId);
                Assert.Null(uniquer.GetArray(3));
                BinaryPrimitives.WriteUInt64LittleEndian(index.AsSpan(48), 10UL << 32);
                File.WriteAllBytes(Path.Combine(arrayPath, "integeruniquer.index"), index);
                Assert.Throws<InvalidDataException>(() => new Uniquing(directory));
            }
            finally { Directory.Delete(directory, true); }
        }

        [Fact]
        public void CallerReturnAddressAtFunctionEndKeepsOriginalAddress()
        {
            var map = new RuntimeImageMap();
            var image = new ImageLoad(ImageUuid, "module", 0x100000c, 2, 0, (ulong)long.MaxValue,
                new[] { new SymbolSegment("__TEXT", 0x100000, 0x4000) });
            map.Add(1, Guid.NewGuid(), 42, false, new ImageScope(new[] { image }, null));
            var catalog = new SymbolCatalog(map, new[] { SymbolArchive.Parse(BuildArchive()) });
            var context = new SymbolContext(1, 42, 0);
            Assert.Equal(SymbolStatus.ModuleOnly, catalog.ResolveFrame(0x101030, context, 0).Status);
            var caller = catalog.ResolveFrame(0x101030, context, 1);
            Assert.Equal("__dispatch_client_callout", caller.Symbol.Name);
            Assert.Equal(0x1030UL, caller.Coordinate);
            Assert.Equal("second", catalog.ResolveFrame(0x101040, context, 0).Symbol.Name);
            Assert.Equal(SymbolStatus.MissingMapping, catalog.ResolveFrame(0, context, 1).Status);
            var frames = catalog.ResolveBacktrace(new[] { 0x101010UL, 0x101030UL }, context);
            Assert.Equal("__dispatch_client_callout", frames[1].Function.Name);
            Assert.Equal("0x101030", frames[1].Function.Address);
        }

        [Fact]
        public void ArchiveUsesCountedRecordsAndRetainsDoubleUnderscoreNames()
        {
            var archive = SymbolArchive.Parse(BuildArchive(), ImageUuid);
            Assert.Equal(new SymbolSegment("__TEXT", 0, 0x4000), Assert.Single(archive.Segments));
            Assert.Equal("__dispatch_client_callout", archive.Find(0x1010, out bool ambiguous)?.Name);
            Assert.False(ambiguous);
            Assert.Equal("second", archive.Find(0x1040, out _)?.Name);
            Assert.Null(archive.Find(0x1030, out _));
        }

        [Fact]
        public void ArchiveRejectsCorruptBoundsAndMismatchedUuid()
        {
            Assert.Throws<InvalidDataException>(() => SymbolArchive.Parse(BuildArchive(), Guid.NewGuid()));
            var bytes = BuildArchive();
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), uint.MaxValue);
            Assert.Throws<InvalidDataException>(() => SymbolArchive.Parse(bytes));
            bytes = BuildArchive();
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x80 + 16), uint.MaxValue);
            Assert.Throws<InvalidDataException>(() => SymbolArchive.Parse(bytes));
            Assert.Throws<InvalidDataException>(() => SymbolArchive.Parse(new byte[10]));
        }

        [Fact]
        public void LifetimesAreHalfOpenAndQueriesAreOrderIndependent()
        {
            var first = new ImageLoad(ImageUuid, "first", 1, 2, 0, 100,
                new[] { new SymbolSegment("__TEXT", 0x1000, 0x1000) });
            var second = new ImageLoad(Guid.NewGuid(), "second", 1, 2, 150, (ulong)long.MaxValue,
                new[] { new SymbolSegment("__TEXT", 0x1000, 0x1000) });
            var scope = new ImageScope(new[] { first, second }, new TraceClock(0, 1, 1, 0, 0));
            Assert.Same(second, scope.Find(0x1010, 150).Image);
            Assert.Same(first, scope.Find(0x1010, 99).Image);
            Assert.Null(scope.Find(0x1010, 100).Image);
            Assert.Null(scope.Find(0x1010, 149).Image);
            Assert.NotEqual(scope.GetGeneration(99), scope.GetGeneration(150));
        }

        [Fact]
        public void AddressSpacesAreIsolatedAndActiveOverlapsAreAmbiguous()
        {
            var image = new ImageLoad(ImageUuid, "user", 1, 2, 0, (ulong)long.MaxValue,
                new[] { new SymbolSegment("__TEXT", 0x1000, 0x1000) });
            var map = new RuntimeImageMap();
            var device = Guid.NewGuid();
            var scope = new ImageScope(new[] { image }, null);
            map.Add(1, device, 42, false, scope);
            Assert.Same(scope, map.GetScope(new SymbolContext(1, 42, 0)));
            Assert.Null(map.GetScope(new SymbolContext(1, 42, 0, true)));
            Assert.Null(map.GetScope(new SymbolContext(2, 42, 0)));
            Assert.Null(map.GetScope(new SymbolContext(1, 43, 0)));
            Assert.Null(map.GetScope(new SymbolContext(1, 42, 0, false, Guid.NewGuid())));
            var other = new ImageLoad(Guid.NewGuid(), "other", 1, 2, 0, (ulong)long.MaxValue, image.Segments);
            Assert.True(new ImageScope(new[] { image, other }, null).Find(0x1010, 0).Ambiguous);
        }

        [Fact]
        public void ClockUsesRecordingOriginRatherThanWindowOrigin()
        {
            var clock = new TraceClock(100, 125, 3, 1000000000m, 2000000000m);
            Assert.Equal(100m, clock.ToMachTime(1000000000));
            Assert.Equal(103m, clock.ToMachTime(1000000125));
        }

        [Fact]
        public void UserAndKernelSignaturesUseTheSameBoundedDecoder()
        {
            foreach (ulong address in new[] { 0x100000UL, 0xFFFFFE000B384000UL })
            {
                var bytes = new byte[24 + 56 + 8 + 32];
                void Write32(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
                Write32(0, 0xFF01FF02);
                Write32(4, 1);
                Write32(8, uint.MaxValue);
                Write32(20, 1);
                Convert.FromHexString(ImageUuid.ToString("N")).CopyTo(bytes, 24);
                BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(24 + 24), 10);
                BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(24 + 32), 100);
                Write32(24 + 40, 0x100000c);
                Write32(24 + 44, 2);
                Write32(24 + 48, 1);
                Write32(24 + 52, 8);
                Encoding.ASCII.GetBytes("module\0").CopyTo(bytes, 80);
                Encoding.ASCII.GetBytes("__TEXT_EXEC").CopyTo(bytes, 88);
                BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(104), address);
                BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(112), 0x1000);
                var image = Assert.Single(SymbolMetadataReader.ParseSignature(bytes).Images);
                Assert.Equal(address, Assert.Single(image.Segments).Address);
                Assert.Equal(10UL, image.Start);
                Assert.Equal(100UL, image.End);
                Assert.Throws<InvalidDataException>(() => SymbolMetadataReader.ParseSignature(bytes[..^1]));
            }
        }

        [Fact]
        public void ResolutionUsesUuidAndMappingLifetimeInsteadOfGlobalAddresses()
        {
            var map = new RuntimeImageMap();
            var image = new ImageLoad(ImageUuid, "/usr/lib/libdispatch.dylib", 0x100000c, 2, 10, 20,
                new[] { new SymbolSegment("__TEXT", 0x100000, 0x4000) });
            map.Add(1, Guid.NewGuid(), 42, false, new ImageScope(new[] { image }, new TraceClock(0, 1, 1, 0, 0)));
            var catalog = new SymbolCatalog(map, new[] { SymbolArchive.Parse(BuildArchive()) });
            var context = new SymbolContext(1, 42, 10);
            var result = catalog.ResolveAddress(0x101010, context);
            Assert.Equal(SymbolStatus.Named, result.Status);
            Assert.Equal("__dispatch_client_callout", result.Symbol.Name);
            Assert.Equal(ImageUuid, result.Image.Uuid);
            Assert.Equal(0x1010UL, result.Coordinate);
            Assert.Equal(SymbolStatus.MissingMapping, catalog.ResolveAddress(0x101010, context with { Timestamp = 20 }).Status);
            Assert.Equal(SymbolStatus.MissingMapping, catalog.ResolveAddress(0x101010, context with { Kernel = true }).Status);
            Assert.Equal("0x101010", catalog.ResolveBacktrace(new[] { 0x101010UL }, context)[0].Function.Address);
        }

        [Fact]
        public void BinaryPlistAndSharedCacheRebasingResolveWithoutExternalSymbols()
        {
            using var fixture = new BundleFixture(sharedCache: true);
            var catalog = SymbolCatalog.Load(fixture.Path);
            Assert.Empty(catalog.Diagnostics);
            var context = new SymbolContext(1, 42, 10);
            var result = catalog.ResolveAddress(0x105010, context);
            Assert.Equal(SymbolStatus.Named, result.Status);
            Assert.Equal(ImageUuid, result.Image.Uuid);
            Assert.Equal(0x1010UL, result.Coordinate);
            Assert.Equal(SymbolStatus.MissingMapping, catalog.ResolveAddress(0x105010, context with { ProcessId = 43 }).Status);
        }

        [Fact]
        public void LazyCacheDoesNotRetainSymbolsAcrossUnloadAndReload()
        {
            using var fixture = new BundleFixture();
            var secondUuid = Guid.NewGuid();
            var segments = new[] { new SymbolSegment("__TEXT", 0x100000, 0x4000) };
            var map = new RuntimeImageMap();
            map.Add(1, fixture.Device, 42, false, new ImageScope(new[] {
                new ImageLoad(ImageUuid, "first", 0x100000c, 2, 0, 100, segments),
                new ImageLoad(secondUuid, "second", 0x100000c, 2, 150, (ulong)long.MaxValue, segments)
            }, new TraceClock(0, 1, 1, 0, 0)));
            var secondArchive = new SymbolArchive(secondUuid, 0x100000c, 2,
                new[] { new SymbolSegment("__TEXT", 0, 0x4000) }, new[] { new SymbolEntry("replacement", 0x1000, 0x30) });
            var catalog = new SymbolCatalog(map, new[] { SymbolArchive.Parse(BuildArchive()), secondArchive });
            var uniquing = new Uniquing(fixture.UniquingPath);
            var cache = new TraceBundleEventFactory.InternCache();
            var context = new SymbolContext(1, 42, 10);
            var before = cache.GetOrCreateBacktrace(1, uniquing, catalog, context, "XRCoreProfileCallstackTypeID");
            var after = cache.GetOrCreateBacktrace(1, uniquing, catalog, context with { Timestamp = 150 }, "XRCoreProfileCallstackTypeID");
            Assert.Equal("replacement", Assert.Single(after.Frames).Function.Name);
            Assert.Equal("__dispatch_client_callout", Assert.Single(before.Frames).Function.Name);
            Assert.NotSame(before, after);
            Assert.Same(before, cache.GetOrCreateBacktrace(1, uniquing, catalog, context with { Timestamp = 99 }, "XRCoreProfileCallstackTypeID"));
            var gap = cache.GetOrCreateBacktrace(1, uniquing, catalog, context with { Timestamp = 100 }, "XRCoreProfileCallstackTypeID");
            Assert.Equal("0x101010", Assert.Single(gap.Frames).Function.Name);
            Assert.Empty(uniquing.DecodeBacktrace(-1, "XRCoreProfileCallstackTypeID").Addresses);
            Assert.Empty(uniquing.DecodeBacktrace(1, "unknown").Addresses);
        }

        [Fact]
        public void KernelUsesCorrespondingExecutableSegmentCoordinates()
        {
            const ulong runtime = 0xFFFFFE000B384000;
            var image = new ImageLoad(ImageUuid, "kernel", 0x100000c, 2, 0, (ulong)long.MaxValue,
                new[] { new SymbolSegment("__TEXT_EXEC", runtime, 0x1000) });
            var archive = new SymbolArchive(ImageUuid, 0x100000c, 2,
                new[] { new SymbolSegment("__TEXT_EXEC", 0x4000, 0x1000) }, new[] { new SymbolEntry("kernel_function", 0x4020, 0x30) });
            var map = new RuntimeImageMap();
            map.Add(1, Guid.NewGuid(), -1, true, new ImageScope(new[] { image }, null));
            var catalog = new SymbolCatalog(map, new[] { archive });
            Assert.Equal("kernel_function", catalog.ResolveAddress(runtime + 0x28, new SymbolContext(1, 42, 0, true)).Symbol.Name);
            Assert.Equal(SymbolStatus.MissingMapping, catalog.ResolveAddress(runtime + 0x28, new SymbolContext(1, 42, 0)).Status);
        }

        [Fact]
        public void ExternalSymbolsUseRecordedBaseAndCannotOverrideNamedBundleSymbols()
        {
            using var fixture = new BundleFixture();
            string store = System.IO.Path.Combine(fixture.Path, "external");
            Directory.CreateDirectory(store);
            File.WriteAllText(System.IO.Path.Combine(store, "manifest.json"), JsonSerializer.Serialize(new {
                uuid = ImageUuid.ToString(), module = "wrong-name", load_addr = "0x99900000", arch = "arm64e"
            }));
            File.WriteAllText(System.IO.Path.Combine(store, "symbols.nm"), "00001000 T external_function\n00001100 T next\n");
            var catalog = SymbolCatalog.Load(fixture.Path);
            Assert.Equal(1, catalog.MergeDsyms(store).MatchedImages);
            var context = new SymbolContext(1, 42, 10);
            Assert.Equal("__dispatch_client_callout", catalog.ResolveAddress(0x101010, context).Symbol.Name);
            Assert.Equal(SymbolStatus.MissingMapping, catalog.ResolveAddress(0x99901010, context).Status);
            Directory.Delete(System.IO.Path.Combine(fixture.Path, "symbols"), true);
            catalog = SymbolCatalog.Load(fixture.Path);
            Assert.Equal(1, catalog.MergeDsyms(store).MatchedImages);
            Assert.Equal("external_function", catalog.ResolveAddress(0x101010, context).Symbol.Name);
            Assert.Equal("module", catalog.ResolveAddress(0x101010, context).Image.Name);
        }

        [Theory]
        [InlineData(2U, 0xC0000002U, true)]
        [InlineData(0x80000002U, 0x40000002U, true)]
        [InlineData(2U, 1U, false)]
        public void CpuSubtypeMatchingIgnoresCapabilityBits(uint recorded, uint external, bool expected)
        {
            Assert.Equal(expected, SymbolCatalog.CpuSubtypesMatch(recorded, external));
        }

        [Fact]
        public void ExternalKernelSymbolsUseCorrespondingExecutableSegmentCoordinates()
        {
            const ulong runtimeTextExec = 0xFFFFFE000B384000;
            var source = new SymbolArchive(ImageUuid, 0x100000c, 2,
                new[] { new SymbolSegment("__TEXT_EXEC", 0x124000, 0x900000) },
                new[] { new SymbolEntry("_kperf_kdebug_handler", 0x2E75B4, 0x200) });

            var symbol = SymbolCatalog.FindExternalSymbol(source,
                new SymbolSegment("__TEXT_EXEC", runtimeTextExec, 0x900000),
                0xFFFFFE000B5476DC, out ulong coordinate, out bool ambiguous);

            Assert.False(ambiguous);
            Assert.Equal(0x2E76DCUL, coordinate);
            Assert.Equal("_kperf_kdebug_handler", symbol.Name);
            Assert.Null(SymbolCatalog.FindExternalSymbol(source,
                new SymbolSegment("__TEXT", 0xFFFFFE000700C000, 0x8000),
                0xFFFFFE000700C010, out _, out _));
        }

        [Fact]
        public void MalformedArchiveDoesNotDiscardValidMappings()
        {
            using var fixture = new BundleFixture();
            File.WriteAllBytes(fixture.ArchivePath, new byte[8]);
            var catalog = SymbolCatalog.Load(fixture.Path);
            Assert.NotEmpty(catalog.Diagnostics);
            Assert.Equal(SymbolStatus.MissingArchive, catalog.ResolveAddress(0x101010, new SymbolContext(1, 42, 0)).Status);
        }

        [Fact]
        public void ResolutionSurvivesExtractedBundleCleanup()
        {
            using var fixture = new BundleFixture();
            string zip = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + ".zip");
            string extracted = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString());
            try
            {
                ZipFile.CreateFromDirectory(fixture.Path, zip);
                ZipFile.ExtractToDirectory(zip, extracted);
                var catalog = SymbolCatalog.Load(extracted);
                var frames = new InstrumentsProcessor.Parsing.DataModels.LazyBacktrace(new[] { 0x101010UL }, catalog, new SymbolContext(1, 42, 0));
                Directory.Delete(extracted, true);
                Assert.Equal("__dispatch_client_callout", Assert.Single(frames.Frames).Function.Name);
            }
            finally
            {
                File.Delete(zip);
                if (Directory.Exists(extracted)) Directory.Delete(extracted, true);
            }
        }

        [LocalTraceFact]
        public void RecordedNflStackMatchesVerifiedNamesAndCoordinates()
        {
            string path = Environment.GetEnvironmentVariable("INSTRUMENTS_TEST_TRACE")!;
            var catalog = SymbolCatalog.Load(path);
            Assert.Empty(catalog.Diagnostics);
            var uniquing = new Uniquing(System.IO.Path.Combine(path, "corespace", "run1", "core", "uniquing"));
            var decoded = uniquing.DecodeBacktrace(10, "XRCoreProfileCallstackTypeID");
            Assert.Equal(722, decoded.ProcessId);
            Assert.Equal(37, decoded.Addresses.Length);
            Assert.Equal(29, uniquing.GetArray(9757).Length);
            Assert.Equal(0x1AD6A5560UL, uniquing.GetArray(9757)[0]);
            Assert.Equal(559, uniquing.DecodeBacktrace(20026, "XRCoreProfileCallstackTypeID").ProcessId);
            Assert.Equal(813, uniquing.DecodeBacktrace(23144, "XRCoreProfileCallstackTypeID").ProcessId);
            Assert.Single(uniquing.DecodeBacktrace(29, "XRCoreProfileCallstackTypeID").Addresses);
            Assert.Equal(20, uniquing.DecodeBacktrace(8792, "XRCoreProfileCallstackTypeID").Addresses.Length);
            var context = new SymbolContext(1, 722, 4381683583);
            var results = decoded.Addresses.Select((address, index) => catalog.ResolveFrame(address, context, index)).ToArray();
            Assert.Equal(33, results.Count(result => result.Status == SymbolStatus.Named));
            Assert.Equal(4, results.Count(result => result.Status == SymbolStatus.ModuleOnly));
            var dispatch = catalog.ResolveAddress(0x18EC504B0, context);
            Assert.Equal(ImageUuid, dispatch.Image.Uuid);
            Assert.Equal(0x1B4B0UL, dispatch.Coordinate);
            Assert.Equal("__dispatch_client_callout", dispatch.Symbol.Name);
        }

        public sealed class LocalTraceFactAttribute : FactAttribute
        {
            public LocalTraceFactAttribute()
            {
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("INSTRUMENTS_TEST_TRACE")))
                    Skip = "Set INSTRUMENTS_TEST_TRACE to the verified 20260707_155842 NFL trace for the local integration check.";
            }
        }

        private sealed class BundleFixture : IDisposable
        {
            public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + ".trace");
            public Guid Device { get; } = Guid.NewGuid();
            public string ArchivePath => System.IO.Path.Combine(Path, "symbols", "stores", ImageUuid + ".symbolsarchive");
            public string UniquingPath => System.IO.Path.Combine(Path, "uniquing");
            private readonly List<NSObject> objects = new() { new NSString("$null") };

            private UID Reference(NSObject value)
            {
                objects.Add(value);
                return new UID((uint)(objects.Count - 1));
            }

            private NSDictionary Dictionary(params (string Key, NSObject Value)[] entries)
            {
                var result = new NSDictionary();
                result.Add("NS.keys", new NSArray(entries.Select(entry => (NSObject)Reference(new NSString(entry.Key))).ToArray()));
                result.Add("NS.objects", new NSArray(entries.Select(entry => (NSObject)Reference(entry.Value)).ToArray()));
                return result;
            }

            private static NSDictionary Fields(params (string Key, NSObject Value)[] entries)
            {
                var result = new NSDictionary();
                foreach (var entry in entries) result.Add(entry.Key, entry.Value);
                return result;
            }

            public BundleFixture(bool sharedCache = false)
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ArchivePath)!);
                File.WriteAllBytes(ArchivePath, BuildArchive());
                Guid cacheUuid = Guid.NewGuid();
                byte[] signature = Signature(sharedCache ? Guid.NewGuid() : ImageUuid, 42, sharedCache ? 0x300000UL : 0x100000UL);
                if (sharedCache)
                {
                    int tail = signature.Length;
                    Array.Resize(ref signature, tail + 32);
                    BinaryPrimitives.WriteUInt32LittleEndian(signature.AsSpan(tail), 0x00C0FFEE);
                    BinaryPrimitives.WriteUInt32LittleEndian(signature.AsSpan(tail + 4), 4);
                    Convert.FromHexString(cacheUuid.ToString("N")).CopyTo(signature, tail + 8);
                    BinaryPrimitives.WriteUInt64LittleEndian(signature.AsSpan(tail + 24), 0x100000);
                }
                var persistent = Fields(
                    ("timeline-type", Reference(new NSString("mach-absolute"))),
                    ("$1", Reference(Fields(("NS.objects", new NSArray(Array.Empty<NSObject>()))))),
                    ("com.apple.xray.symbolstore.signature", Reference(Fields(("NS.data", new NSData(signature))))),
                    ("dsc_load_addresses", Reference(Fields(("NS.objects", new NSArray(sharedCache ? new NSObject[] { new NSNumber(0x4000) } : Array.Empty<NSObject>()))))));
                var clocks = Dictionary((Device.ToString(), Fields(("NS.objects", new NSArray(new NSObject[] {
                    new NSNumber(0), new NSNumber(1), new NSNumber(1), new NSNumber(0), new NSNumber(0), new NSString("")
                })))));
                var run = Dictionary(("startTime", new NSNumber(0)), ("mach_time_info", clocks));
                var top = Fields(
                    ("com.apple.xray.run.data", Reference(Fields(("$1", Reference(Dictionary(("1", run))))))),
                    ("com.apple.xray.symbolstoremanager.symbolstores", Reference(Dictionary(("1", Dictionary(($"{Device}.42", persistent)))))),
                    ("com.apple.xray.symbolstore.kern.signatures", Reference(Dictionary(("1", Fields(("NS.data", new NSData(Signature(Guid.NewGuid(), uint.MaxValue, 0xFFFFFE000B384000)))))))));
                if (sharedCache)
                    top.Add("com.apple.xray.symbolstore.sharedcache.signatures", Reference(Dictionary((cacheUuid.ToString(), Dictionary(("16384", Fields(("NS.data", new NSData(Signature(ImageUuid, 42, 0x4000))))))))));
                var document = Fields(("$top", top), ("$objects", new NSArray(objects.ToArray())));
                using (var file = File.Create(System.IO.Path.Combine(Path, "form.template")))
                using (var compressed = new ZLibStream(file, CompressionMode.Compress)) PropertyListParser.SaveAsBinary(document, compressed);
                Directory.CreateDirectory(System.IO.Path.Combine(UniquingPath, "arrayUniquer"));
                using var arrays = new BinaryWriter(File.Create(System.IO.Path.Combine(UniquingPath, "arrayUniquer", "integeruniquer.data")));
                arrays.Write(new byte[32]);
                foreach (ulong[] entry in new[] { new ulong[] { 0x101010 }, new ulong[] { 0, 0, 2, 0, 0 }, new ulong[] { 42, 0 } })
                {
                    arrays.Write((uint)entry.Length);
                    foreach (ulong value in entry) arrays.Write(value);
                }
            }

            private static byte[] Signature(Guid uuid, uint pid, ulong address)
            {
                var bytes = new byte[120];
                void Write32(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
                Write32(0, 0xFF01FF02);
                Write32(4, 1);
                Write32(8, pid);
                Write32(20, 1);
                Convert.FromHexString(uuid.ToString("N")).CopyTo(bytes, 24);
                BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(56), (ulong)long.MaxValue);
                Write32(64, 0x100000c);
                Write32(68, 2);
                Write32(72, 1);
                Write32(76, 8);
                Encoding.ASCII.GetBytes("module").CopyTo(bytes, 80);
                Encoding.ASCII.GetBytes("__TEXT").CopyTo(bytes, 88);
                BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(104), address);
                BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(112), 0x4000);
                return bytes;
            }

            public void Dispose() => Directory.Delete(Path, true);
        }

        internal static byte[] BuildArchive()
        {
            byte[] strings = Encoding.UTF8.GetBytes("MACH_HEADER\0__dispatch_client_callout\0second\0");
            var bytes = new byte[0x80 + 48 + strings.Length];
            void Write32(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
            Write32(0, 7);
            Write32(4, (uint)bytes.Length);
            Write32(8, 1);
            Write32(16, 2);
            Convert.FromHexString(ImageUuid.ToString("N")).CopyTo(bytes, 0x34);
            Write32(0x44, 0x100000c);
            Write32(0x48, 2);
            Write32(0x54, (uint)strings.Length);
            BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(0x68), 0x4000);
            Encoding.ASCII.GetBytes("__TEXT").CopyTo(bytes, 0x70);
            Write32(0x80, 0x1000);
            Write32(0x84, 0x30);
            Write32(0x90, 12);
            Write32(0x94, uint.MaxValue);
            Write32(0x98, 0x1040);
            Write32(0x9c, 0x20);
            Write32(0xa8, 38);
            Write32(0xac, uint.MaxValue);
            strings.CopyTo(bytes, 0xb0);
            return bytes;
        }
    }
}