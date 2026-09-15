using InstrumentsProcessor.Parsing;
using InstrumentsProcessor.Parsing.Events;
using InstrumentsProcessor.Parsing.TraceBundle;
using System.Reflection;
using System.Xml;
using Microsoft.Performance.SDK;
using Microsoft.Performance.SDK.Extensibility;
using Microsoft.Performance.SDK.Extensibility.SourceParsing;
using Microsoft.Performance.SDK.Processing;
using InstrumentsProcessor.Parsing.DataModels;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Serialization;
using System.Diagnostics;
using Xunit.Abstractions;
using Event = InstrumentsProcessor.Parsing.Events.Event;
using Process = InstrumentsProcessor.Parsing.DataModels.Process;
using Thread = InstrumentsProcessor.Parsing.DataModels.Thread;
using Module = InstrumentsProcessor.Parsing.DataModels.Module;

namespace InstrumentsProcessorTests
{
    [Collection("Trace symbol parity")]
    public class TraceSymbolParityTests
    {
        private readonly ITestOutputHelper output;

        public TraceSymbolParityTests(ITestOutputHelper output) => this.output = output;

        [Theory]
        [InlineData("function", "function", true, "ExactName")]
        [InlineData("function", "_function", true, "LeadingUnderscoreDifference")]
        [InlineData("function(int)", "function(long)", true, "SymbolNameMismatch")]
        [InlineData("function", "module+0x100", false, "LostSymbol")]
        [InlineData("0x1000", "function", true, "ImprovedSymbol")]
        [InlineData("0x1000", "module+0x100", false, "BothUnresolved")]
        public void ClassifierDoesNotHideNameOrCoverageChanges(string expected, string actual, bool named, string category)
        {
            Assert.Equal(category, NameCategory(expected, actual, named ? SymbolStatus.Named : SymbolStatus.ModuleOnly));
        }

        [Fact]
        public void XmlReferencesRetainRepeatedFramesAndRequireTheirOwnTableScope()
        {
            var references = new Dictionary<string, ReferenceValue>();
            XmlElement Row(string text)
            {
                var document = new XmlDocument();
                document.LoadXml(text);
                return document.DocumentElement!;
            }
            var first = ReadReference(Row("<row><tagged-backtrace id='1'><backtrace id='2'><frame id='3' addr='0x1000' name='function'><binary id='4' name='module' UUID='F071EFE4-299F-3089-ACC4-0025B8FFB52A' arch='arm64e' load-addr='0x800'/></frame><frame ref='3'/></backtrace></tagged-backtrace></row>"), references);
            var second = ReadReference(Row("<row><tagged-backtrace ref='1'/></row>"), references);
            var frames = Frames(second).Select(OracleFrame.FromReference).ToArray();
            Assert.Equal(2, frames.Length);
            Assert.Equal(frames[0], frames[1]);
            Assert.Equal(0x800UL, frames[0].LoadAddress);
            Assert.Equal(Frames(first), Frames(second));
            Assert.Throws<InvalidDataException>(() => ReadReference(Row("<row><tagged-backtrace ref='1'/></row>"), new()));
            Assert.Throws<InvalidDataException>(() => ReadReference(Row("<row><binary ref='1'/></row>"), references));
        }

        [Fact]
        public void DuplicateEventsAreConsumedOnceWithoutUsingRowOrder()
        {
            var key = new EventKey(1, "time-profile", 100, 42, 10);
            Sample SampleAt(string address) => new(key, new[] { new Frame(new Function("function", address)) }, "3", 1, "");
            var first = SampleAt("0x1000");
            var second = SampleAt("0x2000");
            var entries = new List<Sample> { second, first, first };
            Assert.Same(first, TakeMatch(entries, first));
            Assert.Same(second, TakeMatch(entries, second));
            Assert.Same(first, TakeMatch(entries, first));
            Assert.Empty(entries);
        }

        [Fact]
        public void ReportCountsEveryDifferenceButBoundsExamples()
        {
            var report = new ComparisonReport();
            for (int index = 0; index < 25; index++) report.Add("LostSymbol", null, index, "function", "0x1000", "detail");
            report.Add("LeadingUnderscoreDifference", null, 0, "function", "_function", "detail");
            report.Add("ExactName", null, 0, "function", "function", "detail");
            Assert.Equal(26, report.Failures);
            Assert.Equal(25, report.Categories["LostSymbol"]);
            Assert.Equal(20, report.Examples["LostSymbol"].Count);
        }

