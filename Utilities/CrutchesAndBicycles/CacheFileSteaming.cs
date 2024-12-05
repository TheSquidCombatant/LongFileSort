using LongFileSort.Utilities.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LongFileSort.Utilities.CrutchesAndBicycles;

public class CacheFileSteaming : IDisposable
{
    private bool _isDisposed = false;

    private static int _pageSize = PredefinedConstants.FileStreamBufferPageSize;

    private static int _pagesCount = PredefinedConstants.FileStreamBufferPagesCount;

    private SortedDictionary<string, LinkedList<Page>> _cache = new();

    private Dictionary<int, List<Pair>> _pool = new();

    private class Pair { public bool IsBusy; public FileStream Stream; }

    private class Page { public bool Changed; public long Position; public byte[] Data; }

    /// <summary>
    /// Well suited for short readings.
    /// </summary>
    public int ReadThroughCache(string file, long position, byte[] buffer)
    {
        var pagePosition = (position / _pageSize) * _pageSize;
        var page = this.GetPage(file, pagePosition);
        if (page == null) return 0;

        var readCount = (page.Data.Length < position - pagePosition ? 0 : page.Data.Length - position + pagePosition);
        readCount = Math.Min(buffer.Length, readCount);
        Array.Copy(page.Data, position - pagePosition, buffer, 0, readCount);

        var readCountTotal = readCount;
        var bufferPosition = readCount;
        pagePosition += _pageSize;

        while ((page.Data.Length == _pageSize) && (bufferPosition < buffer.Length))
        {
            page = this.GetPage(file, pagePosition);
            if (page == null) return (int)readCountTotal;
            readCount = Math.Min(buffer.Length - bufferPosition, page.Data.Length);
            Array.Copy(page.Data, 0, buffer, bufferPosition, readCount);

            readCountTotal += readCount;
            bufferPosition += readCount;
            pagePosition += _pageSize;
        }

        return (int)readCountTotal;
    }

    /// <summary>
    /// Well suited for short writings.
    /// </summary>
    public void WriteThroughCache(string file, long position, byte[] buffer)
    {
        lock (this._cache)
        {
            var pagePosition = (position / _pageSize) * _pageSize;
            var page = this.GetPage(file, pagePosition);
            var dataSize = (int)Math.Min(_pageSize, buffer.Length + position - pagePosition);

            if (page == null)
            {
                page = new Page() { Position = pagePosition, Data = new byte[dataSize] };
            }
            {
                if (dataSize > page.Data.Length) Array.Resize(ref page.Data, dataSize);
            }

            var writeCount = Math.Min(buffer.Length, page.Data.Length - position + pagePosition);
            Array.Copy(buffer, 0, page.Data, position - pagePosition, writeCount);

            page.Changed = true;
            this.SetPage(file, page);

            pagePosition += _pageSize;
            var bufferPosition = writeCount;

            while (bufferPosition < buffer.Length)
            {
                page = this.GetPage(file, pagePosition);
                dataSize = (int)Math.Min(_pageSize, buffer.Length - bufferPosition);

                if (page == null)
                {
                    page = new Page() { Position = pagePosition, Data = new byte[dataSize] };
                }
                {
                    if (dataSize > page.Data.Length) Array.Resize(ref page.Data, dataSize);
                }

                writeCount = Math.Min(page.Data.Length, buffer.Length - bufferPosition);
                Array.Copy(buffer, bufferPosition, page.Data, 0, writeCount);

                page.Changed = true;
                this.SetPage(file, page);

                pagePosition += _pageSize;
                bufferPosition += writeCount;
            }
        }
    }

