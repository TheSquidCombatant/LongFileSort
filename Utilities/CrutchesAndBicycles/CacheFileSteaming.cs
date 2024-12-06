using LongFileSort.Utilities.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LongFileSort.Utilities.CrutchesAndBicycles;

public class CacheFileSteaming : IDisposable
{
    private readonly int _pageSize = PredefinedConstants.FileStreamBufferPageSize;

    private readonly int _pagesCount = PredefinedConstants.FileStreamBufferPagesCount;

    private readonly LinkedList<Page> _cache = new();

    private readonly List<Pair> _pool = new();

    private readonly string _filePath;

    private bool _isDisposed = false;

    private readonly FileStream _stream;

    private class Pair { public bool IsBusy; public FileAccess Access; public FileStream Stream; }

    private class Page { public bool Changed; public long Position; public byte[] Data; }

    public CacheFileSteaming(string filePath)
    {
        this._stream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
        this._filePath = filePath;
    }

    /// <summary>
    /// Well suited for short readings.
    /// </summary>
    public int ReadThroughCache(long position, byte[] buffer)
    {
        var pagePosition = (position / _pageSize) * _pageSize;
        var page = this.GetPage(pagePosition);
        if (page == null) return 0;

        var sourceOffset = position - pagePosition;
        var readCount = (page.Data.Length < sourceOffset ? 0 : page.Data.Length - sourceOffset);
        readCount = Math.Min(buffer.Length, readCount);
        Array.Copy(page.Data, sourceOffset, buffer, 0, readCount);

        var readCountTotal = readCount;
        var bufferPosition = readCount;
        pagePosition += _pageSize;

        while ((page.Data.Length == _pageSize) && (bufferPosition < buffer.Length))
        {
            page = this.GetPage(pagePosition);
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
    public void WriteThroughCache(long position, byte[] buffer)
    {
        lock (this._cache)
        {
            var pagePosition = (position / _pageSize) * _pageSize;
            var page = this.GetPage(pagePosition);
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
            this.SetPage(page);

            pagePosition += _pageSize;
            var bufferPosition = writeCount;

            while (bufferPosition < buffer.Length)
            {
                page = this.GetPage(pagePosition);
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
                this.SetPage(page);

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
    /// <see cref="FileMode.OpenOrCreate"/> and <see cref="FileShare.ReadWrite"/>.
    /// </returns>
    /// <remarks>
    /// Each requested <see cref="FileStream"/> should be released
    /// via method <see cref="PoolFileSteaming.Release"/>.
    /// You should not close or dispose this instance by youself.
    /// </remarks>
    public FileStream Request(FileAccess access)
    {
        FileStream stream = null;

        lock (this._pool)
        {
            var pair = this._pool.FirstOrDefault(p => p.IsBusy == false && p.Access == access);
            if (pair != null)
            {
                pair.IsBusy = true;
                stream = pair.Stream;
                stream.Position = 0;
            }
            else
            {
                stream = new FileStream(this._filePath, FileMode.OpenOrCreate, access, FileShare.ReadWrite);
                pair = new Pair() { IsBusy = true, Access = access, Stream = stream };
                this._pool.Add(pair);
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
            foreach (var pair in this._pool)
                if (pair.Stream.Equals(fileStream))
                    pair.IsBusy = false;
    }

    private Page GetPage(long position)
    {
        lock (this._cache)
        {
            if (this._cache.Last?.Value.Position == position) return this._cache.Last.Value;

            var node = this._cache.FindLastNode(p => p.Position == position);
            if (node != null) this._cache.Remove(node);

            var pageToReturn = node?.Value ?? ReadPage(position);
            if (pageToReturn == null) return null;
            this._cache.AddLast(pageToReturn);

            if (this._cache.Count < _pagesCount) return pageToReturn;

            if (this._cache.First.Value.Changed) WritePage(this._cache.First.Value);
            this._cache.RemoveFirst();

            return pageToReturn;
        }
    }

    private Page ReadPage(long position)
    {
        if (this._stream.Length <= position) return null;
        this._stream.Position = position;
        var pageBuffer = new byte[_pageSize];
        var bytesCount = this._stream.Read(pageBuffer, 0, _pageSize);
        if (bytesCount < _pageSize) Array.Resize(ref pageBuffer, bytesCount);
        return new Page() { Changed = false, Position = position, Data = pageBuffer };
    }

    private void SetPage(Page page)
    {
        lock (this._cache)
        {
            if (this._cache.Last?.Value.Position == page.Position)
            {
                this._cache.Last.Value = page;
                return;
            }

            var node = this._cache.FindLastNode(p => p.Position == page.Position);
            if (node != null) this._cache.Remove(node);
            this._cache.AddLast(page);

            if (this._cache.Count < _pagesCount) return;

            if (this._cache.First.Value.Changed) WritePage(this._cache.First.Value);
            this._cache.RemoveFirst();
        }
    }

    private void WritePage(Page page)
    {
        this._stream.Position = page.Position;
        this._stream.Write(page.Data, 0, page.Data.Length);
    }

    public void Dispose()
    {
        if (this._isDisposed) return;

        lock (this._cache)
            foreach (var page in this._cache)
                if (page.Changed)
                    WritePage(page);

        lock (this._pool)
            foreach (var pair in this._pool)
                pair.Stream.Dispose();

        this._stream.Dispose();

        this._isDisposed = true;
    }
}