// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using InstrumentsProcessor.Parsing.DataModels;
using InstrumentsProcessor.Parsing.Events;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Boolean = InstrumentsProcessor.Parsing.DataModels.Boolean;
using String = InstrumentsProcessor.Parsing.DataModels.String;
using UInt64 = InstrumentsProcessor.Parsing.DataModels.UInt64;

namespace InstrumentsProcessor.Parsing.TraceBundle
{
    /// <summary>
    /// Maps binary .trace bundle schema names to Event types and converts
    /// resolved bulkstore rows into Event objects consumed by the cookers.
    /// </summary>
    internal class TraceBundleEventFactory
    {
        // Schema name → Event type
        private static readonly Dictionary<string, Type> SchemaToEventType = new Dictionary<string, Type>
        {
            ["time-profile"] = typeof(TimeProfileEvent),
            ["counters-profile"] = typeof(CountersProfileEvent),
            ["thread-state"] = typeof(ThreadStateEvent),
            ["device-thermal-state-intervals"] = typeof(DeviceThermalStateIntervalEvent),
            ["syscall"] = typeof(SyscallEvent),
            ["virtual-memory"] = typeof(VirtualMemoryEvent),
            ["potential-hangs"] = typeof(PotentialHangEvent),
            ["cpu-profile"] = typeof(CpuProfileEvent),
            ["metal-gpu-intervals"] = typeof(MetalGpuIntervalEvent),
            ["display-vsyncs-interval"] = typeof(DisplayVsyncIntervalEvent),
            ["life-cycle-period"] = typeof(LifeCyclePeriodEvent),
            ["ane-hw-intervals-internal"] = typeof(AneHwIntervalEvent),
            ["syscall-name-map"] = typeof(SyscallNameMapEvent),
            ["os-signpost"] = typeof(OsSignpostEvent),
            ["csr-switch-on"] = typeof(CsrSwitchEvent),
            ["csr-switch-off"] = typeof(CsrSwitchEvent),
            ["dil-disk-io"] = typeof(DiskIoEvent),
            ["vml-vm-fault"] = typeof(VmFaultEvent),
            ["bb-signpost-event"] = typeof(BbSignpostEvent),
            ["activity-monitor-process-live"] = typeof(ActivityMonitorProcessEvent),
            ["activity-monitor-system"] = typeof(ActivityMonitorSystemEvent),
        };

        // Classification of binary engineeringType → how to resolve the raw value
        internal enum ValueKind
        {
            Timestamp,
            Duration,
            Thread,
            Process,
            Backtrace,
            StringRef,
            IntegerVal,
            UInt64Val,
            BooleanVal,
            PmcEvents,
            Unknown
        }

        // Cached column mapping per (schema name, event type) pair
        internal class ColumnMapping
        {
            public PropertyInfo Property { get; set; }
            public string RowKey { get; set; } // "timestamp", "duration", or a mnemonic
            public ValueKind Kind { get; set; }
            public string EngineeringType { get; set; }
            public bool Kernel { get; set; }
        }

        public static bool IsSchemaSupported(string schemaName) =>
            SchemaToEventType.ContainsKey(schemaName);

