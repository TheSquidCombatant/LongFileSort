using LongFileSort.Utilities.CrutchesAndBicycles;
using LongFileSort.Utilities.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using static LongFileSort.Utilities.CrutchesAndBicycles.ListExtensions;

namespace LongFileSort.Utilities.Indexer;

/// <summary>
/// Core of large file processing mechanism.
/// </summary>
public class LongFileIndex : ILargeList<IndexBlock>, IListHints, IDisposable
{
    private Dictionary<string, object> _listHints = new();

    private bool _cleanupIndexFileWhenClosed;

    private int _isDisposed = 0;

    private long _longCount = 0;

    internal IndexerOptions IndexerOptions { get; private set; }

    internal CacheFileSteaming IndexFileCache { get; private set; }

    internal CacheReadonlyFileSteaming SourceFileCache { get; private set; }

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

    public object GetHintValue(string key) => this._listHints.TryGetValue(key, out var value) ? value : null;

    ~LongFileIndex() => this.FlushIndex();

    /// <summary>
    /// Suitable for parallel setting the file index block.
    /// </summary>
    private void SetIndexBlock(long index, IndexBlock indexBlock)
    {
        var position = index * IndexBlock.Data.BlockSizeBytes;
        var buffer = indexBlock.IndexBlockData.ToByteArray();
        this.IndexFileCache.WriteThroughCache(position, buffer);
        if (this._longCount < ++index) this._longCount = index;
    }

    /// <summary>
    /// Suitable for parallel getting the file index block.
    /// </summary>
    private IndexBlock GetIndexBlock(long index)
    {
        var buffer = new byte[IndexBlock.Data.BlockSizeBytes];
        var position = index * IndexBlock.Data.BlockSizeBytes;
        var count = this.IndexFileCache.ReadThroughCache(position, buffer);
        const string message = "Index file to short to read this index block.";
        if (count < IndexBlock.Data.BlockSizeBytes) throw new IOException(message);
        var data = new IndexBlock.Data(buffer);
        return new IndexBlock(data, this);
    }

    private void FlushIndex()
    {
        if (Interlocked.Exchange(ref this._isDisposed, 1) == 1) return;
        this.IndexFileCache.Dispose();
        this.SourceFileCache.Dispose();
        if (this._cleanupIndexFileWhenClosed) File.Delete(this.IndexerOptions.IndexFilePath);
    }

    private void BuildIndex(IndexerOptions indexerOptions, bool rebuild)
    {
        this.IndexerOptions = indexerOptions;
        this.InitListHits();
        this.InitListCaches();
        this.InitIndexFiles(rebuild);
    }

    private void InitListHits()
    {
        var totalMemoryBytes = this.IndexerOptions.CacheSizeLimitMegabytes * 1024 * 1024;
        var isParallel = this.IndexerOptions.EnableParallelExecution;
        var momoryForListBufferingBytes = totalMemoryBytes / 2;
        var bufferingListElements = momoryForListBufferingBytes / IndexBlock.Data.BlockSizeBytes;
        if (isParallel) bufferingListElements /= Environment.ProcessorCount;
        const string thresholdHintName = "BufferingElementsLimit";
        this._listHints.Add(thresholdHintName, bufferingListElements);
    }

    private void InitListCaches()
    {
        const int bytesInMegabyte = 1024 * 1024;
        const int totalMemoryParts = 4;
        const int minPageSizeBytes = 1024;
        const int minPagesCount = 1;

        var totalMemoryBytes = this.IndexerOptions.CacheSizeLimitMegabytes * bytesInMegabyte;
        var isParallel = this.IndexerOptions.EnableParallelExecution;
        var momoryForOneFileFuffering = totalMemoryBytes / totalMemoryParts;

        var pagesCountForEachThread = (double)PredefinedConstants.DefaultFileCachePagesCount;
        var pageSize = (double)PredefinedConstants.DefaultFileCachePageSize;
        var momeryRatio = momoryForOneFileFuffering / (pagesCountForEachThread * pageSize);

        pagesCountForEachThread *= momeryRatio;
        if (pagesCountForEachThread < minPagesCount) pagesCountForEachThread = minPagesCount;
        var pagesCountForAllThreads = pagesCountForEachThread;

        if (isParallel) pagesCountForAllThreads *= Environment.ProcessorCount;
        if (isParallel) pageSize /= Environment.ProcessorCount;
        if (pageSize < minPageSizeBytes) pageSize = minPageSizeBytes;

        this.IndexFileCache = new CacheFileSteaming(
            (int)pageSize,
            (int)pagesCountForAllThreads,
            this.IndexerOptions.IndexFilePath);

        this.SourceFileCache = new CacheReadonlyFileSteaming(
            (int)pageSize,
            (int)pagesCountForEachThread,
            this.IndexerOptions.SourceFilePath);
    }

    private void InitIndexFiles(bool rebuild)
    {
        if (rebuild) IndexBlockParser.ConvertSourceToIndexFile(
            this.IndexerOptions.SourceFilePath,
            this.IndexerOptions.IndexFilePath,
            this.IndexerOptions.SourceEncoding);

        if (!File.Exists(this.IndexerOptions.IndexFilePath)) return;

        this._longCount = new FileInfo(this.IndexerOptions.IndexFilePath).Length / IndexBlock.Data.BlockSizeBytes;
    }
}