        [Fact]
        public void FrameComparisonUsesIndependentImageAndLoadExpectations()
        {
            var uuid = Guid.NewGuid();
            var image = new ImageLoad(uuid, "/usr/lib/module", 0x100000c, 2, 0, (ulong)long.MaxValue,
                new[] { new SymbolSegment("__TEXT", 0x1000, 0x1000) });
            var map = new RuntimeImageMap();
            map.Add(1, Guid.NewGuid(), 42, false, new ImageScope(new[] { image }, null));
            var archive = new SymbolArchive(uuid, 0x100000c, 2, new[] { new SymbolSegment("__TEXT", 0, 0x1000) },
                new[] { new SymbolEntry("function", 0x20, 0x20) });
            var catalog = new SymbolCatalog(map, new[] { archive });
            var key = new EventKey(1, "time-profile", 10, 42, 100);
            var actual = new Sample(key, catalog.ResolveBacktrace(new[] { 0x1028UL }, new SymbolContext(1, 42, 10)), "3", 1000, "");
            var expected = new Sample(key, new[] { new Frame(new Function("function", "0x1028"), new Module("module")) }, "3", 1000, "");
            var oracle = new OracleFrame(0x1028, "function", "module", uuid, "arm64e", 0x1000);
            var report = new ComparisonReport();
            CompareSamples(expected, actual, new[] { oracle }, catalog, report);
            Assert.Equal(0, report.Failures);
            Assert.Equal(1, report.Categories["ExactName"]);
            CompareSamples(expected, actual, new[] { oracle with { Uuid = Guid.NewGuid(), LoadAddress = 0x2000 } }, catalog, report);
            Assert.Equal(1, report.Categories["ImageUuidMismatch"]);
            Assert.Equal(1, report.Categories["LoadAddressMismatch"]);
        }

        [PairFact]
        public void FirstSampleHasIdenticalRecordedFrameAddresses()
        {
            string trace = Environment.GetEnvironmentVariable("INSTRUMENTS_DIFF_TRACE")!;
            string xml = Environment.GetEnvironmentVariable("INSTRUMENTS_DIFF_XML")!;
            Assert.True(Directory.Exists(trace), $"Missing trace bundle: {trace}");
            Assert.True(File.Exists(xml), $"Missing XML reference: {xml}");
            using var reader = OpenXml(xml);
            XmlElement? expected = null;
            while (reader.ReadToFollowing("schema"))
            {
                bool sampleSchema = reader.GetAttribute("name") == "time-profile";
                reader.Skip();
                if (!sampleSchema) continue;
                Assert.Equal("row", reader.Name);
                expected = (XmlElement)new XmlDocument().ReadNode(reader)!;
                break;
            }
            Assert.NotNull(expected);
            long time = long.Parse(expected.SelectSingleNode("sample-time")!.InnerText);
            var definitions = expected.SelectNodes(".//*[@id]")!.Cast<XmlElement>()
                .ToDictionary(element => element.GetAttribute("id"));
            var expectedAddresses = expected.SelectNodes("tagged-backtrace/backtrace/frame")!.Cast<XmlElement>()
                .Select(frame => frame.HasAttribute("ref") ? definitions[frame.GetAttribute("ref")] : frame)
                .Select(frame => Convert.ToUInt64(frame.GetAttribute("addr")[2..], 16)).ToArray();
            Assert.NotEmpty(expectedAddresses);

            string corespace = Path.Combine(trace, "corespace");
            string core = Path.Combine(corespace, "run1", "core");
            var stores = TableManager.ParseTablesPlist(Path.Combine(core, "table-manager", "tables.plist"), corespace);
            var store = Assert.Single(stores.Where(store => store.Side == 0 && store.SchemaName == "time-profile"));
            var schema = new StoreSchema(store.StorePath, store.SchemaName);
            var uniquing = new Uniquing(Path.Combine(core, "uniquing"));
            NameResolver.BuildPidNameMap(stores, uniquing, out var processes);
            var threads = NameResolver.BuildThreadRefMap(stores, uniquing);
            var stack = Assert.Single(schema.Columns.Where(column => column.EngineeringType.Contains("Backtrace")));
            var first = BulkstoreReader.ReadRows(store.StorePath, schema).First();
            Assert.Equal(time, checked((long)Convert.ToUInt64(first["timestamp"])));
            var context = TraceBundleEventFactory.GetSymbolContext(first, schema, uniquing, 1, processes, threads);
            Assert.Equal(long.Parse(expected.SelectSingleNode(".//process/pid")!.InnerText), context.ProcessId);
            var decoded = uniquing.DecodeBacktrace(Convert.ToInt32(first[stack.Mnemonic]), stack.EngineeringType);
            Assert.Equal(expectedAddresses, decoded.Addresses);
        }

