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
    /// <summary>
    /// Convert index to file with data rows.
    /// </summary>
    /// <returns>
    /// Count of items readed from index file.
    /// </returns>
    /// <exception cref="IOException">
    /// Not enough bytes for read index block.
    /// </exception>
    public static long ConvertIndexToDataFile(
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

        if (expectedPreamble.SequenceEqual(actualPreamble) && !append)
            targetFileStream.Write(actualPreamble);

        var indexBuffer = new byte[IndexBlockData.BlockSizeBytes];
        long resultRowsCount = 0;

        while (indexFileStream.Position < indexFileStream.Length)
        {
            var bytesRead = indexFileStream.Read(indexBuffer);
            const string exceptionMessage = "Not enough bytes for read index block.";
            if (bytesRead != IndexBlockData.BlockSizeBytes) throw new IOException(exceptionMessage);

            var block = new IndexBlockData(indexBuffer);
            ++resultRowsCount;

            WriteIndexBlock(
                block,
                targetFileStream,
                sourceFileStream,
                encoding,
                partsDelimiterBytes,
                rowEndingBytes);
        }

        return resultRowsCount;
    }

    /// <summary>
    /// Convert data to file with index blocks.
    /// </summary>
    /// <returns>
    /// Count of items readed from source file.
    /// </returns>
    /// <exception cref="FileLoadException">
    /// Looks like the source file is empty.
    /// </exception>
    public static long ConvertDataToIndexFile(
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
        long currentSymbolStreamPosition = (expectedPreamble.SequenceEqual(actualPreamble) ? actualPreamble.Length : 0);
        sourceFileStream.Position = 0;

        using var sourceStreamReader = new StreamReader(sourceFileStream, encoding);
        long currentSymbolNumber = 0;

        var fileIndexBlock = ReadIndexBlock(
            sourceStreamReader,
            ref currentSymbolStreamPosition,
            encoding,
            sourceFilePath,
            ref currentSymbolNumber);

        var exceptionMessage = $"Looks like the source file {sourceFilePath} is empty.";
        if (fileIndexBlock == null) throw new FileLoadException(exceptionMessage);

        long resultRowsCount = 0;

        while (fileIndexBlock != null)
        {
            indexFileStream.Write(fileIndexBlock.ToByteArray());
            ++resultRowsCount;

            fileIndexBlock = ReadIndexBlock(
                sourceStreamReader,
                ref currentSymbolStreamPosition,
                encoding,
                sourceFilePath,
                ref currentSymbolNumber);
        }

        indexFileStream.Flush(true);
        return resultRowsCount;
    }

    private static IndexBlockData ReadIndexBlock(
        StreamReader streamReader,
        ref long currentSymbolStreamPosition,
        Encoding encoding,
        string sourceFilePath,
        ref long currentSymbolNumber)
    {
        long numberStartPosition = 0;
        long numberEndPosition = 0;
        long stringStartPosition = 0;
        long stringEndPosition = 0;

        var cachedStringStart = new char[IndexBlockData.CachedSymbolsCount];
        Array.Fill(cachedStringStart, PredefinedConstants.StringCacheFiller);

        const int stateRowStart = 0;
        const int stateNumber = 1;
        const int stateDelimiter = 2;
        const int stateString = 3;
        const int stateEnding = 4;
        const int stateRowFinish = 5;

        int stateCurrent = stateRowStart;
        int symbolIndexInsideState = 0;
        long stateStartPosition = currentSymbolStreamPosition;
        long numberPartValueItself = long.MinValue;

        const string exceptionMessage = "Looks like the source file {0} has broken rows format in position {1}";

        while (!streamReader.EndOfStream && (stateCurrent != stateRowFinish))
        {
            var currentSymbol = (char)streamReader.Read();
            ++currentSymbolNumber;

            switch (stateCurrent)
            {
                case stateString:
                    {
                        if (PredefinedConstants.SourceRowEnding[0] == currentSymbol)
                        {
                            stringStartPosition = stateStartPosition;
                            stringEndPosition = currentSymbolStreamPosition;
                            if (PredefinedConstants.SourceRowEnding.Length > 1)
                            {
                                stateCurrent = stateEnding;
                                symbolIndexInsideState = 1;
                                break;
                            }
                            stateCurrent = stateRowFinish;
                            break;
                        }
                        if (symbolIndexInsideState < cachedStringStart.Length)
                        {
                            cachedStringStart[symbolIndexInsideState] = currentSymbol;
                            ++symbolIndexInsideState;
                        }
                        break;
                    }
                case stateNumber:
                    {
                        if (PredefinedConstants.SourcePartsDelimiter[0] == currentSymbol)
                        {
                            numberStartPosition = stateStartPosition;
                            numberEndPosition = currentSymbolStreamPosition;
                            if (PredefinedConstants.SourcePartsDelimiter.Length > 1)
                            {
                                stateCurrent = stateDelimiter;
                                symbolIndexInsideState = 1;
                                break;
                            }
                            stateCurrent = stateString;
                            symbolIndexInsideState = 0;
                            stateStartPosition = currentSymbolStreamPosition + encoding.GetByteCount(new[] { currentSymbol });
                            break;
                        }
                        if (PredefinedConstants.NumberPartStopSymbols.Contains(currentSymbol))
                        {
                            throw new FileLoadException(string.Format(exceptionMessage, sourceFilePath, currentSymbolNumber));
                        }
                        if (numberPartValueItself == long.MinValue) break;
                        var newDigit = long.Parse(new ReadOnlySpan<char>([currentSymbol]));
                        if ((long.MaxValue - newDigit) / 10 < numberPartValueItself) numberPartValueItself = long.MinValue;
                        else numberPartValueItself = numberPartValueItself * 10 + newDigit;
                        break;
                    }
                case stateRowStart:
                    {
                        if (PredefinedConstants.NumberPartStopSymbols.Contains(currentSymbol))
                        {
                            throw new FileLoadException(string.Format(exceptionMessage, sourceFilePath, currentSymbolNumber));
                        }
                        var success = long.TryParse(new ReadOnlySpan<char>([currentSymbol]), out numberPartValueItself);
                        if (!success) numberPartValueItself = long.MinValue;
                        stateCurrent = stateNumber;
                        break;
                    }
                case stateDelimiter:
                    {
                        if (symbolIndexInsideState == PredefinedConstants.SourcePartsDelimiter.Length)
                        {
                            if (PredefinedConstants.StringPartStopSymbols.Contains(currentSymbol))
                            {
                                throw new FileLoadException(string.Format(exceptionMessage, sourceFilePath, currentSymbolNumber));
                            }
                            stateCurrent = stateString;
                            symbolIndexInsideState = 1;
                            stateStartPosition = currentSymbolStreamPosition;
                            if (cachedStringStart.Length > 0) cachedStringStart[0] = currentSymbol;
                            break;
                        }
                        if (PredefinedConstants.SourcePartsDelimiter[symbolIndexInsideState] != currentSymbol)
                        {
                            throw new FileLoadException(string.Format(exceptionMessage, sourceFilePath, currentSymbolNumber));
                        };
                        ++symbolIndexInsideState;
                        break;
                    }
                case stateEnding:
                    {
                        if (PredefinedConstants.SourceRowEnding[symbolIndexInsideState] != currentSymbol)
                        {
                            throw new FileLoadException(string.Format(exceptionMessage, sourceFilePath, currentSymbolNumber));
                        }
                        if (symbolIndexInsideState == PredefinedConstants.SourceRowEnding.Length - 1)
                        {
                            stateCurrent = stateRowFinish;
                            break;
                        }
                        break;
                    }
            }

            currentSymbolStreamPosition += encoding.GetByteCount(new[] { currentSymbol });
        }

        if (stateCurrent == stateRowStart)
            return null;

        if (stateCurrent != stateRowFinish)
            throw new FileLoadException(string.Format(exceptionMessage, sourceFilePath, currentSymbolNumber));

        if (numberPartValueItself != long.MinValue)
        {
            numberStartPosition = numberPartValueItself;
            numberEndPosition = 0;
        }

        var result = new IndexBlockData(
            cachedStringStart,
            numberStartPosition,
            numberEndPosition,
            stringStartPosition,
            stringEndPosition);

        return result;
    }

    private static void WriteIndexBlock(
        IndexBlockData block,
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
        if (stringPartLength <= IndexBlockData.CachedSymbolsCount)
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
