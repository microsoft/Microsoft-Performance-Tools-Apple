// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;

namespace InstrumentsProcessor.Parsing
{
    public class ProgressStream : Stream
    {
        private readonly Stream baseStream;
        private readonly long totalBytes;
        private readonly IProgress<int> progress;
        private long bytesRead;

        public ProgressStream(Stream baseStream, long totalBytes, IProgress<int> progress)
        {
            this.baseStream = baseStream;
            this.totalBytes = totalBytes;
            this.progress = progress;
            bytesRead = 0;
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
            int read = baseStream.Read(buffer, offset, count);
            bytesRead += read;
            int percent = (int)((double)bytesRead / totalBytes * 100);
            progress.Report(percent);
            return read;
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