        /// <summary>
        /// Try to build a mapping from a binary store's columns to event properties.
        /// Returns null if the schema can't be matched to a known event type.
        /// </summary>
        internal static (Type eventType, List<ColumnMapping> mappings)? TryBuildMapping(
            string schemaName, StoreSchema schema)
        {
            if (!SchemaToEventType.TryGetValue(schemaName, out var eventType))
                return null;

            // Get ColumnAttribute properties from the event type
            var propsByName = new Dictionary<string, PropertyInfo>();
            foreach (var prop in eventType.GetProperties())
            {
                var attr = prop.GetCustomAttribute<ColumnAttribute>();
                if (attr != null)
                    propsByName[attr.Name] = prop;
            }

            var mappings = new List<ColumnMapping>();

            foreach (var col in schema.Columns)
            {
                // Match by engineeringName (display name) → ColumnAttribute.Name
                if (!propsByName.TryGetValue(col.Name, out var property))
                {
                    // Column not recognized — skip it rather than failing the whole schema
                    continue;
                }

                string rowKey;
                if (!string.IsNullOrEmpty(col.TopoField))
                {
                    // Map Apple topology field IDs to row dictionary keys.
                    // BulkstoreReader writes: "timestamp", "duration", "__cat1__", "__cat2__".
                    var tf = col.TopoField;
                    if (tf == "XRTraceRelativeTimestampFieldID" || tf == "start")
                        rowKey = "timestamp";
                    else if (tf == "XRDurationFieldID" || tf == "duration")
                        rowKey = "duration";
                    else if (tf == "XRCategory1FieldID")
                        rowKey = "__cat1__";
                    else if (tf == "XRCategory2FieldID")
                        rowKey = "__cat2__";
                    else if (tf == "end")
                        rowKey = "__computed_end__";
                    else
                        rowKey = col.Mnemonic;
                }
                else
                {
                    rowKey = col.Mnemonic;
                }

                var kind = ClassifyPropertyType(property.PropertyType);

                mappings.Add(new ColumnMapping
                {
                    Property = property,
                    RowKey = rowKey,
                    Kind = kind,
                    EngineeringType = col.EngineeringType,
                    Kernel = col.Mnemonic == "cp-kernel-callstack"
                });
            }

            // Must have at least one mapped column
            if (mappings.Count == 0)
                return null;

            return (eventType, mappings);
        }

        private static ValueKind ClassifyPropertyType(Type propertyType)
        {
            if (propertyType == typeof(Timestamp)) return ValueKind.Timestamp;
            if (propertyType == typeof(TimestampDelta)) return ValueKind.Duration;
            if (propertyType == typeof(Thread)) return ValueKind.Thread;
            if (propertyType == typeof(Process)) return ValueKind.Process;
            if (propertyType == typeof(Backtrace)) return ValueKind.Backtrace;
            if (propertyType == typeof(String)) return ValueKind.StringRef;
            if (propertyType == typeof(Integer)) return ValueKind.IntegerVal;
            if (propertyType == typeof(UInt64)) return ValueKind.UInt64Val;
            if (propertyType == typeof(Boolean)) return ValueKind.BooleanVal;
            if (propertyType == typeof(PmcEvents)) return ValueKind.PmcEvents;
            return ValueKind.Unknown;
        }

        /// <summary>
        /// Caches frequently-allocated data model objects so that events sharing the
        /// same thread, process, string, timestamp-delta, integer, boolean, or backtrace
        /// value reuse a single instance instead of creating millions of duplicates.
        /// For a trace with 5M+ time-profile or thread-state events, this can save
        /// hundreds of megabytes of heap.
        /// </summary>
        internal class InternCache
        {
            // Cache Thread/Process by uniquing refIdx so we never call
            // uniquing.ResolveThread/ResolveProcess more than once per unique ref.
            private readonly Dictionary<int, Thread> _threadsByRef = new Dictionary<int, Thread>();
            private readonly Dictionary<int, Process> _processesByRef = new Dictionary<int, Process>();
            private readonly Dictionary<string, String> _strings = new Dictionary<string, String>();
            private readonly Dictionary<long, TimestampDelta> _deltas = new Dictionary<long, TimestampDelta>();
            private readonly Dictionary<int, Integer> _integers = new Dictionary<int, Integer>();
            private readonly Dictionary<(int Reference, string Encoding), DecodedBacktrace> _rawBacktraces = new();
            private readonly Dictionary<(int Reference, string Encoding, ImageScope Scope, int Generation, bool Kernel), Backtrace> _backtraces = new();
            private static readonly Boolean TrueVal = new Boolean(true);
            private static readonly Boolean FalseVal = new Boolean(false);
            private static readonly PmcEvents EmptyPmc = new PmcEvents(new Dictionary<string, long>());

