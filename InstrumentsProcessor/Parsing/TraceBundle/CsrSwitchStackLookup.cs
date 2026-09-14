using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace InstrumentsProcessor.Parsing.TraceBundle
{
    internal sealed record CsrStackReferences(int User, int Kernel);

    internal sealed class CsrSwitchStackLookup
    {
        private readonly Dictionary<(ulong Time, uint Thread, uint Cpu, bool On, ulong Instructions, ulong Cycles), CsrStackReferences> stacks = new();

        public static CsrSwitchStackLookup Load(IEnumerable<StoreInfo> stores, CancellationToken cancellation)
        {
            var lookup = new CsrSwitchStackLookup();
            foreach (string path in stores.Where(store => store.Side == 0 && store.SchemaName == "kdebug")
                .Select(store => Path.GetFullPath(store.StorePath)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                cancellation.ThrowIfCancellationRequested();
                var schema = new StoreSchema(path, "kdebug");
                if (!schema.Fields.Any(field => field.Name == "cp-user-callstack" || field.Name == "cp-kernel-callstack")) continue;
                foreach (var row in BulkstoreReader.ReadRows(path, schema))
                {
                    cancellation.ThrowIfCancellationRequested();
                    lookup.Add(row);
                }
            }
            return lookup;
        }

        public void Add(Dictionary<string, object> row)
        {
            if (!TryRead(row, "class", out var eventClass) || eventClass != 12 ||
                !TryRead(row, "subclass", out var subclass) || (subclass != 1 && subclass != 5) ||
                !TryRead(row, "code", out var code) || code != 1 ||
                !TryRead(row, "function", out var function) || function != 0 ||
                !TryKey(row, true, subclass == 5, out var key)) return;

            var references = new CsrStackReferences(ReadReference(row, "cp-user-callstack"), ReadReference(row, "cp-kernel-callstack"));
            if (references.User < 0 && references.Kernel < 0) return;
            if (stacks.TryGetValue(key, out var previous) && previous != references)
                stacks[key] = null;
            else
                stacks[key] = references;
        }

        public CsrStackReferences Find(string schema, Dictionary<string, object> row)
        {
            if (schema != "csr-switch-on" && schema != "csr-switch-off") return null;
            return TryKey(row, false, schema == "csr-switch-on", out var key) && stacks.TryGetValue(key, out var references)
                ? references : null;
        }

        private static bool TryKey(Dictionary<string, object> row, bool raw, bool on,
            out (ulong Time, uint Thread, uint Cpu, bool On, ulong Instructions, ulong Cycles) key)
        {
            key = default;
            if (!TryRead(row, "timestamp", out var time) ||
                !TryRead(row, raw ? "__cat1__" : "thread", out var thread) || thread >= uint.MaxValue ||
                !TryRead(row, raw ? "core-index" : "cpu", out var cpu) || cpu >= uint.MaxValue ||
                !TryRead(row, raw ? "arg1" : "instructions", out var instructions) ||
                !TryRead(row, raw ? "arg2" : "cycles", out var cycles)) return false;
            key = (time, (uint)thread, (uint)cpu, on, instructions, cycles);
            return true;
        }

        private static int ReadReference(Dictionary<string, object> row, string field) =>
            TryRead(row, field, out var value) && value <= int.MaxValue ? (int)value : -1;

        private static bool TryRead(Dictionary<string, object> row, string field, out ulong value)
        {
            value = 0;
            if (!row.TryGetValue(field, out var raw) || raw == null) return false;
            value = Convert.ToUInt64(raw);
            return true;
        }
    }
}