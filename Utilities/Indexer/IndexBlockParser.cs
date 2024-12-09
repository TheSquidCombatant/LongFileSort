using LongFileSort.Utilities.Options;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace LongFileSort.Utilities.Indexer;

/// <summary>
/// Converts an index file to a raw data file and vice versa.
/// </summary>
public static class IndexBlockParser
{
    public static void ConvertIndexToTargetFile(
        string sourceFilePath,
        string indexFilePath,
        string targetFilePath,
        Encoding encoding,
        bool append = false)
    {
        var targetFileMode = (append ? FileMode.Append : FileMode.Create);

        using var indexFileStream = new FileStream(
            indexFilePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite);

        using var sourceFileStream = new FileStream(
            sourceFilePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite);

        using var targetFileStream = new FileStream(
            targetFilePath,
            targetFileMode,
            FileAccess.Write,
            FileShare.ReadWrite);

        var expectedPreamble = encoding.GetPreamble();
        var actualPreamble = new byte[expectedPreamble.Length];
        sourceFileStream.Read(actualPreamble, 0, actualPreamble.Length);

        var partsDelimiterBytes = encoding.GetBytes(PredefinedConstants.SourcePartsDelimiter);
        var rowEndingBytes = encoding.GetBytes(PredefinedConstants.SourceRowEnding);

        var indexBuffer = new byte[IndexBlock.BlockSizeBytes];

        if (expectedPreamble.SequenceEqual(actualPreamble) && !append)
            targetFileStream.Write(actualPreamble);

        while (indexFileStream.Position < indexFileStream.Length)
        {
            indexFileStream.Read(indexBuffer);
            var block = new IndexBlock.Data(indexBuffer);

            WriteIndexBlock(
                block,
                targetFileStream,
                sourceFileStream,
                encoding,
                partsDelimiterBytes,
                rowEndingBytes);
        }
    }

    public static void ConvertSourceToIndexFile(
        string sourceFilePath,
        string indexFilePath,
        Encoding encoding,
        bool append = false)
    {
        var indexFileMode = (append ? FileMode.Append : FileMode.Create);

        using var indexFileStream = new FileStream(
            indexFilePath,
            indexFileMode,
            FileAccess.Write,
            FileShare.ReadWrite);

        using var sourceFileStream = new FileStream(
            sourceFilePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite);

        var expectedPreamble = encoding.GetPreamble();
        var actualPreamble = new byte[expectedPreamble.Length];
        sourceFileStream.Read(actualPreamble, 0, actualPreamble.Length);
        long streamCurrentPosition = (expectedPreamble.SequenceEqual(actualPreamble) ? actualPreamble.Length : 0);
        sourceFileStream.Position = 0;

        using var sourceStreamReader = new StreamReader(sourceFileStream, encoding);

        var fileIndexBlock = ReadIndexBlock(sourceStreamReader, ref streamCurrentPosition, encoding, sourceFilePath);
        var exceptionMessage = $"Looks like the source file {sourceFilePath} is empty.";
        if (fileIndexBlock == null) throw new FileLoadException(exceptionMessage);

        while (fileIndexBlock != null)
        {
            indexFileStream.Write(fileIndexBlock.ToByteArray());
            fileIndexBlock = ReadIndexBlock(sourceStreamReader, ref streamCurrentPosition, encoding, sourceFilePath);
        }

        indexFileStream.Flush(true);
    }

