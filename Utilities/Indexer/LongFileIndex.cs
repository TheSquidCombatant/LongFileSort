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

    private long _longCount = -1;

    internal IndexerOptions IndexerOptions { get; private set; }

    internal CacheFileSteaming CacheFileSteaming { get; private set; }

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
        CacheFileSteaming.WriteThroughCache(this.IndexerOptions.IndexFilePath, position, buffer);
    }

    /// <summary>
    /// Suitable for parallel getting the file index block.
    /// </summary>
    private IndexBlock GetIndexBlock(long index)
    {
        var buffer = new byte[IndexBlock.BlockSizeBytes];
        var position = index * IndexBlock.BlockSizeBytes;
        CacheFileSteaming.ReadThroughCache(this.IndexerOptions.IndexFilePath, position, buffer);
        var data = new IndexBlock.Data(buffer);
        return new IndexBlock(data, this);
    }

    private void FlushIndex()
    {
        if (this._isDisposed) return;
        this.CacheFileSteaming.Dispose();
        if (this._cleanupIndexFileWhenClosed) File.Delete(this.IndexerOptions.IndexFilePath);
        this._isDisposed = true;
    }

    private void BuildIndex(IndexerOptions indexerOptions, bool rebuild)
    {
        this.IndexerOptions = indexerOptions;
        this.CacheFileSteaming = new CacheFileSteaming();

        if (rebuild) IndexBlockParser.ConvertSourceToIndexFile(
            this.IndexerOptions.SourceFilePath,
            this.IndexerOptions.IndexFilePath,
            this.IndexerOptions.SourceEncoding);

        this._longCount = new FileInfo(this.IndexerOptions.IndexFilePath).Length / IndexBlock.BlockSizeBytes;
    }
}
