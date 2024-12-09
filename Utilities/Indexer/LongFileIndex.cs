using LongFileSort.Utilities.CrutchesAndBicycles;
using LongFileSort.Utilities.Options;
using System;
using System.IO;

namespace LongFileSort.Utilities.Indexer;

/// <summary>
/// Core of large file processing mechanism.
/// </summary>
public class LongFileIndex : ILargeList<IndexBlock>, IDisposable
{
    private bool _cleanupIndexFileWhenClosed;

    private bool _isDisposed = false;

    private long _longCount = 0;

    internal IndexerOptions IndexerOptions { get; private set; }

    internal CacheFileSteaming IndexFileCache { get; private set; }

    internal CacheFileSteaming SourceFileCache { get; private set; }

    public LongFileIndex(
        IndexerOptions indexerOptions,
        bool rebuildIndexFileFromSource,
        bool cleanupIndexFileWhenClosed)
    {
        this.BuildIndex(indexerOptions, rebuildIndexFileFromSource);
        this._cleanupIndexFileWhenClosed = cleanupIndexFileWhenClosed;
    }

    /// <summary>
    /// Suitable for parallel getting or setting the file index block.
    /// </summary>
    public IndexBlock this[long index]
    {
        get => GetIndexBlock(index);
        set => SetIndexBlock(index, value);
    }

    public long LongCount() => _longCount;

    /// <summary>
    /// Finalize this indexer object and flushes all index buffers to disk.
    /// </summary>
    public void Dispose() => this.FlushIndex();

    ~LongFileIndex() => this.FlushIndex();

    /// <summary>
    /// Suitable for parallel setting the file index block.
    /// </summary>
    private void SetIndexBlock(long index, IndexBlock indexBlock)
    {
        var position = index * IndexBlock.BlockSizeBytes;
        var buffer = indexBlock.IndexBlockData.ToByteArray();
        this.IndexFileCache.WriteThroughCache(position, buffer);
        if (this._longCount < ++index) this._longCount = index;
    }

    /// <summary>
    /// Suitable for parallel getting the file index block.
    /// </summary>
    private IndexBlock GetIndexBlock(long index)
    {
        var buffer = new byte[IndexBlock.BlockSizeBytes];
        var position = index * IndexBlock.BlockSizeBytes;
        var count = this.IndexFileCache.ReadThroughCache(position, buffer);
        const string message = "Index file to short to read this index block.";
        if (count < IndexBlock.BlockSizeBytes) throw new IOException(message);
        var data = new IndexBlock.Data(buffer);
        return new IndexBlock(data, this);
    }

    private void FlushIndex()
    {
        if (this._isDisposed) return;
        this.IndexFileCache.Dispose();
        this.SourceFileCache.Dispose();
        if (this._cleanupIndexFileWhenClosed) File.Delete(this.IndexerOptions.IndexFilePath);
        this._isDisposed = true;
    }

    private void BuildIndex(IndexerOptions indexerOptions, bool rebuild)
    {
        this.IndexerOptions = indexerOptions;
        this.IndexFileCache = new CacheFileSteaming(indexerOptions.IndexFilePath);
        this.SourceFileCache = new CacheFileSteaming(indexerOptions.SourceFilePath);

        if (rebuild) IndexBlockParser.ConvertSourceToIndexFile(
            this.IndexerOptions.SourceFilePath,
            this.IndexerOptions.IndexFilePath,
            this.IndexerOptions.SourceEncoding);

        if (!File.Exists(this.IndexerOptions.IndexFilePath)) return;

        this._longCount = new FileInfo(this.IndexerOptions.IndexFilePath).Length / IndexBlock.BlockSizeBytes;
    }
}
