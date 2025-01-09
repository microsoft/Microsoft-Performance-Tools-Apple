// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;

namespace InstrumentsProcessor.Parsing
{
    public class CompositeStream : Stream
    {
        private readonly List<Stream> streams;
        private int currentStreamIndex = 0;

        public CompositeStream(IEnumerable<Stream> streams)
        {
            this.streams = new List<Stream>(streams);
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

            while (bufferBytes < count && currentStreamIndex < streams.Count)
            {
                bufferBytes += streams[currentStreamIndex].Read(buffer, offset + bufferBytes, count - bufferBytes);

                if (bufferBytes < count)
                {
                    // Move to the next stream
                    currentStreamIndex++;
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
    }
}