        [PairFact]
        public void TraceAndXmlHaveIdenticalStacksAndSymbols()
        {
            string trace = Environment.GetEnvironmentVariable("INSTRUMENTS_DIFF_TRACE")!;
            string xml = Environment.GetEnvironmentVariable("INSTRUMENTS_DIFF_XML")!;
            Assert.True(Directory.Exists(trace), $"Missing trace bundle: {trace}");
            Assert.True(File.Exists(xml), $"Missing XML reference: {xml}");
            string reportPath = Environment.GetEnvironmentVariable("INSTRUMENTS_DIFF_REPORT") ??
                Path.Combine(AppContext.BaseDirectory, "TestResults", "trace-symbol-parity.json");
            var report = new ComparisonReport
            {
                TracePath = Path.GetFullPath(trace), XmlPath = Path.GetFullPath(xml),
                XmlSha256 = HashFile(xml), MetadataSha256 = HashFile(Path.Combine(trace, "form.template")),
            };
            var watch = Stopwatch.StartNew();
            try
            {
                Assert.Equal(new[] { "run1" }, Directory.EnumerateDirectories(Path.Combine(trace, "corespace"))
                    .Select(Path.GetFileName).Where(name => name != "currentRun" && name!.StartsWith("run", StringComparison.Ordinal)).OrderBy(name => name));
                var catalog = SymbolCatalog.Load(trace);
                foreach (string diagnostic in catalog.Diagnostics) report.Add("TraceDiagnostic", null, -1, null, null, diagnostic);

                string pluginDirectory = Path.GetDirectoryName(typeof(TraceSourceParser).Assembly.Location)!;
                Assert.False(Directory.Exists(Path.Combine(pluginDirectory, "SymbolStore")),
                    "Move the plugin-relative SymbolStore out of the test output to isolate bundled symbol resolution.");
                string? oldSymbols = Environment.GetEnvironmentVariable("INSTRUMENTS_SYMBOL_PATH");
                var actual = new Dictionary<EventKey, List<Sample>>();
                try
                {
                    Environment.SetEnvironmentVariable("INSTRUMENTS_SYMBOL_PATH", null);
                    var parser = new TraceSourceParser(new[] { new DirectoryDataSource(trace) });
                    parser.ProcessSource(new Sink(evt =>
                    {
                        if (!ComparedSchemas.Contains(evt.SchemaName)) return;
                        var sample = Sample.FromEvent(evt);
                        if (!actual.TryGetValue(sample.Key, out var entries)) actual[sample.Key] = entries = new List<Sample>();
                        entries.Add(sample);
                        report.Table(evt.SchemaName).TraceEvents++;
                        report.Table(evt.SchemaName).TraceFrames += sample.Frames.Count;
                    }), null, new NullProgress(), CancellationToken.None);
                }
                finally { Environment.SetEnvironmentVariable("INSTRUMENTS_SYMBOL_PATH", oldSymbols); }

                using var reader = OpenXml(xml);
                var schemaSerializer = new XmlSerializer(typeof(Schema));
                var seenSchemas = new HashSet<string>();
                while (reader.ReadToFollowing("node"))
                {
                    using var nodeReader = reader.ReadSubtree();
                    nodeReader.Read();
                    string xpath = nodeReader.GetAttribute("xpath") ?? "";
                    if (!nodeReader.ReadToDescendant("schema")) continue;
                    string name = nodeReader.GetAttribute("name") ?? "";
                    if (!ComparedSchemas.Contains(name)) continue;
                    Assert.True(xpath.Contains("run[1]", StringComparison.Ordinal) || xpath.Contains("run[@number='1']", StringComparison.Ordinal) ||
                        xpath.Contains("run[@number=\"1\"]", StringComparison.Ordinal), $"Unsupported run selector: {xpath}");
                    Assert.True(seenSchemas.Add(name), $"Multiple XML tables for {name} need an explicit table identity; refusing to merge their reference scopes.");
                    var schema = (Schema)schemaSerializer.Deserialize(nodeReader)!;
                    IEventDeserializer deserializer = name switch
                    {
                        "time-profile" => new EventDeserializer<TimeProfileEvent>(),
                        "syscall" => new EventDeserializer<SyscallEvent>(),
                        "virtual-memory" => new EventDeserializer<VirtualMemoryEvent>(),
                        _ => throw new InvalidOperationException(name),
                    };
                    Assert.True(deserializer.CanDeserialize(schema), $"Legacy XML loader does not support the {name} schema.");
                    var xmlContext = new XmlParsingContext();
                    var references = new Dictionary<string, ReferenceValue>();
                    var document = new XmlDocument();
                    while (nodeReader.Name == "row")
                    {
                        var row = (XmlElement)document.ReadNode(nodeReader)!;
                        var oracle = ReadReference(row, references);
                        var expected = Sample.FromEvent(deserializer.Deserialize(row, xmlContext, schema));
                        report.Table(name).XmlEvents++;
                        var oracleFrames = Frames(oracle).Select(OracleFrame.FromReference).ToArray();
                        report.Table(name).XmlFrames += oracleFrames.Length;
                        report.Table(name).LegacyFrames += expected.Frames.Count;
                        CompareLegacyOracle(expected, oracleFrames, report);
                        if (!actual.TryGetValue(expected.Key, out var candidates) || candidates.Count == 0)
                        {
                            report.Add("MissingTraceEvent", expected.Key, -1, null, null, "No event with the same identity and timestamp.");
                            continue;
                        }
                        var found = TakeMatch(candidates, expected);
                        if (candidates.Count == 0) actual.Remove(expected.Key);
                        CompareSamples(expected, found, oracleFrames, catalog, report);
                    }
                }
                foreach (var samples in actual.Values)
                    foreach (var sample in samples) report.Add("ExtraTraceEvent", sample.Key, -1, null, null, "No corresponding XML event.");
                foreach (string name in ComparedSchemas)
                {
                    var table = report.Table(name);
                    if (!seenSchemas.Contains(name) || table.XmlEvents == 0 || table.TraceEvents == 0)
                        report.Add("MissingCoverage", null, -1, null, null, $"{name}: XML={table.XmlEvents}, trace={table.TraceEvents}");
                    if (table.XmlFrames == 0) report.Add("MissingFrameCoverage", null, -1, null, null, name);
                }
                report.Completed = true;
            }
            catch (Exception error)
            {
                report.Add("HarnessOrLoaderError", null, -1, null, null, error.ToString());
            }
            finally
            {
                report.ElapsedSeconds = watch.Elapsed.TotalSeconds;
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
                File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
                output.WriteLine($"Report: {Path.GetFullPath(reportPath)}");
                foreach (var table in report.Tables)
                    output.WriteLine($"{table.Key}: XML events={table.Value.XmlEvents}, trace events={table.Value.TraceEvents}, XML frames={table.Value.XmlFrames}, trace frames={table.Value.TraceFrames}");
                foreach (var count in report.Categories.OrderBy(pair => pair.Key)) output.WriteLine($"{count.Key}: {count.Value}");
            }
            Assert.True(report.Failures == 0, $"{report.Failures} parity differences; see {Path.GetFullPath(reportPath)}. " +
                string.Join(", ", report.Categories.Where(pair => !ComparisonReport.IsInformational(pair.Key)).Select(pair => $"{pair.Key}={pair.Value}")));
        }

