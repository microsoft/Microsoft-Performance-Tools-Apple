using InstrumentsProcessor.Parsing.TraceBundle;
using InstrumentsProcessor.Parsing.DataModels;
using InstrumentsProcessor.Parsing.Events;
using InstrumentsProcessor.Tables;
using InstrumentsProcessor.Cookers;
using InstrumentsProcessor.AccessProviders;
using Microsoft.Performance.Toolkit.Engine;
using Microsoft.Performance.SDK.Extensibility;
using Microsoft.Performance.SDK.Processing;

namespace InstrumentsProcessorTests
{
    public class CsrSwitchStackTests
    {
        [Fact]
        public void JoinsOnlyExactSwitchIdentityAndCounters()
        {
            var lookup = new CsrSwitchStackLookup();
            lookup.Add(RawRow());
            var modeled = ModeledRow();
            Assert.Equal(new CsrStackReferences(10, -1), lookup.Find("csr-switch-on", modeled));
            Assert.Null(lookup.Find("csr-switch-off", modeled));
            foreach (string field in new[] { "timestamp", "thread", "cpu", "instructions", "cycles" })
            {
                var different = ModeledRow();
                different[field] = Convert.ToUInt64(different[field]) + 1;
                Assert.Null(lookup.Find("csr-switch-on", different));
            }
        }

        [Fact]
        public void IgnoresNonSwitchEventsAndPreservesMissingStacks()
        {
            var lookup = new CsrSwitchStackLookup();
            var raw = RawRow();
            raw["class"] = 1U;
            lookup.Add(raw);
            Assert.Null(lookup.Find("csr-switch-on", ModeledRow()));
            raw = RawRow();
            raw["cp-user-callstack"] = uint.MaxValue;
            lookup.Add(raw);
            Assert.Null(lookup.Find("csr-switch-on", ModeledRow()));
        }

        [Fact]
        public void ConflictingReferencesNeverSelectAnArbitraryStack()
        {
            var lookup = new CsrSwitchStackLookup();
            lookup.Add(RawRow());
            lookup.Add(RawRow());
            Assert.NotNull(lookup.Find("csr-switch-on", ModeledRow()));
            var conflict = RawRow();
            conflict["cp-user-callstack"] = 11U;
            lookup.Add(conflict);
            lookup.Add(RawRow());
            Assert.Null(lookup.Find("csr-switch-on", ModeledRow()));
        }

        [Fact]
        public void IntervalsKeepSwitchOnStacksWithoutChangingCounterDeltas()
        {
            var firstStack = new Backtrace(new[] { new Frame(new Function("first", "0x1000")) });
            var secondStack = new Backtrace(new[] { new Frame(new Function("second", "0x2000")) });
            CsrSwitchEvent Event(long time, bool on, ulong counter, Backtrace? stack) => new()
            {
                SchemaName = on ? "csr-switch-on" : "csr-switch-off",
                Time = new Timestamp(time), Cpu = new InstrumentsProcessor.Parsing.DataModels.UInt64(0),
                Instructions = new InstrumentsProcessor.Parsing.DataModels.UInt64(counter),
                Cycles = new InstrumentsProcessor.Parsing.DataModels.UInt64(counter * 2),
                Stack = stack,
                KernelStack = stack,
            };
            var intervals = CsrSwitchTable.BuildIntervals(new List<CsrSwitchEvent>
            {
                Event(10, true, 10, firstStack), Event(20, false, 30, null),
                Event(30, true, 40, secondStack), Event(40, false, 70, null),
            });
            Assert.Equal(2, intervals.Count);
            Assert.Same(firstStack, intervals[0].Stack);
            Assert.Same(firstStack, intervals[0].KernelStack);
            Assert.Same(secondStack, intervals[1].Stack);
            Assert.Same(secondStack, intervals[1].KernelStack);
            Assert.Equal(20UL, intervals[0].DeltaInstructions);
            Assert.Equal(60UL, intervals[1].DeltaCycles);
        }

        [Fact]
        public void SwitchOffStacksRemainSeparateAndReferenceZeroIsValid()
        {
            var lookup = new CsrSwitchStackLookup();
            var raw = RawRow();
            raw["subclass"] = 1U;
            raw["cp-user-callstack"] = 0U;
            raw["cp-kernel-callstack"] = 2U;
            lookup.Add(raw);
            Assert.Equal(new CsrStackReferences(0, 2), lookup.Find("csr-switch-off", ModeledRow()));
            Assert.Null(lookup.Find("csr-switch-on", ModeledRow()));
        }