            public Thread GetOrCreateThread(int refIdx, Uniquing uniquing,
                Dictionary<long, string> pidNames,
                Dictionary<int, (long pid, string name)> refMap = null,
                Dictionary<int, (long tid, long pid, string name)> threadRefMap = null)
            {
                if (!_threadsByRef.TryGetValue(refIdx, out var thread))
                {
                    if ((uint)refIdx == 0xFFFFFFFF)
                    {
                        thread = new Thread(0, "Unknown", GetOrCreateProcessByPid(0, "Unknown"));
                    }
                    else
                    {
                        long tid, pid;
                        string resolvedThreadName = null;
                        if (threadRefMap != null && threadRefMap.TryGetValue(refIdx, out var tinfo))
                        {
                            // Use authoritative TID/PID from thread-info store
                            tid = tinfo.tid;
                            pid = tinfo.pid;
                            resolvedThreadName = tinfo.name;
                        }
                        else
                        {
                            var resolved = uniquing.ResolveThread(refIdx);
                            tid = (long)resolved.tid;
                            pid = resolved.pid;
                        }
                        string processName;
                        if (pidNames.TryGetValue(pid, out var pn))
                            processName = pn;
                        else if (refMap != null)
                        {
                            // Try to find the process name via the process ref map
                            // ResolveThread returns a resolved pid that may need refMap
                            processName = $"pid:{pid}";
                        }
                        else
                            processName = $"pid:{pid}";
                        // Prefer the authoritative thread name. Name-less threads
                        // (kernel/system threads) have no symbolic name in the
                        // thread-info store; fall back to the process name so the
                        // Thread Name column shows a meaningful value instead of the
                        // raw hex tid. Only use the hex tid as a last resort.
                        string threadName;
                        if (!string.IsNullOrEmpty(resolvedThreadName))
                        {
                            threadName = resolvedThreadName;
                        }
                        else if (!string.IsNullOrEmpty(processName) && !processName.StartsWith("pid:"))
                        {
                            threadName = processName;
                        }
                        else
                        {
                            threadName = $"0x{tid:x}";
                        }
                        thread = new Thread((int)tid, threadName, GetOrCreateProcessByPid(pid, processName));
                    }
                    _threadsByRef[refIdx] = thread;
                }
                return thread;
            }

            public Process GetOrCreateProcess(int refIdx, Uniquing uniquing,
                Dictionary<long, string> pidNames,
                Dictionary<int, (long pid, string name)> refMap = null)
            {
                if (!_processesByRef.TryGetValue(refIdx, out var process))
                {
                    long pid;
                    string processName;

                    if ((uint)refIdx == 0xFFFFFFFF)
                    {
                        pid = 0;
                        processName = "Unknown";
                    }
                    else if (refMap != null && refMap.TryGetValue(refIdx, out var info))
                    {
                        // Use the authoritative PID from process-info's __cat1__
                        pid = info.pid;
                        processName = info.name;
                    }
                    else
                    {
                        pid = uniquing.ResolveProcess(refIdx);
                        if (pid < 0)
                        {
                            pid = 0;
                            processName = "Unknown";
                        }
                        else if (pidNames.TryGetValue(pid, out var pn))
                        {
                            processName = pn;
                        }
                        else
                        {
                            processName = uniquing.ResolveProcessName(refIdx) ?? $"pid:{pid}";
                        }
                    }
                    process = new Process((int)pid, processName);
                    _processesByRef[refIdx] = process;
                }
                return process;
            }

            // Internal helper for Thread creation — avoids duplicate Process objects
            private readonly Dictionary<long, Process> _processesByPid = new Dictionary<long, Process>();
            private Process GetOrCreateProcessByPid(long pid, string processName)
            {
                if (!_processesByPid.TryGetValue(pid, out var process))
                {
                    process = new Process((int)pid, processName);
                    _processesByPid[pid] = process;
                }
                return process;
            }

            public String GetOrCreateString(string value)
            {
                if (!_strings.TryGetValue(value, out var str))
                {
                    str = new String(value);
                    _strings[value] = str;
                }
                return str;
            }

            public TimestampDelta GetOrCreateDelta(long nanoseconds)
            {
                if (!_deltas.TryGetValue(nanoseconds, out var delta))
                {
                    delta = new TimestampDelta(nanoseconds);
                    _deltas[nanoseconds] = delta;
                }
                return delta;
            }

            public Integer GetOrCreateInteger(int value)
            {
                if (!_integers.TryGetValue(value, out var integer))
                {
                    integer = new Integer(value);
                    _integers[value] = integer;
                }
                return integer;
            }

