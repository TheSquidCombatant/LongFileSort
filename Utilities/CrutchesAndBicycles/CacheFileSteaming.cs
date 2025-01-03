﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace LongFileSort.Utilities.CrutchesAndBicycles;

public class CacheFileSteaming : IDisposable
{
    private readonly int _pageSize;
    private readonly int _pagesCount;
    private readonly FileStream _stream;

    private readonly Dictionary<long, LinkedListNode<Page>> _links = new();
    private readonly LinkedList<Page> _cache = new();

    private int _isDisposed = 0;

    private class Page { public bool Changed; public long Position; public int Length; public byte[] Data; }

    public CacheFileSteaming(int pageSizeBytes, int pagesCountForAllThreads, string filePath)
    {
        this._pageSize = pageSizeBytes;
        this._pagesCount = pagesCountForAllThreads;
        this._stream = new FileStream(
            filePath,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.ReadWrite);
    }

    /// <summary>
    /// Well suited for short readings.
    /// </summary>
    public int ReadThroughCache(long position, byte[] buffer)
    {
        var pagePosition = (position / this._pageSize) * this._pageSize;
        var page = this.GetPage(pagePosition);
        if (page.Length == 0) return 0;

        var pageOffset = position - pagePosition;
        var readCount = (page.Length < pageOffset ? 0 : page.Length - pageOffset);
        readCount = Math.Min(buffer.Length, readCount);

        Array.Copy(page.Data, pageOffset, buffer, 0, readCount);

        var readCountTotal = readCount;
        var bufferPosition = readCount;
        pagePosition += this._pageSize;

        while ((page.Length == this._pageSize) && (bufferPosition < buffer.Length))
        {
            page = this.GetPage(pagePosition);
            if (page.Length == 0) return (int)readCountTotal;

            readCount = Math.Min(buffer.Length - bufferPosition, page.Length);

            Array.Copy(page.Data, 0, buffer, bufferPosition, readCount);

            readCountTotal += readCount;
            bufferPosition += readCount;
            pagePosition += this._pageSize;
        }

        return (int)readCountTotal;
    }

    /// <summary>
    /// Well suited for short writings.
    /// </summary>
    public void WriteThroughCache(long position, byte[] buffer)
    {
        if (buffer.Length == 0) return;
        var pagePosition = (position / this._pageSize) * this._pageSize;
        var page = this.GetPage(pagePosition);

        var pageOffset = position - pagePosition;
        var writeCount = Math.Min(buffer.Length, page.Data.Length - pageOffset);

        Array.Copy(buffer, 0, page.Data, pageOffset, writeCount);

        page.Length = (int)Math.Max(pageOffset + writeCount, page.Length);
        page.Changed = true;

        pagePosition += this._pageSize;
        var bufferPosition = writeCount;

        while (bufferPosition < buffer.Length)
        {
            page = this.GetPage(pagePosition);
            writeCount = Math.Min(page.Data.Length, buffer.Length - bufferPosition);

            Array.Copy(buffer, bufferPosition, page.Data, 0, writeCount);

            page.Length = (int)Math.Max(writeCount, page.Length);
            page.Changed = true;

            pagePosition += this._pageSize;
            bufferPosition += writeCount;
        }
    }

    private Page GetPage(long position)
    {
        lock (this._cache)
        {
            if (this._cache.Last?.Value.Position == position)
            {
                return this._cache.Last.Value;
            }

            var found = this._links.TryGetValue(position, out var node);

            if (found)
            {
                this._cache.Remove(node);
                this._cache.AddLast(node);
                return node.Value;
            }

            if (this._cache.Count == this._pagesCount)
            {
                node = this._cache.First;
                var pageToUpdate = node.Value;
                if (pageToUpdate.Changed)
                {
                    WritePage(pageToUpdate);
                    pageToUpdate.Changed = false;
                }
                this._links.Remove(pageToUpdate.Position);
                pageToUpdate.Position = position;
                ReadPage(pageToUpdate);
                if (pageToUpdate.Length != 0)
                {
                    this._cache.RemoveFirst();
                    node = this._cache.AddLast(pageToUpdate);
                }
                this._links.Add(position, node);
                return pageToUpdate;
            }

            var page = new Page()
            {
                Position = position,
                Changed = false,
                Data = new byte[this._pageSize]
            };

            ReadPage(page);
            node = this._cache.AddLast(page);
            this._links.Add(position, node);
            return page;
        }
    }

    private void ReadPage(Page page)
    {
        this._stream.Position = page.Position;
        var bytesCount = this._stream.Read(page.Data, 0, page.Data.Length);
        page.Length = bytesCount;
    }

    private void WritePage(Page page)
    {
        this._stream.Position = page.Position;
        this._stream.Write(page.Data, 0, page.Length);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref this._isDisposed, 1) == 1) return;

        lock (this._cache)
            foreach (var page in this._cache)
                if (page.Changed)
                    WritePage(page);

        this._stream.Dispose();
    }
}