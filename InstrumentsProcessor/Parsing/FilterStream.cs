// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace InstrumentsProcessor.Parsing
{
    public class FilterStream : Stream
    {
        private readonly Stream baseStream;
        private readonly List<byte[]> filters;
        private readonly byte[] stagingBuffer;
        private int stagingBufferBytes;
        private bool isBaseStreamEmpty;

        public FilterStream(Stream baseStream, IEnumerable<byte[]> filters)
        {
            this.baseStream = baseStream;
            this.filters = filters.ToList();
            stagingBuffer = new byte[filters.Max(f => f.Length) - 1];
            stagingBufferBytes = 0;
            isBaseStreamEmpty = false;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotImplementedException();

        public override long Position
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int bufferBytes = 0;

            while (bufferBytes != count && (stagingBufferBytes != 0 || !isBaseStreamEmpty))
            {
                // Calculate how many bytes to copy from stagingBuffer and baseStream
                int bytesToCopy = count - bufferBytes;
                int stagingBufferBytesToCopy = Math.Min(stagingBufferBytes, bytesToCopy);
                int baseStreamBytesToCopy = Math.Max(0, bytesToCopy - stagingBufferBytesToCopy);

                // Fill buffer from stagingBuffer
                if (stagingBufferBytesToCopy > 0)
                {
                    Array.Copy(stagingBuffer, 0, buffer, offset + bufferBytes, stagingBufferBytesToCopy);
                    bufferBytes += stagingBufferBytesToCopy;
                    Array.Copy(stagingBuffer, stagingBufferBytesToCopy, stagingBuffer, 0, stagingBufferBytes - stagingBufferBytesToCopy);
                    stagingBufferBytes -= stagingBufferBytesToCopy;
                }

                // Fill buffer from baseStream if necessary
                if (baseStreamBytesToCopy > 0)
                {
                    int baseStreamBytesCopied = baseStream.Read(buffer, offset + bufferBytes, baseStreamBytesToCopy);
                    isBaseStreamEmpty = baseStreamBytesCopied < baseStreamBytesToCopy;
                    bufferBytes += baseStreamBytesCopied;
                }

                // Refill stagingBuffer from baseStream so we can detect any upcoming filters to remove
                stagingBufferBytes += baseStream.Read(stagingBuffer, stagingBufferBytes, stagingBuffer.Length - stagingBufferBytes);

                // Remove any filters from the buffer and stagingBuffer until they are clean
                while (ContainsFilter(buffer, offset, bufferBytes, stagingBuffer, stagingBufferBytes, filters,
                    out int filterIndexInBuffer, out int filterBytes))
                {
                    RemoveFilter(buffer, offset, ref bufferBytes, stagingBuffer, ref stagingBufferBytes, filterIndexInBuffer, filterBytes);
                }
            }

            return bufferBytes;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Determines if any filter in filters is a subsequence of the contiguous sequence 
        /// formed by bytes from buffer[offset] to buffer[offset + bufferBytes] and 
        /// stagingBuffer[0] to stagingBuffer[0 + stagingBufferBytes]. If found, returns true 
        /// and sets sequencePos to the starting index in buffer and filterBytes to the 
        /// length of the matching filter.
        /// </summary>
        private static bool ContainsFilter(byte[] buffer, int offset, int bufferBytes,
            byte[] stagingBuffer, int stagingBufferBytes, List<byte[]> filters, out int filterIndexInBuffer, out int filterBytes)
        {
            filterIndexInBuffer = -1;
            filterBytes = 0;
            int totalBytes = bufferBytes + stagingBufferBytes;

            foreach (byte[] filter in filters)
            {
                if (filter.Length == 0 || filter.Length > totalBytes)
                {
                    continue;
                }

                int maxPossibleFilterIndexInBuffer = Math.Min(offset + bufferBytes - 1, offset + totalBytes - filter.Length);

                for (int possibleFilterIndexInBuffer = offset; possibleFilterIndexInBuffer <= maxPossibleFilterIndexInBuffer; possibleFilterIndexInBuffer++)
                {
                    bool found = true;

                    for (int i = possibleFilterIndexInBuffer; i - possibleFilterIndexInBuffer < filter.Length; i++)
                    {
                        byte currentByte = i < offset + bufferBytes ?
                            buffer[i] :
                            stagingBuffer[i - (offset + bufferBytes)];
                        byte filterByte = filter[i - possibleFilterIndexInBuffer];

                        if (currentByte != filter[i - possibleFilterIndexInBuffer])
                        {
                            found = false;

                            break;
                        }
                    }

                    if (found)
                    {
                        filterIndexInBuffer = possibleFilterIndexInBuffer;
                        filterBytes = filter.Length;

                        return true;
                    }
                }
            }

            return false;
        }

        private static void RemoveFilter(byte[] buffer, int offset, ref int bufferBytes,
            byte[] stagingBuffer, ref int stagingBufferBytes, int filterIndexInBuffer, int filterBytes)
        {
            int filterBytesInStagingBuffer = Math.Max(0, filterIndexInBuffer + filterBytes - (offset + bufferBytes));

            // Remove the portion of the filter in stagingBuffer
            if (filterBytesInStagingBuffer > 0)
            {
                Array.Copy(stagingBuffer, filterBytesInStagingBuffer, stagingBuffer, 0, stagingBufferBytes - filterBytesInStagingBuffer);
                stagingBufferBytes -= filterBytesInStagingBuffer;
            }

            int filterStopIndexInBuffer = Math.Min(offset + bufferBytes, filterIndexInBuffer + filterBytes);

            // Remove the portion of the filter in buffer
            if (filterStopIndexInBuffer == offset + bufferBytes)
            {
                bufferBytes = filterIndexInBuffer - offset;
            }
            else
            {
                Array.Copy(buffer, filterStopIndexInBuffer, buffer, filterIndexInBuffer, offset + bufferBytes - filterStopIndexInBuffer);
                bufferBytes -= filterBytes;
            }
        }
    }
}
