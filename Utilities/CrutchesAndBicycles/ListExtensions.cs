using LongFileSort.Utilities.Indexer;
using LongFileSort.Utilities.Options;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LongFileSort.Utilities.CrutchesAndBicycles;

/// <summary>
/// Some extension methods for list below.
/// </summary>
public static class ListExtensions
{
    /// <summary>
    /// Swaps two elements.
    /// </summary>
    public static void Swap<T>(
        this ILargeList<T> largeList,
        long oneIndex,
        long otherIndex)
    {
        if (oneIndex == otherIndex) return;
        var temp = largeList[oneIndex];
        largeList[oneIndex] = largeList[otherIndex];
        largeList[otherIndex] = temp;
    }

    /// <summary>
    /// Checks a range of elements and returns a zero-based index of the first order violation.
    /// </summary>
    /// <remarks>
    /// Returns a negative value if the order has not been violated.
    /// </remarks>
    public static long IsSorted<T>(
        this ILargeList<T> largeList,
        long index,
        long count,
        IComparer<T> comparer)
    {
        for (var i = index + 1; i < index + count; ++i)
            if (comparer.Compare(largeList[i - 1], largeList[i]) > 0)
                return i;
        return -1;
    }

    /// <summary>
    /// Tries to speed up sort using all CPU cores.
    /// </summary>
    public static void SortParallel<T>(
        this ILargeList<T> largeList,
        long index,
        long count,
        IComparer<T> comparer)
    {
        InnerSortParallel(index, index + count - 1, comparer, degree: 1);

        void InnerSortParallel(long leftBorder, long rightBorder, IComparer<T> comparer, int degree)
        {
            if (leftBorder >= rightBorder) return;
            var mid = largeList[(leftBorder + rightBorder) / 2];
            var (left, right) = (leftBorder, rightBorder);

            while (left <= right)
            {
                while (comparer.Compare(largeList[left], mid) < 0) ++left;
                while (comparer.Compare(largeList[right], mid) > 0) --right;
                if (left <= right) largeList.Swap(left++, right--);
            }

            if (Math.Pow(2, degree) > PredefinedConstants.MaximumDegreeOfParallelism)
            {
                largeList.Sort(leftBorder, right - leftBorder + 1, comparer);
                largeList.Sort(left, rightBorder - left + 1, comparer);
            }
            else
            {
                void action() => InnerSortParallel(leftBorder, right, comparer, degree + 1);
                var task = Task.Factory.StartNew(action, TaskCreationOptions.LongRunning);
                InnerSortParallel(left, rightBorder, comparer, degree + 1);
                task.Wait();
            }
        }
    }
}

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
        InnerQuickSort(index, index + count - 1, comparer);

        void InnerQuickSort(long leftBorder, long rightBorder, IComparer<T> comparer)
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

            const long threshold = PredefinedConstants.SortBufferingThreshold;

            if (right - leftBorder > threshold) InnerQuickSort(leftBorder, right, comparer);
            else InnerCacheSort(leftBorder, right, comparer);

            if (rightBorder - left > threshold) InnerQuickSort(left, rightBorder, comparer);
            else InnerCacheSort(left, rightBorder, comparer);
        }

        void InnerCacheSort(long leftBorder, long rightBorder, IComparer<T> comparer)
        {
            if (leftBorder >= rightBorder) return;
            var count = (int)(rightBorder - leftBorder + 1);
            var buffer = GC.AllocateUninitializedArray<T>(count);
            for (int i = 0; i < count; ++i) buffer[i] = this[leftBorder + i];
            Array.Sort(buffer, comparer);
            for (int i = 0; i < count; ++i) this[leftBorder + i] = buffer[i];
        }
    }
}
