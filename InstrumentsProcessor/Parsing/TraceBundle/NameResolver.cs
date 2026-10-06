// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace InstrumentsProcessor.Parsing.TraceBundle
{
    /// <summary>
    /// Resolves process IDs to names using the process-info store
    /// inside a .trace bundle.
    /// </summary>
    internal static class NameResolver
    {
        private const int HeaderPage = 0x1000;

        public static Dictionary<long, string> BuildPidNameMap(
            List<StoreInfo> stores, Uniquing uniquing)
        {
            return BuildPidNameMap(stores, uniquing, out _);
        }

        /// <summary>
        /// Build both a PID→name map and a processRef→(pid,name) map.
        /// The ref map allows GetOrCreateProcess to get the correct 32-bit PID
        /// from process-info's __cat1__ field, which is authoritative.
        /// </summary>
        public static Dictionary<long, string> BuildPidNameMap(
            List<StoreInfo> stores, Uniquing uniquing,
            out Dictionary<int, (long pid, string name)> refMap)
        {
            var map = new Dictionary<long, string>();
            refMap = new Dictionary<int, (long pid, string name)>();

            foreach (var s in stores)
            {
                if (s.SchemaName != "process-info") continue;
                var schema = new StoreSchema(s.StorePath, s.SchemaName);
                if (schema.RowSize == 0) continue;

                // Use BulkstoreReader to handle all field offsets properly
                foreach (var row in BulkstoreReader.ReadRows(s.StorePath, schema))
                {
                    // Extract the process-name string ref
                    if (!row.TryGetValue("process-name", out var pnameObj)) continue;
                    int pnameRef = Convert.ToInt32(pnameObj);
                    string name = uniquing.GetString(pnameRef);
                    if (string.IsNullOrEmpty(name)) continue;

                    // Extract the PID from cat1 (topology category field — 32-bit PID)
                    long rawPid = -1;
                    if (row.TryGetValue("__cat1__", out var cat1Obj))
                    {
                        rawPid = (long)(uint)Convert.ToUInt64(cat1Obj);
                        map[rawPid] = name;
                    }

                    // Map the process uniquing ref to the authoritative cat1 PID
                    if (row.TryGetValue("process", out var processObj))
                    {
                        int processRef = Convert.ToInt32(processObj);
                        if (rawPid >= 0)
                        {
                            refMap[processRef] = (rawPid, name);
                        }

                        // Also store resolved PID for backward compatibility
                        long resolvedPid = uniquing.ResolveProcess(processRef);
                        if (!map.ContainsKey(resolvedPid))
                            map[resolvedPid] = name;
                    }
                }
            }
            return map;
        }

        /// <summary>
        /// Build a threadRef → (tid, pid) map from the thread-info store.
        /// The thread-info store has a 'tid' data field with the authoritative
        /// thread ID, and a 'thread' field with the uniquing ref.
        /// This avoids relying on ResolveThread for large uniquing entries
        /// where entry[0] is not the TID.
        /// </summary>
        public static Dictionary<int, (long tid, long pid, string name)> BuildThreadRefMap(
            List<StoreInfo> stores, Uniquing uniquing)
        {
            var map = new Dictionary<int, (long tid, long pid, string name)>();
            foreach (var s in stores)
            {
                if (s.SchemaName != "thread-info") continue;
                var schema = new StoreSchema(s.StorePath, s.SchemaName);
                if (schema.RowSize == 0) continue;

                foreach (var row in BulkstoreReader.ReadRows(s.StorePath, schema))
                {
                    if (!row.TryGetValue("thread", out var threadObj)) continue;
                    int threadRef = Convert.ToInt32(threadObj);

                    // Get the raw TID from the 'tid' data field (U64)
                    long tid = -1;
                    if (row.TryGetValue("tid", out var tidObj))
                        tid = (long)Convert.ToUInt64(tidObj);

                    // Get the PID: prefer process-info __cat1__, but thread-info
                    // has a 'process' ref we can resolve via the process refMap
                    long pid = -1;
                    if (row.TryGetValue("__cat1__", out var cat1Obj))
                    {
                        long cat1 = (long)(uint)Convert.ToUInt64(cat1Obj);
                        if (cat1 > 0) pid = cat1;
                    }
                    if (pid <= 0 && row.TryGetValue("process", out var procObj))
                    {
                        int procRef = Convert.ToInt32(procObj);
                        pid = uniquing.ResolveProcess(procRef);
                    }

                    // Resolve the thread name. The thread-info 'name' column
                    // (XRThreadNameTypeID) is a 64-bit value whose HIGH 32 bits are
                    // the index of the thread name in the global strings table.
                    // A value of 0xFFFFFFFFFFFFFFFF means the thread has no name.
                    string name = null;
                    if (row.TryGetValue("name", out var nameObj) && nameObj != null)
                    {
                        ulong rawName = Convert.ToUInt64(nameObj);
                        if (rawName != ulong.MaxValue)
                        {
                            int nameStrIdx = (int)(rawName >> 32);
                            if (nameStrIdx >= 0 && nameStrIdx < uniquing.Strings.Count)
                            {
                                string resolved = uniquing.GetString(nameStrIdx);
                                if (!string.IsNullOrEmpty(resolved)) name = resolved;
                            }
                        }
                    }

                    // Name-less main threads are shown as "Main Thread" (matches Instruments).
                    if (string.IsNullOrEmpty(name) &&
                        row.TryGetValue("main-thread", out var mainObj) && mainObj != null &&
                        Convert.ToInt64(mainObj) != 0)
                    {
                        name = "Main Thread";
                    }

                    if (tid >= 0)
                    {
                        // Keep the first non-empty name we see for a given thread ref.
                        if (map.TryGetValue(threadRef, out var existing) && string.IsNullOrEmpty(name))
                            name = existing.name;
                        map[threadRef] = (tid, pid, name);
                    }
                }
            }
            return map;
        }
    }
}