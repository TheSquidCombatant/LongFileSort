using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LongFileSort.Utilities.CrutchesAndBicycles;

/// <summary>
/// Allows to read only specified part of the underlying stream.
/// </summary>
internal class ReadonlyPartialStream : Stream
{
    /// <summary>
    /// Underlying stream to read from.
    /// </summary>
    private readonly Stream _innerStream;

    /// <summary>
    /// Starting position in underlying stream inclusive.
    /// </summary>
    private readonly long _startPosition;

    /// <summary>
    /// Final position in underlying stream exclusive.
    /// </summary>
    private readonly long _endPosition;

    /// <summary>
    /// Is underlying stream should to be closed when closing the owning object.
    /// </summary>
    private readonly bool _closeUnderlyingStream;

    private int _isDisposed = 0;

    public event Action<ReadonlyPartialStream> OnClose;

    public override bool CanRead => this._innerStream.CanRead;

    public override bool CanSeek => this._innerStream.CanSeek;

    public override bool CanWrite => false;

    public override long Length => this._endPosition - this._startPosition;

    public override void Flush() => throw new NotImplementedException();

    public override void SetLength(long value) => throw new NotImplementedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotImplementedException();

    public new void Dispose() => this.DisposeAsync().AsTask().Wait();

    public override void Close() => this.DisposeAsync().AsTask().Wait();

    public override ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref this._isDisposed, 1) == 1) return ValueTask.CompletedTask;
        this.OnClose?.Invoke(this);
        if (!this._closeUnderlyingStream) return ValueTask.CompletedTask;
        return this._innerStream.DisposeAsync();
    }

    public override long Position
    {
        get => this._innerStream.Position - this._startPosition;
        set => this.Seek(value, SeekOrigin.Begin);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {

        var leftBytes = this._endPosition - this._innerStream.Position;
        count = (leftBytes < count ? (int)leftBytes : count);
        return this._innerStream.Read(buffer, offset, count);
    }

    /// <summary>
    /// Сannot navigate beyond partial stream.
    /// </summary>
    public override long Seek(long offset, SeekOrigin origin)
    {
        var position = origin switch
        {
            SeekOrigin.Begin => this._startPosition + offset,
            SeekOrigin.End => this._endPosition + offset,
            SeekOrigin.Current => this._innerStream.Position + offset,
            _ => throw new NotSupportedException($"Unsupported value {origin} of type {nameof(SeekOrigin)}.")
        };

        if ((position < this._startPosition) || (this._endPosition < position))
            throw new IOException("Сannot navigate beyond partial stream.");
        this._innerStream.Position = position;
        return this.Position;
    }

    public ReadonlyPartialStream(
        Stream innerStream,
        long startPosition,
        long endPosition,
        bool closeUnderlyingStream = false)
    {
        if (innerStream == null)
            throw new ArgumentNullException(nameof(innerStream));

        if ((startPosition < 0) || (innerStream.Length <= startPosition))
            throw new ArgumentOutOfRangeException(nameof(startPosition));

        if ((endPosition < startPosition) || (innerStream.Length < endPosition))
            throw new ArgumentOutOfRangeException(nameof(endPosition));

        this._innerStream = innerStream;
        this._startPosition = startPosition;
        this._endPosition = endPosition;
        this._innerStream.Position = startPosition;
        this._closeUnderlyingStream = closeUnderlyingStream;
    }
}