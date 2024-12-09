using LongFileSort.Utilities.CrutchesAndBicycles;
using LongFileSort.Utilities.Options;
using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace LongFileSort.Utilities.Indexer;

public class IndexBlock
{
    public readonly Data IndexBlockData;

    public readonly LongFileIndex ParentIndexer;

    /// <summary>
    /// Suitable for parallel reads for the number part.
    /// </summary>
    /// <returns>
    /// A new reader each time with a new underlying filestream.
    /// </returns>
    /// <remarks>
    /// The caller must finalize each received reader itself.
    /// </remarks>
    public StreamReader GetNumberPartReader()
    {
        var length = this.IndexBlockData.NumberEndPosition - this.IndexBlockData.NumberStartPosition;

        if (length < PredefinedConstants.FileStreamBufferPageSize)
        {
            var buffer = new byte[length];
            var count = this.ParentIndexer.SourceFileCache.ReadThroughCache(this.IndexBlockData.NumberStartPosition, buffer);
            if (count < length) throw new IOException("The actual and expected lengths of the number part do not match.");
            var memoryStream = new MemoryStream(buffer);
            return new StreamReader(memoryStream, this.ParentIndexer.IndexerOptions.SourceEncoding);
        }

        var fileStream = this.ParentIndexer.SourceFileCache.Request();

        var readonlyPartialStream = new ReadonlyPartialStream(
            fileStream,
            this.IndexBlockData.NumberStartPosition,
            this.IndexBlockData.NumberEndPosition);

        readonlyPartialStream.OnClose += c => this.ParentIndexer.SourceFileCache.Release(fileStream);

        return new StreamReader(readonlyPartialStream, this.ParentIndexer.IndexerOptions.SourceEncoding);
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
    public StreamReader GetStringPartReader()
    {
        var length = this.IndexBlockData.StringEndPosition - this.IndexBlockData.StringStartPosition;

        if (length < PredefinedConstants.FileStreamBufferPageSize)
        {
            var buffer = new byte[length];
            var count = this.ParentIndexer.SourceFileCache.ReadThroughCache(this.IndexBlockData.StringStartPosition, buffer);
            if (count < length) throw new IOException("The actual and expected lengths of the string part do not match.");
            var memoryStream = new MemoryStream(buffer);
            return new StreamReader(memoryStream, this.ParentIndexer.IndexerOptions.SourceEncoding);
        }

        var fileStream = this.ParentIndexer.SourceFileCache.Request();

        var readonlyPartialStream = new ReadonlyPartialStream(
            fileStream,
            this.IndexBlockData.StringStartPosition,
            this.IndexBlockData.StringEndPosition);

        readonlyPartialStream.OnClose += c => this.ParentIndexer.SourceFileCache.Release(fileStream);

        return new StreamReader(readonlyPartialStream, this.ParentIndexer.IndexerOptions.SourceEncoding);
    }

    public IndexBlock(
        Data indexBlockData,
        LongFileIndex parentIndexer)
    {
        this.IndexBlockData = indexBlockData;
        this.ParentIndexer = parentIndexer;
    }

    public class Data
    {
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
            var bytes = GC.AllocateUninitializedArray<byte>(IndexBlock.Data.BlockSizeBytes, false);

            fixed(byte* target = &bytes[0])
            {
                Unsafe.Write(target, NumberStartPosition);
                Unsafe.Write(target + sizeof(long), NumberEndPosition);
                Unsafe.Write(target + sizeof(long) * 2, StringStartPosition);
                Unsafe.Write(target + sizeof(long) * 3, StringEndPosition);

                var cacheLength = IndexBlock.Data.CachedSymbolsCount * sizeof(char);

                fixed (char* symbols = &this.CachedStringStart[0])
                {
                    Buffer.MemoryCopy(symbols, target + sizeof(long) * 4, cacheLength, cacheLength);
                }
            }

            return bytes;
        }

        public Data(
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

        public unsafe Data(byte[] bytes)
        {
            fixed (byte* source = &bytes[0])
            {
                this.NumberStartPosition = Unsafe.Read<long>(source);
                this.NumberEndPosition = Unsafe.Read<long>(source + sizeof(long));
                this.StringStartPosition = Unsafe.Read<long>(source + sizeof(long) * 2);
                this.StringEndPosition = Unsafe.Read<long>(source + sizeof(long) * 3);

                this.CachedStringStart = GC.AllocateUninitializedArray<char>(IndexBlock.Data.CachedSymbolsCount, false);
                var cacheLength = IndexBlock.Data.CachedSymbolsCount * sizeof(char);

                fixed (char* symbols = &this.CachedStringStart[0])
                {
                    Buffer.MemoryCopy(source + sizeof(long) * 4, symbols, cacheLength, cacheLength);
                }
            }
        }
    }
}