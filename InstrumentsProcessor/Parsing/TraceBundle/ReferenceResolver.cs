// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;

namespace InstrumentsProcessor.Parsing.TraceBundle
{
    /// <summary>
    /// Resolves raw bulkstore reference indices to human-readable values
    /// (strings, thread IDs, process IDs, backtraces) using the Uniquing data.
    /// </summary>
    internal static class ReferenceResolver
    {
        private static Dictionary<string, string> ClassifyColumns(StoreSchema schema)
        {
            var colTypes = new Dictionary<string, string>();
            foreach (var col in schema.Columns)
            {
                string m = col.Mnemonic, et = col.EngineeringType;
                if (et.Contains("Thread")) colTypes[m] = "thread";
                else if (et.Contains("Process")) colTypes[m] = "process";
                else if (et.Contains("Backtrace", StringComparison.OrdinalIgnoreCase))
                    colTypes[m] = "backtrace";
                else if (et.Contains("XRUUIDTypeID")) colTypes[m] = "uuid";
                else if (et.Contains("XRFilePath")) colTypes[m] = "string";
                else if (et.Contains("XRVirtualMemoryAddress")) colTypes[m] = "address";
                else if (et.Contains("String") || et.Contains("XRSignpostName") ||
                         et.Contains("XROSLogSubsystem") || et.Contains("XROSLogCategory") ||
                         et.Contains("XROSLogFormatString") || et.Contains("XROSLogMetadata") ||
                         et.Contains("XRReturnLocation"))
                    colTypes[m] = "string";
            }
            return colTypes;
        }

        public static IEnumerable<Dictionary<string, object>> Resolve(
            IEnumerable<Dictionary<string, object>> rows,
            StoreSchema schema, Uniquing uniquing)
        {
            var colTypes = ClassifyColumns(schema);

            foreach (var row in rows)
            {
                foreach (var (fname, ftype) in colTypes)
                {
                    if (!row.TryGetValue(fname, out var refObj)) continue;
                    int refVal = Convert.ToInt32(refObj);

                    switch (ftype)
                    {
                        case "thread":
                            var (tid, pid) = uniquing.ResolveThread(refVal);
                            row[fname + "_tid"] = tid;
                            row[fname + "_pid"] = pid;
                            row.Remove(fname);
                            break;
                        case "process":
                            row[fname + "_pid"] = uniquing.ResolveProcess(refVal);
                            row.Remove(fname);
                            break;
                        case "backtrace":
                            var arr = uniquing.GetArray(refVal);
                            row[fname] = arr != null
                                ? string.Join(";", arr.Select(a => $"0x{a:x}"))
                                : "";
                            break;
                        case "string":
                            row[fname] = uniquing.GetString(refVal);
                            break;
                        case "uuid":
                            var uarr = uniquing.GetArray(refVal);
                            if (uarr is { Length: >= 2 })
                            {
                                var raw = new byte[16];
                                BitConverter.GetBytes(uarr[0]).CopyTo(raw, 0);
                                BitConverter.GetBytes(uarr[1]).CopyTo(raw, 8);
                                row[fname] = FormatUuid(raw);
                            }
                            else row[fname] = $"<uuid:{refVal}>";
                            break;
                        case "address":
                            row[fname] = $"0x{refVal:x}";
                            break;
                    }
                }
                yield return row;
            }
        }

        private static string FormatUuid(byte[] b) =>
            $"{b[0]:X2}{b[1]:X2}{b[2]:X2}{b[3]:X2}-" +
            $"{b[4]:X2}{b[5]:X2}-{b[6]:X2}{b[7]:X2}-" +
            $"{b[8]:X2}{b[9]:X2}-" +
            $"{b[10]:X2}{b[11]:X2}{b[12]:X2}{b[13]:X2}{b[14]:X2}{b[15]:X2}";
    }
}