            public Boolean GetBoolean(bool value) => value ? TrueVal : FalseVal;

            public PmcEvents GetEmptyPmc() => EmptyPmc;

            public Backtrace GetOrCreateBacktrace(int refIdx, Uniquing uniquing, SymbolCatalog symbols,
                SymbolContext context = null, string engineeringType = "backtrace")
            {
                var rawKey = (refIdx, engineeringType);
                if (!_rawBacktraces.TryGetValue(rawKey, out var decoded))
                    _rawBacktraces[rawKey] = decoded = uniquing.DecodeBacktrace(refIdx, engineeringType);
                context = ContextForBacktrace(decoded, context);
                var scope = symbols?.Mappings.GetScope(context);
                var key = (refIdx, engineeringType, scope, scope?.GetGeneration(context.Timestamp) ?? -1, context?.Kernel ?? false);
                if (!_backtraces.TryGetValue(key, out var bt))
                {
                    bt = new LazyBacktrace(decoded.Addresses, symbols, context);
                    _backtraces[key] = bt;
                }
                return bt;
            }
        }

        internal static SymbolContext ContextForBacktrace(DecodedBacktrace decoded, SymbolContext context)
        {
            if (context == null || context.Kernel || !decoded.ProcessId.HasValue) return context;
            return context with { ProcessId = context.ProcessId < 0 || context.ProcessId == decoded.ProcessId.Value
                ? decoded.ProcessId.Value : long.MinValue };
        }

        internal static SymbolContext GetSymbolContext(Dictionary<string, object> row, StoreSchema schema,
            Uniquing uniquing, int run,
            Dictionary<int, (long pid, string name)> processes = null,
            Dictionary<int, (long tid, long pid, string name)> threads = null)
        {
            long pid = -1;
            foreach (var column in schema.Columns)
            {
                bool processColumn = column.EngineeringType == "XRProcessTypeID";
                bool threadColumn = column.EngineeringType == "XRThreadTypeID";
                if (!processColumn && !threadColumn) continue;
                string key = column.TopoField == "XRCategory1FieldID" ? "__cat1__" :
                    column.TopoField == "XRCategory2FieldID" ? "__cat2__" : column.Mnemonic;
                if (!row.TryGetValue(key, out var value)) continue;
                int reference = RawToInt32(value);
                if (reference < 0) continue;
                if (processColumn)
                {
                    pid = processes != null && processes.TryGetValue(reference, out var process)
                        ? process.pid : uniquing.ResolveProcess(reference);
                    break;
                }
                pid = threads != null && threads.TryGetValue(reference, out var thread)
                    ? thread.pid : uniquing.ResolveThread(reference).pid;
            }
            long time = row.TryGetValue("timestamp", out var timestamp) ? checked((long)Convert.ToUInt64(timestamp)) : 0;
            return new SymbolContext(run, pid, time);
        }

        // Pre-compiled event factories to avoid Activator.CreateInstance overhead
        private static readonly Dictionary<Type, Func<Event>> EventFactories = new Dictionary<Type, Func<Event>>();
        private static Event CreateEventInstance(Type eventType)
        {
            if (!EventFactories.TryGetValue(eventType, out var factory))
            {
                factory = () => (Event)Activator.CreateInstance(eventType);
                EventFactories[eventType] = factory;
            }
            return factory();
        }

