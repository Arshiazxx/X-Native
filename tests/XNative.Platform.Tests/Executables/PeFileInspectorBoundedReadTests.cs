using System;
using System.IO;
using XNative.Core.Devices;
using XNative.Platform.Executables;
using Xunit;

namespace XNative.Platform.Tests.Executables
{
    /// <summary>
    /// Tests for the bounded-read contract of
    /// <see cref="PeFileInspector.Inspect(Stream)"/> (previously the stream
    /// was read into memory unbounded): the overload must consume at most
    /// a 1 MiB header window, in a single incrementally filled buffer, no
    /// matter how long the source stream is.
    /// </summary>
    /// <remarks>
    /// The exact read semantics asserted here are the implementation's
    /// documented ones: <c>Stream.Read</c> is called repeatedly, each time
    /// for the buffer space still remaining, until the 1 MiB buffer is full
    /// or a read returns 0 (end of stream). Once the buffer is full no
    /// further read — not even a one-byte probe — is attempted, so a longer
    /// stream has exactly 1,048,576 bytes consumed from it.
    /// </remarks>
    public sealed class PeFileInspectorBoundedReadTests
    {
        private const int HeaderWindowBytes = 1024 * 1024;

        private readonly PeFileInspector _inspector = new PeFileInspector();

        [Fact]
        public void Inspect_StreamLongerThanCapWithValidHeader_ParsesAsValid()
        {
            byte[] pe = SyntheticPe.X64GuiExe();
            var longData = new byte[HeaderWindowBytes + 12345];
            Array.Copy(pe, longData, pe.Length);
            for (int i = pe.Length; i < longData.Length; i++)
            {
                longData[i] = 0xFF;
            }

            using (var stream = new TrackingStream(new MemoryStream(longData)))
            {
                var info = _inspector.Inspect(stream);

                Assert.True(info.IsValid);
                Assert.Null(info.Error);
                Assert.Equal(ProcessorArchitecture.X64, info.Architecture);
                Assert.Equal(SyntheticPe.MachineX64, info.MachineRaw);
                Assert.Equal(true, info.IsPe32Plus);
            }
        }

        [Fact]
        public void Inspect_StreamLongerThanCap_ConsumesExactlyTheCapAndNothingMore()
        {
            byte[] pe = SyntheticPe.X64GuiExe();
            var longData = new byte[(2 * HeaderWindowBytes) + 999];
            Array.Copy(pe, longData, pe.Length);

            using (var stream = new TrackingStream(new MemoryStream(longData)))
            {
                var info = _inspector.Inspect(stream);

                Assert.True(info.IsValid);

                // Exactly the cap is consumed: never more than the cap, and
                // no extra probe read once the window is full. The backing
                // MemoryStream satisfies the single 1 MiB request in one
                // call, so exactly one Read call is expected.
                Assert.Equal(HeaderWindowBytes, stream.TotalBytesRead);
                Assert.Equal(1, stream.ReadCallCount);
                Assert.Equal(HeaderWindowBytes, stream.Position);

                // The caller's stream is left open and positioned at the cap;
                // the remainder is still there for the caller to read.
                Assert.True(stream.CanRead);
                Assert.Equal(longData.Length, stream.Length);
                Assert.NotEqual(-1, stream.ReadByte());
            }
        }

        [Fact]
        public void Inspect_ChunkedStreamWithSmallReads_StillParsesValidPe()
        {
            byte[] pe = SyntheticPe.ManagedX64Exe();

            // Each Read returns at most 7 bytes, so the inspector's
            // incremental loop must issue many partial reads and still
            // assemble the complete header correctly.
            using (var stream = new TrackingStream(new MemoryStream(pe), maxBytesPerRead: 7))
            {
                var info = _inspector.Inspect(stream);

                Assert.True(info.IsValid);
                Assert.True(info.IsManaged);
                Assert.Equal(ProcessorArchitecture.X64, info.Architecture);
                Assert.True(stream.ReadCallCount > 1);
                Assert.Equal(pe.Length, stream.TotalBytesRead);
                Assert.True(stream.CanRead);
            }
        }

        [Fact]
        public void Inspect_ChunkedLongStream_StopsAtCapAcrossPartialReads()
        {
            byte[] pe = SyntheticPe.X64GuiExe();
            var longData = new byte[HeaderWindowBytes + 5000];
            Array.Copy(pe, longData, pe.Length);

            using (var stream = new TrackingStream(new MemoryStream(longData), maxBytesPerRead: 8192))
            {
                var info = _inspector.Inspect(stream);

                Assert.True(info.IsValid);
                Assert.Equal(ProcessorArchitecture.X64, info.Architecture);
                Assert.Equal(HeaderWindowBytes, stream.TotalBytesRead);
            }
        }

        /// <summary>
        /// A read-tracking wrapper stream: delegates to an inner stream,
        /// optionally caps how many bytes a single <c>Read</c> may return,
        /// and records how many bytes and read calls the consumer made.
        /// </summary>
        private sealed class TrackingStream : Stream
        {
            private readonly Stream _inner;
            private readonly int _maxBytesPerRead;

            public TrackingStream(Stream inner)
                : this(inner, int.MaxValue)
            {
            }

            public TrackingStream(Stream inner, int maxBytesPerRead)
            {
                _inner = inner ?? throw new ArgumentNullException(nameof(inner));
                _maxBytesPerRead = maxBytesPerRead;
            }

            public long TotalBytesRead { get; private set; }

            public int ReadCallCount { get; private set; }

            public override bool CanRead => _inner.CanRead;

            public override bool CanSeek => _inner.CanSeek;

            public override bool CanWrite => false;

            public override long Length => _inner.Length;

            public override long Position
            {
                get => _inner.Position;
                set => _inner.Position = value;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                ReadCallCount++;
                int read = _inner.Read(buffer, offset, Math.Min(count, _maxBytesPerRead));
                if (read > 0)
                {
                    TotalBytesRead += read;
                }

                return read;
            }

            public override void Flush()
            {
                _inner.Flush();
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                return _inner.Seek(offset, origin);
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _inner.Dispose();
                }

                base.Dispose(disposing);
            }
        }
    }
}
