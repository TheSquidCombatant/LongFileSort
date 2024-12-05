using System;
using System.Collections.Generic;

namespace LongFileSort.Utilities.CrutchesAndBicycles;

/// <summary>
/// Just replace this interface with IList when it fully supports indexing with a long value.
/// </summary>
/// <remarks>
/// Also see, for example, this github ticket: https://github.com/dotnet/runtime/issues/12221.
/// </remarks>
public interface ILargeList<T>
{
    /// <summary>
    /// Just an indexer.
    /// </summary>
    public T this[long index] { get; set; }

    /// <summary>
    /// The number of elements contained in this collection.
    /// </summary>
    public long LongCount();

    /// <summary>
    /// Searches a range of elements and returns the zero-based index of the element.
    /// </summary>
    /// <remarks>
    /// Returns a negative value if the element was not found.
    /// </remarks>
    public long BinarySearch(
        long index,
        long count,
        T item,
        IComparer<T> comparer)
    {
        var leftBorder = index;
        var rightBorder = index + count - 1;
        if (rightBorder < leftBorder) throw new ArgumentOutOfRangeException(nameof(count));
        if (this.LongCount() <= leftBorder) throw new ArgumentOutOfRangeException(nameof(index));

        while (leftBorder + 1 < rightBorder)
        {
            var mid = (leftBorder + rightBorder) / 2;
            var element = this[mid];
            var comparison = comparer.Compare(item, element);

            if (comparison == 0) return mid;
            if (comparison > 0) leftBorder = mid;
            if (comparison < 0) rightBorder = mid;
        }

        if (comparer.Compare(item, this[leftBorder]) == 0) return leftBorder;
        if (comparer.Compare(item, this[rightBorder]) == 0) return rightBorder;

        return -1;
    }

    /// <summary>
    /// Ad Hoc sorting implementation. Don't judge strictly.
    /// </summary>
    /// <remarks>
    /// When switching to using IList, it will be seamlessly replaced by a standard implementation.
    /// </remarks>
    public void Sort(long index, long count, IComparer<T> comparer)
    {
        InnerSort(index, index + count - 1);

        void InnerSort(long leftBorder, long rightBorder)
        {
            if (leftBorder >= rightBorder) return;
            var mid = this[(leftBorder + rightBorder) / 2];
            var (left, right) = (leftBorder, rightBorder);

            while (left <= right)
            {
                while (comparer.Compare(this[left], mid) < 0) ++left;
                while (comparer.Compare(this[right], mid) > 0) --right;
                if (left <= right) this.Swap(left++, right--);
            }

            InnerSort(leftBorder, right);
            InnerSort(left, rightBorder);
        }
    }
}
