using LongFileSort.Utilities.CrutchesAndBicycles;
using LongFileSort.Utilities.Options;
using System;
using System.IO;
using System.Text;

namespace LongFileSort.Utilities.Indexer;

public class IndexBlock
{
    internal static long CachedSymbolsCount = PredefinedConstants.StringPartCacheSymbolsCount;

    internal static long BlockSizeBytes = sizeof(long) * 4 + sizeof(char) * CachedSymbolsCount;

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
        var fileStream = this.ParentIndexer.CacheFileSteaming.Request(
            this.ParentIndexer.IndexerOptions.SourceFilePath,
            FileAccess.Read);

        var readonlyPartialStream = new ReadonlyPartialStream(
            fileStream,
            this.IndexBlockData.NumberStartPosition,
            this.IndexBlockData.NumberEndPosition);

        readonlyPartialStream.OnClose += c => this.ParentIndexer.CacheFileSteaming.Release(fileStream);

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
        var fileStream = this.ParentIndexer.CacheFileSteaming.Request(
            this.ParentIndexer.IndexerOptions.SourceFilePath,
            FileAccess.Read);

        var readonlyPartialStream = new ReadonlyPartialStream(
            fileStream,
            this.IndexBlockData.StringStartPosition,
            this.IndexBlockData.StringEndPosition);

        readonlyPartialStream.OnClose += c => this.ParentIndexer.CacheFileSteaming.Release(fileStream);

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

        public byte[] ToByteArray()
        {
            var bytes = new byte[BlockSizeBytes];

            var buffer = BitConverter.GetBytes(NumberStartPosition);
            Array.Copy(buffer, 0, bytes, 0, sizeof(long));
            buffer = BitConverter.GetBytes(NumberEndPosition);
            Array.Copy(buffer, 0, bytes, sizeof(long), sizeof(long));
            buffer = BitConverter.GetBytes(StringStartPosition);
            Array.Copy(buffer, 0, bytes, sizeof(long) * 2, sizeof(long));
            buffer = BitConverter.GetBytes(StringEndPosition);
            Array.Copy(buffer, 0, bytes, sizeof(long) * 3, sizeof(long));

            for (int i = 0; i < CachedSymbolsCount; ++i)
            {
                buffer = BitConverter.GetBytes(CachedStringStart[i]);
                var position = sizeof(long) * 4 + sizeof(char) * i;
                Array.Copy(buffer, 0, bytes, position, sizeof(char));
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

        public Data(byte[] bytes)
        {
            this.NumberStartPosition = BitConverter.ToInt64(bytes, 0);
            this.NumberEndPosition = BitConverter.ToInt64(bytes, sizeof(long));
            this.StringStartPosition = BitConverter.ToInt64(bytes, sizeof(long) * 2);
            this.StringEndPosition = BitConverter.ToInt64(bytes, sizeof(long) * 3);
            this.CachedStringStart = new char[CachedSymbolsCount];

            for (int i = 0; i < CachedSymbolsCount; ++i)
            {
                var position = sizeof(long) * 4 + sizeof(char) * i;
                var symbol = BitConverter.ToChar(bytes, position);
                this.CachedStringStart[i] = symbol;
            }
        }
    }
}