        private static readonly HashSet<string> ComparedSchemas = new(StringComparer.Ordinal) { "time-profile", "syscall", "virtual-memory" };

        private sealed record EventKey(int Run, string Schema, long Timestamp, long Process, long Thread);
        private sealed record Sample(EventKey Key, IReadOnlyList<Frame> Frames, string Cpu, long Duration, string Discriminator)
        {
            public static Sample FromEvent(Event evt)
            {
                (Process process, Thread thread, Backtrace stack, string cpu, long duration, string discriminator) = evt switch
                {
                    TimeProfileEvent sample => (sample.Process, sample.Thread, sample.Backtrace, sample.Core?.Value ?? "", sample.Weight?.Value.ToNanoseconds ?? 0, ""),
                    SyscallEvent syscall => (syscall.Process, syscall.Thread, syscall.Stack, "", syscall.Duration?.Value.ToNanoseconds ?? 0, syscall.Call?.Value ?? ""),
                    VirtualMemoryEvent memory => (memory.Process, memory.Thread, memory.Stack, "", memory.Duration?.Value.ToNanoseconds ?? 0,
                        $"{memory.Operation?.Value}|{memory.Address?.Value}|{memory.Size?.Value}"),
                    _ => throw new InvalidOperationException("Unsupported event type."),
                };
                var key = new EventKey(1, evt.SchemaName, evt.Timestamp.ToNanoseconds,
                    process?.ProcessId?.Value ?? -1, thread?.ThreadId?.Value ?? -1);
                return new Sample(key, stack?.Frames ?? Array.Empty<Frame>(), cpu, duration, discriminator);
            }
        }

