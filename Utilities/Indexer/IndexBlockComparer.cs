using System.Collections.Generic;

namespace LongFileSort.Utilities.Indexer;

public class IndexBlockComparer : IComparer<IndexBlock>
{
    public int Compare(IndexBlock x, IndexBlock y)
    {
        var stringPartCoparisonResult = StringPartCoparison(x, y);
        if (stringPartCoparisonResult != 0) return stringPartCoparisonResult;
        return NumberPartCoparison(x, y);
    }

    public static int StringPartCoparison(IndexBlock x, IndexBlock y)
    {
        var stringCacheCoparisonResult = StringCacheCoparison(x, y);
        if (stringCacheCoparisonResult.HasValue) return stringCacheCoparisonResult.Value;

        var stringValueCoparisonResult = StringValueComparison(x, y);
        if (stringValueCoparisonResult.HasValue) return stringValueCoparisonResult.Value;

        return 0;
    }

    public static int NumberPartCoparison(IndexBlock x, IndexBlock y)
    {
        var numberLengthComparison = NumberLengthComparison(x, y);
        if (numberLengthComparison.HasValue) return numberLengthComparison.Value;

        var numberValueComparison = NumberValueComparison(x, y);
        if (numberValueComparison.HasValue) return numberValueComparison.Value;

        return 0;
    }

    private static int? StringCacheCoparison(IndexBlock x, IndexBlock y)
    {
        for (int i = 0; i < IndexBlock.CachedSymbolsCount; ++i)
        {
            var result = x.IndexBlockData.CachedStringStart[i] - y.IndexBlockData.CachedStringStart[i];
            if (result != 0) return result;
        }

        if ((x.ParentIndexer.IndexerOptions.IndexFilePath == y.ParentIndexer.IndexerOptions.IndexFilePath)
            && (x.IndexBlockData.StringStartPosition == y.IndexBlockData.StringStartPosition)
                && (x.IndexBlockData.StringEndPosition == y.IndexBlockData.StringEndPosition))
            return 0;

        var firstTotalLength = x.IndexBlockData.StringEndPosition - x.IndexBlockData.StringStartPosition;
        var secondTotalLength = y.IndexBlockData.StringEndPosition - y.IndexBlockData.StringStartPosition;

        if (firstTotalLength <= IndexBlock.CachedSymbolsCount)
            if (secondTotalLength <= IndexBlock.CachedSymbolsCount)
                return 0;

        if (firstTotalLength == IndexBlock.CachedSymbolsCount)
            if (secondTotalLength > IndexBlock.CachedSymbolsCount)
                return -1;

        if (secondTotalLength == IndexBlock.CachedSymbolsCount)
            if (firstTotalLength > IndexBlock.CachedSymbolsCount)
                return 1;

        return null;
    }

    private static int? StringValueComparison(IndexBlock x, IndexBlock y)
    {
        using var firstIndexReader = x.GetStringPartReader();
        using var secondIndexReader = y.GetStringPartReader();

        while (!firstIndexReader.EndOfStream && !secondIndexReader.EndOfStream)
        {
            var firstStreamChar = (char)firstIndexReader.Read();
            var secondStreamChar = (char)secondIndexReader.Read();
            var stringValueCoparisonResult = firstStreamChar - secondStreamChar;
            if (stringValueCoparisonResult != 0) return stringValueCoparisonResult;
        }

        if (!firstIndexReader.EndOfStream && secondIndexReader.EndOfStream) return 1;
        if (firstIndexReader.EndOfStream && !secondIndexReader.EndOfStream) return -1;

        return null;
    }

    private static int? NumberLengthComparison(IndexBlock x, IndexBlock y)
    {
        if ((x.ParentIndexer.IndexerOptions.IndexFilePath == y.ParentIndexer.IndexerOptions.IndexFilePath)
            && (x.IndexBlockData.NumberStartPosition == y.IndexBlockData.NumberStartPosition)
                && (x.IndexBlockData.NumberEndPosition == y.IndexBlockData.NumberEndPosition))
            return 0;

        if ((x.IndexBlockData.NumberEndPosition != 0) && (y.IndexBlockData.NumberEndPosition == 0))
            return 1;
        if ((x.IndexBlockData.NumberEndPosition == 0) && (y.IndexBlockData.NumberEndPosition != 0))
            return -1;
        if ((x.IndexBlockData.NumberEndPosition == 0) && (y.IndexBlockData.NumberEndPosition == 0))
            return x.IndexBlockData.NumberStartPosition.CompareTo(y.IndexBlockData.NumberStartPosition);

        var firstTotalLength = x.IndexBlockData.NumberEndPosition - x.IndexBlockData.NumberStartPosition;
        var secondTotalLength = y.IndexBlockData.NumberEndPosition - y.IndexBlockData.NumberStartPosition;

        if (firstTotalLength > secondTotalLength) return 1;
        if (firstTotalLength < secondTotalLength) return -1;

        return null;
    }

    private static int? NumberValueComparison(IndexBlock x, IndexBlock y)
    {
        using var firstIndexReader = x.GetNumberPartReader();
        using var secondIndexReader = y.GetNumberPartReader();

        while (!firstIndexReader.EndOfStream && !secondIndexReader.EndOfStream)
        {
            var firstStreamChar = (char)firstIndexReader.Read();
            var secondStreamChar = (char)secondIndexReader.Read();
            var stringValueCoparisonResult = firstStreamChar - secondStreamChar;
            if (stringValueCoparisonResult != 0) return stringValueCoparisonResult;
        }

        if (!firstIndexReader.EndOfStream && secondIndexReader.EndOfStream) return 1;
        if (firstIndexReader.EndOfStream && !secondIndexReader.EndOfStream) return -1;

        return null;
    }
}
