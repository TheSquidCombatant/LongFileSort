using LongFileSort.Utilities.Options;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LongFileSort.Utilities.CrutchesAndBicycles;

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
        InnerSortParallel(index, index + count - 1, 0);

        void InnerSortParallel(long leftBorder, long rightBorder, int degree)
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

            if (Math.Pow(2, degree) > PredefinedConstants.SortMaximumDegreeOfParallelism)
            {
                largeList.Sort(leftBorder, right - leftBorder + 1, comparer);
                largeList.Sort(left, rightBorder - left + 1, comparer);
            }
            else
            {
                var task1 = Task.Factory.StartNew(() => InnerSortParallel(leftBorder, right, degree + 1));
                InnerSortParallel(left, rightBorder, degree + 1);
                task1.Wait();
            }
        }
    }

    /// <summary>
    /// Returns the last node in the list that matches the condition, otherwise null.
    /// </summary>
    public static LinkedListNode<T> FindLastNode<T>(
        this LinkedList<T> linkedList,
        Func<T, bool> predicate)
    {
        var currentNode = linkedList.Last;
        while (currentNode != null)
        {
            if (predicate(currentNode.Value)) return currentNode;
            currentNode = currentNode.Previous;
        }
        return null;
    }
}