        private sealed record ReferenceValue(string Type, string Text, Dictionary<string, string> Attributes, ReferenceValue[] Children);

        private static ReferenceValue ReadReference(XmlElement element, Dictionary<string, ReferenceValue> references)
        {
            if (element.HasAttribute("ref"))
            {
                if (!references.TryGetValue(element.GetAttribute("ref"), out var reference))
                    throw new InvalidDataException($"Undefined XML reference {element.GetAttribute("ref")}.");
                if (reference.Type != element.Name) throw new InvalidDataException("XML reference type mismatch.");
                return reference;
            }
            var attributes = element.Attributes.Cast<XmlAttribute>().Where(attribute => attribute.Name != "id")
                .ToDictionary(attribute => attribute.Name, attribute => attribute.Value);
            var children = element.ChildNodes.OfType<XmlElement>().Select(child => ReadReference(child, references)).ToArray();
            var value = new ReferenceValue(element.Name, children.Length == 0 ? element.InnerText : "", attributes, children);
            if (element.HasAttribute("id"))
            {
                if (!references.TryAdd(element.GetAttribute("id"), value)) throw new InvalidDataException("Duplicate XML definition.");
            }
            return value;
        }

        private static IEnumerable<ReferenceValue> Frames(ReferenceValue row)
        {
            var stack = row.Children.SingleOrDefault(child => child.Type == "tagged-backtrace" || child.Type == "backtrace");
            if (stack?.Type == "tagged-backtrace") stack = stack.Children.SingleOrDefault(child => child.Type == "backtrace");
            return stack?.Children.Where(child => child.Type == "frame") ?? Enumerable.Empty<ReferenceValue>();
        }

        private sealed record OracleFrame(ulong Address, string Name, string Module, Guid? Uuid, string Architecture, ulong? LoadAddress)
        {
            public static OracleFrame FromReference(ReferenceValue frame)
            {
                var binary = frame.Children.SingleOrDefault(child => child.Type == "binary");
                string Attribute(ReferenceValue? node, string name) => node != null && node.Attributes.TryGetValue(name, out string? value) ? value : "";
                string address = Attribute(frame, "addr");
                string uuid = Attribute(binary, "UUID");
                string load = Attribute(binary, "load-addr");
                return new OracleFrame(ParseAddress(address), Attribute(frame, "name"), Attribute(binary, "name"),
                    uuid.Length == 0 ? null : Guid.Parse(uuid), Attribute(binary, "arch"), load.Length == 0 ? null : ParseAddress(load));
            }
        }