    /// <summary>
    /// Requests a shared instance with the specified path and read/write permission.
    /// </summary>
    /// <returns>
    /// A stream instance with parameters equivalent
    /// <see cref="FileMode.Open"/> and <see cref="FileShare.ReadWrite"/>.
    /// </returns>
    /// <remarks>
    /// Each requested <see cref="FileStream"/> should be released
    /// via method <see cref="PoolFileSteaming.Release"/>.
    /// You should not close or dispose this instance by youself.
    /// </remarks>
    public FileStream Request(string path, FileAccess access)
    {
        var hash = (path, access).GetHashCode();
        FileStream stream = null;

        lock (this._pool)
        {
            if (this._pool.TryGetValue(hash, out var list))
            {
                var pair = list.FirstOrDefault(p => p.IsBusy == false);
                if (pair != null)
                {
                    pair.IsBusy = true;
                    stream = pair.Stream;
                    stream.Position = 0;
                }
                else
                {
                    stream = new FileStream(path, FileMode.Open, access, FileShare.ReadWrite);
                    pair = new Pair() { IsBusy = true, Stream = stream };
                    list.Add(pair);
                }
            }
            else
            {
                stream = new FileStream(path, FileMode.Open, access, FileShare.ReadWrite);
                list = [new Pair() { IsBusy = true, Stream = stream }];
                this._pool.Add(hash, list);
            }
            return stream;
        }
    }

    /// <summary>
    /// Releases a requested file stream.
    /// </summary>
    public void Release(FileStream fileStream)
    {
        fileStream.Flush();

        lock (this._pool)
            foreach (var pair in this._pool.Values.SelectMany(c => c))
                if (pair.Stream.Equals(fileStream))
                    pair.IsBusy = false;
    }

    private Page GetPage(string file, long position)
    {
        lock (this._cache)
        {
            var success = this._cache.TryGetValue(file, out var list);
            if (!success) this._cache.Add(file, list = new LinkedList<Page>());

            if (list?.Last?.Value?.Position == position) return list.Last.Value;

            var node = list.FindLastNode(p => p.Position == position);
            if (node != null) list.Remove(node);
            var pageToReturn = node?.Value ?? ReadPage(file, position);
            list.AddLast(pageToReturn);

            if (list.Count == _pagesCount)
            {
                if (list.First.Value.Changed) WritePage(file, list.First.Value);
                list.RemoveFirst();
            }

            return pageToReturn;
        }
    }

    private Page ReadPage(string file, long position)
    {
        var fileStream = this.Request(file, FileAccess.Read);

        if (fileStream.Length <= position) return null;
        fileStream.Position = position;
        var pageBuffer = new byte[_pageSize];
        var bytesCount = fileStream.Read(pageBuffer, 0, _pageSize);

        this.Release(fileStream);

        if (bytesCount < _pageSize) Array.Resize(ref pageBuffer, bytesCount);
        return new Page() { Changed = false, Position = position, Data = pageBuffer };
    }

    private void SetPage(string file, Page page)
    {
        lock (this._cache)
        {
            var success = this._cache.TryGetValue(file, out var list);
            if (!success) this._cache.Add(file, list = new LinkedList<Page>());

            if (list?.Last?.Value?.Position == page.Position)
            {
                list.Last.Value = page;
                return;
            }

            var node = list.FindLastNode(p => p.Position == page.Position);
            if (node != null) list.Remove(node);
            list.AddLast(page);

            if (list.Count == _pagesCount)
            {
                if (list.First.Value.Changed) WritePage(file, list.First.Value);
                list.RemoveFirst();
            }
        }
    }

    private void WritePage(string file, Page page)
    {
        var fileStream = this.Request(file, FileAccess.Write);

        fileStream.Position = page.Position;
        fileStream.Write(page.Data, 0, page.Data.Length);

        this.Release(fileStream);
    }

    public void Dispose()
    {
        if (this._isDisposed) return;

        lock (this._cache)
            foreach (var file in this._cache)
                foreach (var page in file.Value)
                    if (page.Changed)
                        WritePage(file.Key, page);

        lock (this._pool)
            foreach (var pair in this._pool.Values.SelectMany(c => c))
                pair.Stream.Dispose();

        this._isDisposed = true;
    }
}