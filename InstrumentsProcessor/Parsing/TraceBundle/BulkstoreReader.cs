// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;

namespace InstrumentsProcessor.Parsing.TraceBundle
{
    /// <summary>
    /// Reads binary bulkstore files from a .trace bundle and yields
    /// rows as dictionaries of field name → value.
    /// </summary>
    internal static class BulkstoreReader
    {
        private const int HeaderPage = 0x1000;

        public static IEnumerable<Dictionary<string, object>> ReadRows(
            string storePath, StoreSchema schema)
        {
            var bsPath = Path.Combine(storePath, "bulkstore");
            if (!File.Exists(bsPath)) yield break;
            if (schema.RowSize <= 0) yield break;

            bool isInterval = schema.IsInterval;
            var dataFields = schema.GetDataFields();
            byte[] rowBuf = new byte[schema.RowSize];

            using var stream = Decompressor.OpenDecompressedStream(bsPath);

            // Skip header page
            int remaining = HeaderPage;
            while (remaining > 0)
            {
                int toRead = Math.Min(rowBuf.Length, remaining);
                int read = stream.Read(rowBuf, 0, toRead);
                if (read == 0) yield break;
                remaining -= read;
            }

            // Reuse a single dictionary to avoid millions of allocations.
            // The caller (ProcessTraceRun) consumes each row immediately via
            // CreateEvent before requesting the next, so reuse is safe.
            var row = new Dictionary<string, object>(dataFields.Count + 2);

            // Read rows one at a time from the stream
            while (true)
            {
                int totalRead = 0;
                while (totalRead < schema.RowSize)
                {
                    int read = stream.Read(rowBuf, totalRead, schema.RowSize - totalRead);
                    if (read == 0) break;
                    totalRead += read;
                }
                if (totalRead < schema.RowSize) yield break;

                var rowSpan = rowBuf.AsSpan(0, schema.RowSize);

                // Skip zero rows
                bool allZero = true;
                for (int b = 0; b < rowSpan.Length; b++)
                    if (rowSpan[b] != 0) { allZero = false; break; }
                if (allZero) continue;

                row.Clear();

                // Topology: extract timestamp, duration, and category references.
                // TopoPoint  (16 bytes): timestamp(8) + cat1(4) + cat2(4)
                // TopoInterval(24 bytes): timestamp(8) + duration(8) + cat1(4) + cat2(4)
                row["timestamp"] = BinaryPrimitives.ReadUInt64LittleEndian(rowSpan);

                if (isInterval)
                {
                    row["duration"] = BinaryPrimitives.ReadUInt64LittleEndian(rowSpan.Slice(8));
                    if (schema.TopoSize >= 24)
                    {
                        row["__cat1__"] = BinaryPrimitives.ReadUInt32LittleEndian(rowSpan.Slice(16));
                        row["__cat2__"] = BinaryPrimitives.ReadUInt32LittleEndian(rowSpan.Slice(20));
                    }
                }
                else
                {
                    if (schema.TopoSize >= 16)
                    {
                        row["__cat1__"] = BinaryPrimitives.ReadUInt32LittleEndian(rowSpan.Slice(8));
                        row["__cat2__"] = BinaryPrimitives.ReadUInt32LittleEndian(rowSpan.Slice(12));
                    }
                }

                // Data fields
                foreach (var f in dataFields)
                {
                    if (f.Offset + f.Size > rowSpan.Length) continue;
                    row[f.Name] = f.Type switch
                    {
                        FieldType.U64 => BinaryPrimitives.ReadUInt64LittleEndian(rowSpan.Slice(f.Offset)),
                        _ => (object)BinaryPrimitives.ReadUInt32LittleEndian(rowSpan.Slice(f.Offset))
                    };
                }
                yield return row;
            }
        }
    }
}