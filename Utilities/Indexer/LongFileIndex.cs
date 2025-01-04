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
public class LongFileIndex : ILargeList<IndexBlockData>, IListHints, IDisposable
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
    public IndexBlockData this[long index]
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
    private void SetIndexBlock(long index, IndexBlockData indexBlockData)
    {
        var position = index * IndexBlockData.BlockSizeBytes;
        var buffer = indexBlockData.ToByteArray();
        this.IndexFileCache.WriteThroughCache(position, buffer);
        if (this._longCount < ++index) this._longCount = index;
    }

    /// <summary>
    /// Suitable for parallel getting the file index block.
    /// </summary>
    private IndexBlockData GetIndexBlock(long index)
    {
        var buffer = new byte[IndexBlockData.BlockSizeBytes];
        var position = index * IndexBlockData.BlockSizeBytes;
        var count = this.IndexFileCache.ReadThroughCache(position, buffer);
        const string message = "Index file to short to read this index block.";
        if (count < IndexBlockData.BlockSizeBytes) throw new IOException(message);
        return new IndexBlockData(buffer);
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
        var bufferingListElements = momoryForListBufferingBytes / IndexBlockData.BlockSizeBytes;
        if (isParallel) bufferingListElements /= Environment.ProcessorCount;
        const string thresholdHintName = "BufferingElementsLimit";
        this._listHints.Add(thresholdHintName, bufferingListElements);
    }

    private void InitListCaches()
    {
        const int bytesInMegabyte = 1024 * 1024;
        const int totalMemoryParts = 4;
        const int minPagesCount = 1;

        var totalMemoryBytes = this.IndexerOptions.CacheSizeLimitMegabytes * bytesInMegabyte;
        var isParallel = this.IndexerOptions.EnableParallelExecution;
        var momoryForOneFileFuffering = totalMemoryBytes / totalMemoryParts;

        var pageSize = (double)PredefinedConstants.DefaultFileCachePageSize;
        var pagesCountForAllThreads = momoryForOneFileFuffering / pageSize;
        if (pagesCountForAllThreads < minPagesCount) pagesCountForAllThreads = minPagesCount;

        var pagesCountForEachThread = pagesCountForAllThreads;
        if (isParallel) pagesCountForEachThread /= Environment.ProcessorCount;
        if (pagesCountForEachThread < minPagesCount) pagesCountForEachThread = minPagesCount;

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
        if (rebuild) IndexBlockParser.ConvertDataToIndexFile(
            this.IndexerOptions.SourceFilePath,
            this.IndexerOptions.IndexFilePath,
            this.IndexerOptions.SourceEncoding);

        if (!File.Exists(this.IndexerOptions.IndexFilePath)) return;

        this._longCount = new FileInfo(this.IndexerOptions.IndexFilePath).Length / IndexBlockData.BlockSizeBytes;
    }
}