        /// <summary>
        /// Create an Event from a raw bulkstore row (before reference resolution).
        /// Uses the Uniquing data directly to produce rich data model objects.
        /// </summary>
        internal static Event CreateEvent(
            Type eventType,
            List<ColumnMapping> mappings,
            Dictionary<string, object> row,
            Uniquing uniquing,
            Dictionary<long, string> pidNames,
            StoreSchema schema,
            InternCache internCache,
            SymbolCatalog symbols = null,
            Dictionary<int, (long pid, string name)> refMap = null,
            Dictionary<int, (long tid, long pid, string name)> threadRefMap = null,
            int runNumber = 1,
            CsrSwitchStackLookup switchStacks = null)
        {
            var evt = CreateEventInstance(eventType);
            evt.SchemaName = schema.SchemaName;
            var symbolContext = mappings.Any(mapping => mapping.Kind == ValueKind.Backtrace)
                ? GetSymbolContext(row, schema, uniquing, runNumber, refMap, threadRefMap) : null;

            foreach (var mapping in mappings)
            {
                object rawValue;
                if (mapping.RowKey == "__computed_end__")
                {
                    // End = timestamp + duration
                    ulong ts = row.TryGetValue("timestamp", out var tsv) ? Convert.ToUInt64(tsv) : 0;
                    ulong dur = row.TryGetValue("duration", out var dv) ? Convert.ToUInt64(dv) : 0;
                    rawValue = ts + dur;
                }
                else if (!row.TryGetValue(mapping.RowKey, out rawValue))
                {
                    continue;
                }

                object dataModelValue = mapping.Kind == ValueKind.Backtrace
                    ? internCache.GetOrCreateBacktrace(RawToInt32(rawValue), uniquing, symbols,
                        symbolContext with { Kernel = mapping.Kernel }, mapping.EngineeringType)
                    : ConvertValue(mapping.Kind, rawValue, uniquing, pidNames, schema, internCache, symbols, refMap, threadRefMap);
                if (dataModelValue != null)
                {
                    mapping.Property.SetValue(evt, dataModelValue);
                }
            }

            if (evt is CsrSwitchEvent contextSwitch && switchStacks?.Find(schema.SchemaName, row) is CsrStackReferences stacks)
            {
                var context = GetSymbolContext(row, schema, uniquing, runNumber, refMap, threadRefMap);
                if (stacks.User >= 0)
                    contextSwitch.Stack = internCache.GetOrCreateBacktrace(stacks.User, uniquing, symbols, context, "XRCoreProfileCallstackTypeID");
                if (stacks.Kernel >= 0)
                    contextSwitch.KernelStack = internCache.GetOrCreateBacktrace(stacks.Kernel, uniquing, symbols,
                        context with { Kernel = true }, "XRCoreProfileCallstackTypeID");
            }

            return evt;
        }

        /// <summary>
        /// Safely convert a raw bulkstore value (uint or ulong) to int
        /// without throwing on sentinel values like 0xFFFFFFFF or large ulong values.
        /// </summary>
        private static int RawToInt32(object rawValue) =>
            unchecked((int)(uint)Convert.ToUInt64(rawValue));

        private static object ConvertValue(
            ValueKind kind, object rawValue,
            Uniquing uniquing, Dictionary<long, string> pidNames,
            StoreSchema schema, InternCache internCache,
            SymbolCatalog symbols = null,
            Dictionary<int, (long pid, string name)> refMap = null,
            Dictionary<int, (long tid, long pid, string name)> threadRefMap = null)
        {
            switch (kind)
            {
                case ValueKind.Timestamp:
                    return new Timestamp((long)Convert.ToUInt64(rawValue));

                case ValueKind.Duration:
                    return internCache.GetOrCreateDelta((long)Convert.ToUInt64(rawValue));

                case ValueKind.Thread:
                {
                    int refIdx = RawToInt32(rawValue);
                    return internCache.GetOrCreateThread(refIdx, uniquing, pidNames, refMap, threadRefMap);
                }

                case ValueKind.Process:
                {
                    int refIdx = RawToInt32(rawValue);
                    return internCache.GetOrCreateProcess(refIdx, uniquing, pidNames, refMap);
                }

                case ValueKind.Backtrace:
                {
                    int refIdx = RawToInt32(rawValue);
                    return internCache.GetOrCreateBacktrace(refIdx, uniquing, symbols);
                }

                case ValueKind.StringRef:
                {
                    int refIdx = RawToInt32(rawValue);
                    return internCache.GetOrCreateString(uniquing.GetString(refIdx));
                }

                case ValueKind.IntegerVal:
                    return internCache.GetOrCreateInteger(RawToInt32(rawValue));

                case ValueKind.UInt64Val:
                    return new UInt64(Convert.ToUInt64(rawValue));

                case ValueKind.BooleanVal:
                    return internCache.GetBoolean(Convert.ToUInt64(rawValue) != 0);

                case ValueKind.PmcEvents:
                    return internCache.GetEmptyPmc();

                default:
                    return null;
            }
        }
    }
}