    private static IndexBlock.Data ReadIndexBlock(
        StreamReader streamReader,
        ref long streamCurrentPosition,
        Encoding encoding,
        string sourceFilePath)
    {
        long numberStartPosition = 0;
        long numberEndPosition = 0;
        long stringStartPosition = 0;
        long stringEndPosition = 0;

        var cachedStringStart = new char[IndexBlock.CachedSymbolsCount];
        Array.Fill(cachedStringStart, PredefinedConstants.StringCacheFiller);

        const int stateRowStart = 0;
        const int stateNumber = 1;
        const int stateDelimiter = 2;
        const int stateString = 3;
        const int stateEnding = 4;
        const int stateRowFinish = 5;

        var stateCurrent = stateRowStart;
        long stateStartPosition = streamCurrentPosition;
        long stateEndPosition = 0;
        long symbolIndexInsideState = 0;
        long numberPartValueItself = long.MinValue;

        var exceptionMessage = $"Looks like the source file {sourceFilePath} is broken.";

        while (!streamReader.EndOfStream && (stateCurrent != stateRowFinish))
        {
            stateEndPosition = streamCurrentPosition;
            var nextChar = (char)streamReader.Read();
            switch (stateCurrent)
            {
                case stateString:
                    {
                        if (PredefinedConstants.SourceRowEnding[0] == nextChar)
                        {
                            stringStartPosition = stateStartPosition;
                            stringEndPosition = stateEndPosition;
                            stateCurrent = (PredefinedConstants.SourceRowEnding.Length > 1 ? stateEnding : stateRowFinish);
                            symbolIndexInsideState = 0;
                            stateStartPosition = stateEndPosition;
                            break;
                        }
                        if (symbolIndexInsideState < cachedStringStart.Length)
                        {
                            cachedStringStart[symbolIndexInsideState] = nextChar;
                        }
                        break;
                    }
                case stateNumber:
                    {
                        if (PredefinedConstants.SourcePartsDelimiter[0] == nextChar)
                        {
                            numberStartPosition = stateStartPosition;
                            numberEndPosition = stateEndPosition;
                            stateCurrent = (PredefinedConstants.SourcePartsDelimiter.Length > 1 ? stateDelimiter : stateString);
                            symbolIndexInsideState = 0;
                            stateStartPosition = stateEndPosition;
                            break;
                        }
                        if (PredefinedConstants.NumberPartStopSymbols.Contains(nextChar))
                        {
                            throw new FileLoadException(exceptionMessage);
                        }
                        if (numberPartValueItself == long.MinValue) break;
                        var newDigit = long.Parse(new ReadOnlySpan<char>([nextChar]));
                        if ((long.MaxValue - newDigit) / 10 < numberPartValueItself) numberPartValueItself = long.MinValue;
                        else numberPartValueItself = numberPartValueItself * 10 + newDigit;
                        break;
                    }
                case stateRowStart:
                    {
                        if (PredefinedConstants.NumberPartStopSymbols.Contains(nextChar))
                        {
                            throw new FileLoadException(exceptionMessage);
                        }
                        numberPartValueItself = long.Parse(new ReadOnlySpan<char>([nextChar]));
                        stateCurrent = stateNumber;
                        break;
                    }
                case stateDelimiter:
                    {
                        if (symbolIndexInsideState == PredefinedConstants.SourcePartsDelimiter.Length)
                        {
                            if (PredefinedConstants.StringPartStopSymbols.Contains(nextChar))
                            {
                                throw new FileLoadException(exceptionMessage);
                            }
                            stateCurrent = stateString;
                            symbolIndexInsideState = 0;
                            stateStartPosition = stateEndPosition;
                            if (cachedStringStart.Length > 0) cachedStringStart[0] = nextChar;
                            break;
                        }
                        if (PredefinedConstants.SourcePartsDelimiter[(int)symbolIndexInsideState] != nextChar)
                        {
                            throw new FileLoadException(exceptionMessage);
                        };
                        break;
                    }
                case stateEnding:
                    {
                        if (PredefinedConstants.SourceRowEnding[(int)symbolIndexInsideState] != nextChar)
                        {
                            throw new FileLoadException(exceptionMessage);
                        }
                        if (symbolIndexInsideState == PredefinedConstants.SourceRowEnding.Length - 1)
                        {
                            stateCurrent = stateRowFinish;
                            break;
                        }
                        break;
                    }
            }
            ++symbolIndexInsideState;
            streamCurrentPosition += encoding.GetByteCount(new[] { nextChar });
        }

        if (stateCurrent == stateRowStart) return null;
        if (stateCurrent != stateRowFinish) throw new FileLoadException(exceptionMessage);

        if (numberPartValueItself != long.MinValue)
        {
            numberStartPosition = numberPartValueItself;
            numberEndPosition = 0;
        }

        return new IndexBlock.Data(
            cachedStringStart,
            numberStartPosition,
            numberEndPosition,
            stringStartPosition,
            stringEndPosition);
    }

    private static void WriteIndexBlock(
        IndexBlock.Data block,
        FileStream targetFileStream,
        FileStream sourceFileStream,
        Encoding encoding,
        byte[] partsDelimiterBytes,
        byte[] rowEndingBytes)
    {
        if (block.NumberEndPosition == 0)
        {
            var bytes = encoding.GetBytes(block.NumberStartPosition.ToString());
            targetFileStream.Write(bytes);
        }
        else
        {
            var sourceBuffer = new byte[PredefinedConstants.DefaultFileStreamBufferSize];
            sourceFileStream.Position = block.NumberStartPosition;
            while (sourceFileStream.Position < block.NumberEndPosition)
            {
                var bytesLeft = block.NumberEndPosition - sourceFileStream.Position;
                var expectedCount = (int)Math.Min(bytesLeft, sourceBuffer.Length);
                var actualCount = sourceFileStream.Read(sourceBuffer, 0, expectedCount);
                targetFileStream.Write(sourceBuffer, 0, actualCount);
            }
        }

        targetFileStream.Write(partsDelimiterBytes);

        var stringPartLength = block.StringEndPosition - block.StringStartPosition;
        if (stringPartLength <= PredefinedConstants.StringPartCacheSymbolsCount)
        {
            for (int s = 0; s < stringPartLength; ++s)
            {
                var sourceBuffer = encoding.GetBytes([block.CachedStringStart[s]]);
                targetFileStream.Write(sourceBuffer, 0, sourceBuffer.Length);
            }
        }
        else
        {
            var sourceBuffer = new byte[PredefinedConstants.DefaultFileStreamBufferSize];
            sourceFileStream.Position = block.StringStartPosition;
            while (sourceFileStream.Position < block.StringEndPosition)
            {
                var bytesLeft = block.StringEndPosition - sourceFileStream.Position;
                var expectedCount = (int)Math.Min(bytesLeft, sourceBuffer.Length);
                var actualCount = sourceFileStream.Read(sourceBuffer, 0, expectedCount);
                targetFileStream.Write(sourceBuffer, 0, actualCount);
            }
        }

        targetFileStream.Write(rowEndingBytes);
    }
}