        [SymbolResolutionTests.LocalTraceFact]
        public void RecordedContextSwitchTablesExposeSymbolizedStacks()
        {
            string trace = Environment.GetEnvironmentVariable("INSTRUMENTS_TEST_TRACE")!;
            string pluginPath = Path.GetDirectoryName(typeof(InstrumentsProcessor.InstrumentsProcessingSource).Assembly.Location)!;
            using var plugins = PluginSet.Load(pluginPath);
            using var sources = DataSourceSet.Create(plugins);
            sources.AddFile(Path.Combine(trace, "open.creq"));
            using var engine = Engine.Create(new EngineCreateInfo(sources.AsReadOnly()));
            engine.EnableCooker(CsrSwitchCooker.DataCookerPath);
            engine.EnableTable(CsrSwitchTable.TableDescriptor);
            engine.EnableTable(CsrSwitchRawTable.TableDescriptor);
            var results = engine.Process();
            var events = results.QueryOutput<List<CsrSwitchEvent>>(new DataOutputPath(
                CsrSwitchCooker.DataCookerPath, nameof(CsrSwitchCooker.CsrSwitchEvents)));
            Assert.Equal(93660, events.Count);
            var populated = events.Where(evt => evt.IsSwitchOn && evt.Stack != null).ToArray();
            Assert.Equal(16196, populated.Length);
            Assert.All(populated, evt => Assert.NotEmpty(evt.Stack.Frames));
            var recorded = Assert.Single(events.Where(evt => evt.IsSwitchOn && evt.Time.Value.ToNanoseconds == 4381683583 &&
                evt.Process.ProcessId.Value == 722 && evt.Cpu.Value == 0));
            Assert.NotNull(recorded.Stack);
            Assert.Equal(37, recorded.Stack.Frames.Count);
            Assert.Contains(recorded.Stack.Frames, frame => frame.Module?.Name == "libdispatch.dylib" &&
                frame.Function.Name == "__dispatch_client_callout" && frame.Function.Address == "0x18ec504b0");
            Assert.Null(recorded.KernelStack);

            var intervals = CsrSwitchTable.BuildIntervals(events);
            int intervalIndex = intervals.FindIndex(interval => interval.SwitchOnTime == recorded.Time.Value && interval.Cpu == 0);
            Assert.True(intervalIndex >= 0);
            var table = results.BuildTable(CsrSwitchTable.TableDescriptor);
            Assert.Equal(intervals.Count, table.RowCount);
            var stackColumn = Assert.Single(table.Columns.Where(column => column.Configuration.Metadata.Name == "Switch-On Stack"));
            Assert.Same(recorded.Stack, stackColumn.Project(intervalIndex));
            Assert.NotEmpty(table.ColumnVariants[stackColumn]);

            var rawTable = results.BuildTable(CsrSwitchRawTable.TableDescriptor);
            Assert.Equal(events.Count, rawTable.RowCount);
            var rawStackColumn = Assert.Single(rawTable.Columns.Where(column => column.Configuration.Metadata.Name == "Stack"));
            Assert.Same(recorded.Stack, rawStackColumn.Project(events.IndexOf(recorded)));
            var access = new StackAccessProvider();
            Assert.Contains("libdispatch.dylib!__dispatch_client_callout", Enumerable.Range(0, access.GetCount(recorded.Stack))
                .Select(index => access.GetValue(recorded.Stack, index)));
        }

        internal static Dictionary<string, object> RawRow() => new()
        {
            ["timestamp"] = 4381683583UL, ["__cat1__"] = 6U, ["core-index"] = 0U,
            ["class"] = 12U, ["subclass"] = 5U, ["code"] = 1U, ["function"] = 0U,
            ["arg1"] = 321953422562UL, ["arg2"] = 165780330940UL,
            ["cp-user-callstack"] = 10U, ["cp-kernel-callstack"] = uint.MaxValue,
        };

        internal static Dictionary<string, object> ModeledRow() => new()
        {
            ["timestamp"] = 4381683583UL, ["thread"] = 6U, ["process"] = 5U, ["cpu"] = 0U,
            ["instructions"] = 321953422562UL, ["cycles"] = 165780330940UL,
        };
    }
}