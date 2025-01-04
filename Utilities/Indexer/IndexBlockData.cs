using LongFileSort.Utilities.CrutchesAndBicycles;
using LongFileSort.Utilities.Options;
using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace LongFileSort.Utilities.Indexer;

public class IndexBlockData
{
    /// <summary>
    /// Suitable for parallel reads for the number part.
    /// </summary>
    /// <returns>
    /// A new reader each time with a new underlying filestream.
    /// </returns>
    /// <remarks>
    /// The caller must finalize each received reader itself.
    /// </remarks>
    public StreamReader GetNumberPartReader(LongFileIndex parentIndexer)
    {
        var length = this.NumberEndPosition - this.NumberStartPosition;

        if (length < PredefinedConstants.DefaultFileStreamBufferSize)
        {
            var buffer = new byte[length];
            var count = parentIndexer.SourceFileCache.ReadThroughCache(this.NumberStartPosition, buffer);
            const string message = "The actual and expected lengths of the number part do not match.";
            if (count < length) throw new IOException(message);
            var memoryStream = new MemoryStream(buffer);
            return new StreamReader(memoryStream, parentIndexer.IndexerOptions.SourceEncoding);
        }

        var fileStream = parentIndexer.SourceFileCache.Request();

        var readonlyPartialStream = new ReadonlyPartialStream(
            fileStream,
            this.NumberStartPosition,
            this.NumberEndPosition);

        readonlyPartialStream.OnClose += c => parentIndexer.SourceFileCache.Release(fileStream);

        return new StreamReader(readonlyPartialStream, parentIndexer.IndexerOptions.SourceEncoding);
    }

    /// <summary>
    /// Suitable for parallel reads for the string part.
    /// </summary>
    /// <returns>
    /// A new reader each time with a new underlying filestream.
    /// </returns>
    /// <remarks>
    /// The caller must finalize each received reader itself.
    /// </remarks>
    public StreamReader GetStringPartReader(LongFileIndex parentIndexer)
    {
        var length = this.StringEndPosition - this.StringStartPosition;

        if (length < PredefinedConstants.DefaultFileStreamBufferSize)
        {
            var buffer = new byte[length];
            var count = parentIndexer.SourceFileCache.ReadThroughCache(this.StringStartPosition, buffer);
            const string message = "The actual and expected lengths of the string part do not match.";
            if (count < length) throw new IOException(message);
            var memoryStream = new MemoryStream(buffer);
            return new StreamReader(memoryStream, parentIndexer.IndexerOptions.SourceEncoding);
        }

        var fileStream = parentIndexer.SourceFileCache.Request();

        var readonlyPartialStream = new ReadonlyPartialStream(
            fileStream,
            this.StringStartPosition,
            this.StringEndPosition);

        readonlyPartialStream.OnClose += c => parentIndexer.SourceFileCache.Release(fileStream);

        return new StreamReader(readonlyPartialStream, parentIndexer.IndexerOptions.SourceEncoding);
    }

    public const int CachedSymbolsCount = 16;

    public const int BlockSizeBytes = sizeof(long) * 4 + sizeof(char) * CachedSymbolsCount;

    /// <summary>
    /// <see cref="CachedStringStart"/> stores the beginning of the string part of the row to speed up operations.
    /// </summary>
    public readonly char[] CachedStringStart;

    /// <summary>
    /// If <see cref="NumberEndPosition"/> is not zero, then <see cref="NumberStartPosition"/>
    /// means position in the file of the beginning of the numeric part.
    /// If <see cref="NumberEndPosition"/> is zero, then <see cref="NumberStartPosition"/>
    /// means the number itself.
    /// </summary>
    public readonly long NumberStartPosition;

    /// <summary>
    /// If <see cref="NumberEndPosition"/> is not zero, then <see cref="NumberEndPosition"/>
    /// means position in the file of the ending of the numeric part.
    /// If <see cref="NumberEndPosition"/> is zero, then <see cref="NumberStartPosition"/>
    /// means the number itself.
    /// </summary>
    public readonly long NumberEndPosition;

    /// <summary>
    /// <see cref="StringStartPosition"/> means position in the file of the beginning
    /// of the string part, including beginning cached in <see cref="CachedStringStart"/>.
    /// </summary>
    public readonly long StringStartPosition;

    /// <summary>
    /// <see cref="StringEndPosition"/> means position in the file of the ending
    /// of the string part, including beginning cached in <see cref="CachedStringStart"/>.
    /// </summary>
    public readonly long StringEndPosition;

    public unsafe byte[] ToByteArray()
    {
        var bytes = GC.AllocateUninitializedArray<byte>(IndexBlockData.BlockSizeBytes, false);

        fixed (byte* target = &bytes[0])
        {
            Unsafe.Write(target, NumberStartPosition);
            Unsafe.Write(target + sizeof(long), NumberEndPosition);
            Unsafe.Write(target + sizeof(long) * 2, StringStartPosition);
            Unsafe.Write(target + sizeof(long) * 3, StringEndPosition);

            var cacheLength = IndexBlockData.CachedSymbolsCount * sizeof(char);

            fixed (char* symbols = &this.CachedStringStart[0])
            {
                Buffer.MemoryCopy(symbols, target + sizeof(long) * 4, cacheLength, cacheLength);
            }
        }

        return bytes;
    }

    public IndexBlockData(
        char[] cachedStringStart,
        long numberStartPosition,
        long numberEndPosition,
        long stringStartPosition,
        long stringEndPosition)
    {
        this.CachedStringStart = cachedStringStart;
        this.NumberStartPosition = numberStartPosition;
        this.NumberEndPosition = numberEndPosition;
        this.StringStartPosition = stringStartPosition;
        this.StringEndPosition = stringEndPosition;
    }

    public unsafe IndexBlockData(byte[] bytes)
    {
        fixed (byte* source = &bytes[0])
        {
            this.NumberStartPosition = Unsafe.Read<long>(source);
            this.NumberEndPosition = Unsafe.Read<long>(source + sizeof(long));
            this.StringStartPosition = Unsafe.Read<long>(source + sizeof(long) * 2);
            this.StringEndPosition = Unsafe.Read<long>(source + sizeof(long) * 3);

            this.CachedStringStart = GC.AllocateUninitializedArray<char>(IndexBlockData.CachedSymbolsCount, false);
            var cacheLength = IndexBlockData.CachedSymbolsCount * sizeof(char);

            fixed (char* symbols = &this.CachedStringStart[0])
            {
                Buffer.MemoryCopy(source + sizeof(long) * 4, symbols, cacheLength, cacheLength);
            }
        }
    }
}