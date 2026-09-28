// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.IO;
using System.IO.Compression;

namespace InstrumentsProcessor.Parsing.TraceBundle
{
    /// <summary>
    /// Decompresses zlib-compressed data found inside .trace bundles.
    /// </summary>
    internal static class Decompressor
    {
        public static byte[] Decompress(byte[] raw)
        {
            if (raw.Length >= 2 &&
                raw[0] == 0x78 &&
                (raw[1] == 0x9C || raw[1] == 0x01 || raw[1] == 0xDA))
            {
                using var input = new MemoryStream(raw);
                // Skip the 2-byte zlib header — DeflateStream expects raw deflate.
                input.ReadByte();
                input.ReadByte();
                using var deflate = new DeflateStream(input, CompressionMode.Decompress);
                using var output = new MemoryStream();
                deflate.CopyTo(output);
                return output.ToArray();
            }
            return raw;
        }

        public static byte[] ReadCompressed(string path) => Decompress(File.ReadAllBytes(path));

        /// <summary>
        /// Opens a forward-only decompressed stream over a file. The caller owns
        /// the returned stream and must dispose it. This avoids loading the entire
        /// decompressed content into a single byte[] (critical for large bulkstore files).
        /// </summary>
        public static Stream OpenDecompressedStream(string path)
        {
            var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            byte[] header = new byte[2];
            int read = fs.Read(header, 0, 2);
            if (read == 2 &&
                header[0] == 0x78 &&
                (header[1] == 0x9C || header[1] == 0x01 || header[1] == 0xDA))
            {
                // zlib header detected — DeflateStream expects raw deflate (after 2-byte header).
                // DeflateStream will dispose the underlying FileStream.
                return new DeflateStream(fs, CompressionMode.Decompress);
            }
            // Not compressed — rewind and return raw stream
            fs.Position = 0;
            return fs;
        }
    }
}