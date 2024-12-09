using LongFileSort.Utilities.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace LongFileSort.Utilities.CrutchesAndBicycles;

public class CacheReadonlyFileSteaming : IDisposable
{
    private readonly int _pageSize = PredefinedConstants.FileStreamBufferPageSize;

    private readonly int _pagesCount = PredefinedConstants.FileStreamReadonlyBufferPagesCount;

    private readonly ConcurrentDictionary<int, Dictionary<long, LinkedListNode<Page>>> _links = new();

    private readonly ConcurrentDictionary<int, LinkedList<Page>> _cache = new();

    private readonly ConcurrentDictionary<int, List<Pair>> _pool = new();

    private readonly string _filePath;

    private bool _isDisposed = false;

    private readonly ConcurrentDictionary<int, FileStream> _stream = new();

    private class Pair { public bool IsBusy; public FileStream Stream; }

    private class Page { public long Position; public int Length; public byte[] Data; }

    public CacheReadonlyFileSteaming(string filePath)
    {
        this._filePath = filePath;
    }

    /// <summary>
    /// Well suited for short readings.
    /// </summary>
    public int ReadThroughCache(long position, byte[] buffer)
    {
        var pagePosition = (position / _pageSize) * _pageSize;
        var page = this.GetPage(pagePosition);
        if (page.Length == 0) return 0;

        var pageOffset = position - pagePosition;
        var readCount = (page.Length < pageOffset ? 0 : page.Length - pageOffset);
        readCount = Math.Min(buffer.Length, readCount);

        Array.Copy(page.Data, pageOffset, buffer, 0, readCount);

        var readCountTotal = readCount;
        var bufferPosition = readCount;
        pagePosition += _pageSize;

        while ((page.Length == _pageSize) && (bufferPosition < buffer.Length))
        {
            page = this.GetPage(pagePosition);
            if (page.Length == 0) return (int)readCountTotal;

            readCount = Math.Min(buffer.Length - bufferPosition, page.Length);

            Array.Copy(page.Data, 0, buffer, bufferPosition, readCount);

            readCountTotal += readCount;
            bufferPosition += readCount;
            pagePosition += _pageSize;
        }

        return (int)readCountTotal;
    }

    /// <summary>
    /// Well suited for long readings.
    /// </summary>
    /// <returns>
    /// A stream instance with parameters equivalent <see cref="FileMode.Open"/>,
    /// <see cref="FileAccess.Read"/> and <see cref="FileShare.ReadWrite"/>.
    /// </returns>
    /// <remarks>
    /// Each requested <see cref="FileStream"/> should be released
    /// via method <see cref="PoolFileSteaming.Release"/>.
    /// You should not close or dispose this instance by youself.
    /// </remarks>
    public FileStream Request()
    {
        var threadId = Thread.CurrentThread.ManagedThreadId;
        var pool = this._pool.GetOrAdd(threadId, (c) => new());
        FileStream stream = null;

        var pair = pool.FirstOrDefault(p => p.IsBusy == false);

        if (pair != null)
        {
            pair.IsBusy = true;
            stream = pair.Stream;
            stream.Position = 0;
        }
        else
        {
            stream = new FileStream(this._filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            pair = new Pair() { IsBusy = true, Stream = stream };
            pool.Add(pair);
        }
        return stream;
    }

    /// <summary>
    /// Releases a requested file stream.
    /// </summary>
    public void Release(FileStream fileStream)
    {
        fileStream.Flush();

        var threadId = Thread.CurrentThread.ManagedThreadId;
        var pool = this._pool.GetOrAdd(threadId, (c) => new());

        foreach (var pair in pool)
            if (pair.Stream.Equals(fileStream))
                pair.IsBusy = false;
    }

    private Page GetPage(long position)
    {
        var threadId = Thread.CurrentThread.ManagedThreadId;
        var cache = this._cache.GetOrAdd(threadId, (c) => new());
        var links = this._links.GetOrAdd(threadId, (c) => new());

        if (cache.Last?.Value.Position == position)
        {
            return cache.Last.Value;
        }

        var found = links.TryGetValue(position, out var node);

        if (found)
        {
            cache.Remove(node);
            cache.AddLast(node);
            return node.Value;
        }

        if (cache.Count == _pagesCount)
        {
            node = cache.First;
            var pageToUpdate = node.Value;
            links.Remove(pageToUpdate.Position);
            pageToUpdate.Position = position;
            ReadPage(threadId, pageToUpdate);
            if (pageToUpdate.Length != 0)
            {
                cache.RemoveFirst();
                node = cache.AddLast(pageToUpdate);
            }
            links.Add(position, node);
            return pageToUpdate;
        }

        var page = new Page()
        {
            Position = position,
            Data = new byte[_pageSize]
        };

        ReadPage(threadId, page);
        node = cache.AddLast(page);
        links.Add(position, node);
        return page;
    }

    private void ReadPage(int threadId, Page page)
    {
        FileStream action(int c) => new(
            _filePath,
            FileMode.OpenOrCreate,
            FileAccess.Read,
            FileShare.ReadWrite);

        var stream = this._stream.GetOrAdd(threadId, action);
        stream.Position = page.Position;
        var bytesCount = stream.Read(page.Data, 0, page.Data.Length);
        page.Length = bytesCount;
    }

    public void Dispose()
    {
        if (this._isDisposed) return;

        lock (this._pool)
            foreach (var pool in this._pool.Values)
                foreach (var pair in pool)
                    pair.Stream.Dispose();

        lock (this._stream)
            foreach (var stream in this._stream.Values)
                stream.Dispose();

        this._isDisposed = true;
    }
}