using System.Collections.Generic;

namespace LongFileSort.Utilities.Indexer;

public class IndexBlockComparer : IComparer<IndexBlockData>
{
    private readonly LongFileIndex _leftIndexer;

    private readonly LongFileIndex _rightIndexer;

    public IndexBlockComparer(LongFileIndex bothOperandsIndexer)
        : this(bothOperandsIndexer, bothOperandsIndexer)
    { }

    public IndexBlockComparer(LongFileIndex firstOperandIndexer, LongFileIndex secondOperandIndexer)
    {
        this._leftIndexer = firstOperandIndexer;
        this._rightIndexer = secondOperandIndexer;
    }

    public int Compare(IndexBlockData x, IndexBlockData y)
    {
        var stringPartCoparisonResult = StringPartCoparison(x, y);
        if (stringPartCoparisonResult != 0) return stringPartCoparisonResult;
        return NumberPartCoparison(x, y);
    }

    public int StringPartCoparison(IndexBlockData x, IndexBlockData y)
    {
        var stringCacheCoparisonResult = StringCacheCoparison(x, y);
        if (stringCacheCoparisonResult.HasValue) return stringCacheCoparisonResult.Value;

        var stringValueCoparisonResult = StringValueComparison(x, y);
        if (stringValueCoparisonResult.HasValue) return stringValueCoparisonResult.Value;

        return 0;
    }

    public int NumberPartCoparison(IndexBlockData x, IndexBlockData y)
    {
        var numberLengthComparison = NumberLengthComparison(x, y);
        if (numberLengthComparison.HasValue) return numberLengthComparison.Value;

        var numberValueComparison = NumberValueComparison(x, y);
        if (numberValueComparison.HasValue) return numberValueComparison.Value;

        return 0;
    }

    private int? StringCacheCoparison(IndexBlockData x, IndexBlockData y)
    {
        for (int i = 0; i < IndexBlockData.CachedSymbolsCount; ++i)
        {
            var result = x.CachedStringStart[i] - y.CachedStringStart[i];
            if (result != 0) return result;
        }

        if ((this._leftIndexer == this._rightIndexer)
            && (x.StringStartPosition == y.StringStartPosition)
                && (x.StringEndPosition == y.StringEndPosition))
            return 0;

        var firstTotalLength = x.StringEndPosition - x.StringStartPosition;
        var secondTotalLength = y.StringEndPosition - y.StringStartPosition;

        if (firstTotalLength <= IndexBlockData.CachedSymbolsCount)
            if (secondTotalLength <= IndexBlockData.CachedSymbolsCount)
                return 0;

        if (firstTotalLength == IndexBlockData.CachedSymbolsCount)
            if (secondTotalLength > IndexBlockData.CachedSymbolsCount)
                return -1;

        if (secondTotalLength == IndexBlockData.CachedSymbolsCount)
            if (firstTotalLength > IndexBlockData.CachedSymbolsCount)
                return 1;

        return null;
    }

    private int? StringValueComparison(IndexBlockData x, IndexBlockData y)
    {
        using var firstIndexReader = x.GetStringPartReader(this._leftIndexer);
        using var secondIndexReader = y.GetStringPartReader(this._rightIndexer);

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

    private int? NumberLengthComparison(IndexBlockData x, IndexBlockData y)
    {
        if ((this._leftIndexer == this._rightIndexer)
            && (x.NumberStartPosition == y.NumberStartPosition)
                && (x.NumberEndPosition == y.NumberEndPosition))
            return 0;

        if ((x.NumberEndPosition != 0) && (y.NumberEndPosition == 0))
            return 1;
        if ((x.NumberEndPosition == 0) && (y.NumberEndPosition != 0))
            return -1;
        if ((x.NumberEndPosition == 0) && (y.NumberEndPosition == 0))
            return x.NumberStartPosition.CompareTo(y.NumberStartPosition);

        var firstTotalLength = x.NumberEndPosition - x.NumberStartPosition;
        var secondTotalLength = y.NumberEndPosition - y.NumberStartPosition;

        if (firstTotalLength > secondTotalLength) return 1;
        if (firstTotalLength < secondTotalLength) return -1;

        return null;
    }

    private int? NumberValueComparison(IndexBlockData x, IndexBlockData y)
    {
        using var firstIndexReader = x.GetNumberPartReader(this._leftIndexer);
        using var secondIndexReader = y.GetNumberPartReader(this._rightIndexer);

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