        private static ulong ParseAddress(string value) => ulong.Parse(value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value[2..] : value,
            NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        private static ulong? FrameAddress(Frame? frame) => string.IsNullOrEmpty(frame?.Function?.Address) ? null : ParseAddress(frame.Function.Address);

        private static bool SameAddresses(Sample left, Sample right) => left.Frames.Count == right.Frames.Count &&
            left.Frames.Select(FrameAddress).SequenceEqual(right.Frames.Select(FrameAddress));

        private static Sample TakeMatch(List<Sample> candidates, Sample expected)
        {
            int index = candidates.FindIndex(candidate => candidate.Cpu == expected.Cpu && candidate.Duration == expected.Duration &&
                candidate.Discriminator == expected.Discriminator && SameAddresses(candidate, expected));
            if (index < 0) index = candidates.FindIndex(candidate => SameAddresses(candidate, expected));
            if (index < 0) index = 0;
            var result = candidates[index];
            candidates.RemoveAt(index);
            return result;
        }

        private static void CompareLegacyOracle(Sample expected, OracleFrame[] oracle, ComparisonReport report)
        {
            if (expected.Frames.Count != oracle.Length)
            {
                report.Add("LegacyFrameCoverage", expected.Key, -1, null, null, $"XML definitions={oracle.Length}, legacy decoded={expected.Frames.Count}");
                return;
            }
            for (int index = 0; index < oracle.Length; index++)
                if (FrameAddress(expected.Frames[index]) != oracle[index].Address || expected.Frames[index]?.Function?.Name != oracle[index].Name)
                    report.Add("LegacyOracleMismatch", expected.Key, index, oracle[index].Name, expected.Frames[index]?.Function?.Name, $"PC=0x{oracle[index].Address:x}");
        }

        private static void CompareSamples(Sample expected, Sample actual, OracleFrame[] oracle, SymbolCatalog catalog, ComparisonReport report)
        {
            var table = report.Table(expected.Key.Schema);
            table.MatchedEvents++;
            if (expected.Cpu != actual.Cpu || expected.Duration != actual.Duration || expected.Discriminator != actual.Discriminator)
                report.Add("EventFieldsMismatch", expected.Key, -1, $"{expected.Cpu}|{expected.Duration}|{expected.Discriminator}",
                    $"{actual.Cpu}|{actual.Duration}|{actual.Discriminator}", "Non-identity event fields differ; still comparing the stack.");
            if (expected.Frames.Count != actual.Frames.Count)
                report.Add("FrameCountMismatch", expected.Key, -1, expected.Frames.Count.ToString(), actual.Frames.Count.ToString(), "Recorded stack lengths differ.");
            var context = new SymbolContext(expected.Key.Run, actual.Key.Process, actual.Key.Timestamp);
            int common = Math.Min(oracle.Length, actual.Frames.Count);
            for (int index = 0; index < common; index++)
            {
                var reference = oracle[index];
                var frame = actual.Frames[index];
                var recordedAddress = FrameAddress(frame);
                if (!recordedAddress.HasValue)
                {
                    report.Add("MissingTraceFrame", expected.Key, index, $"0x{reference.Address:x}", null, "Direct loader returned a null frame or address.");
                    continue;
                }
                ulong address = recordedAddress.Value;
                if (reference.Address != address)
                {
                    report.Add("FrameAddressMismatch", expected.Key, index, $"0x{reference.Address:x}", $"0x{address:x}", "Frame order/address differs.");
                    continue;
                }
                table.MatchingFrameAddresses++;
                var result = catalog.ResolveFrame(address, context, index);
                string details = $"PC=0x{address:x}; expected UUID={reference.Uuid}; actual UUID={result.Image?.Uuid}; coordinate=0x{result.Coordinate:x}; status={result.Status}";
                if (reference.Uuid.HasValue && reference.Uuid != result.Image?.Uuid)
                    report.Add("ImageUuidMismatch", expected.Key, index, reference.Uuid.ToString(), result.Image?.Uuid.ToString(), details);
                if (reference.Module.Length != 0 && reference.Module != frame.Module?.Name)
                    report.Add("ModuleMismatch", expected.Key, index, reference.Module, frame.Module?.Name, details);
                if (reference.LoadAddress.HasValue && result.Image != null)
                {
                    var text = result.Image.Segments.Where(segment => segment.Name == "__TEXT").ToArray();
                    if (text.Length != 1 || text[0].Address != reference.LoadAddress.Value)
                        report.Add("LoadAddressMismatch", expected.Key, index, $"0x{reference.LoadAddress:x}", text.Length == 1 ? $"0x{text[0].Address:x}" : null, details);
                }
                if (reference.Architecture.Length != 0 && result.Image != null)
                {
                    string architecture = (result.Image.CpuType, result.Image.CpuSubtype & 0x00ffffff) switch
                    {
                        (0x100000c, 0) => "arm64", (0x100000c, 2) => "arm64e",
                        (0x1000007, 3) => "x86_64", (0x1000007, 8) => "x86_64h", _ => "unsupported",
                    };
                    if (architecture != reference.Architecture)
                        report.Add("ArchitectureMismatch", expected.Key, index, reference.Architecture, architecture, details);
                }
                string category = NameCategory(reference.Name, frame?.Function?.Name ?? "", result.Status);
                report.Add(category, expected.Key, index, reference.Name, frame?.Function?.Name, details);
            }
        }

        internal static string NameCategory(string expected, string actual, SymbolStatus status)
        {
            bool expectedKnown = !string.IsNullOrEmpty(expected) && !expected.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
                expected != "Unknown" && expected != "NA";
            if (!expectedKnown) return status == SymbolStatus.Named ? "ImprovedSymbol" : "BothUnresolved";
            if (status != SymbolStatus.Named) return "LostSymbol";
            if (expected == actual) return "ExactName";
            if (actual == "_" + expected) return "LeadingUnderscoreDifference";
            return "SymbolNameMismatch";
        }

        private sealed class ComparisonReport
        {
            public string TracePath { get; init; } = "";
            public string XmlPath { get; init; } = "";
            public string XmlSha256 { get; init; } = "";
            public string MetadataSha256 { get; init; } = "";
            public bool Completed { get; set; }
            public double ElapsedSeconds { get; set; }
            public Dictionary<string, TableCounts> Tables { get; } = new(StringComparer.Ordinal);
            public Dictionary<string, long> Categories { get; } = new(StringComparer.Ordinal);
            public Dictionary<string, List<Difference>> Examples { get; } = new(StringComparer.Ordinal);
            public long Failures => Categories.Where(pair => !IsInformational(pair.Key)).Sum(pair => pair.Value);
            public static bool IsInformational(string category) => category == "ExactName" || category == "BothUnresolved";
            public TableCounts Table(string name)
            {
                if (!Tables.TryGetValue(name, out var counts)) Tables[name] = counts = new TableCounts();
                return counts;
            }
            public void Add(string category, EventKey? key, int frameIndex, string? expected, string? actual, string detail)
            {
                Categories.TryGetValue(category, out long count);
                Categories[category] = count + 1;
                if (IsInformational(category)) return;
                if (!Examples.TryGetValue(category, out var examples)) Examples[category] = examples = new List<Difference>();
                if (examples.Count < 20) examples.Add(new Difference(key, frameIndex, expected, actual, detail));
            }
        }

        private sealed class TableCounts
        {
            public long XmlEvents { get; set; }
            public long TraceEvents { get; set; }
            public long MatchedEvents { get; set; }
            public long XmlFrames { get; set; }
            public long TraceFrames { get; set; }
            public long LegacyFrames { get; set; }
            public long MatchingFrameAddresses { get; set; }
        }

        private sealed record Difference(EventKey? Event, int Frame, string? Expected, string? Actual, string Detail);

        private sealed class Sink : ISourceDataProcessor<Event, ParsingContext, Type>
        {
            private readonly Action<Event> receive;
            public Sink(Action<Event> receive) => this.receive = receive;
            public DataProcessingResult ProcessDataElement(Event data, ParsingContext context, CancellationToken cancellationToken)
            {
                receive(data);
                return DataProcessingResult.Processed;
            }
        }

        private static string HashFile(string path)
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream));
        }

        private static XmlReader OpenXml(string path)
        {
            var source = new Microsoft.Performance.SDK.Processing.FileDataSource(path);
            return (XmlReader)typeof(TraceSourceParser).GetMethod("GetXmlReader", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, new object[] { source, new NullProgress() })!;
        }

        private sealed class NullProgress : IProgress<int>
        {
            public void Report(int value) { }
        }

        public sealed class PairFactAttribute : FactAttribute
        {
            public PairFactAttribute()
            {
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("INSTRUMENTS_DIFF_TRACE")) &&
                    string.IsNullOrEmpty(Environment.GetEnvironmentVariable("INSTRUMENTS_DIFF_XML")))
                    Skip = "Set INSTRUMENTS_DIFF_TRACE and INSTRUMENTS_DIFF_XML to a matching capture/export pair.";
            }
        }
    }

    [CollectionDefinition("Trace symbol parity", DisableParallelization = true)]
    public sealed class TraceSymbolParityCollection { }
}