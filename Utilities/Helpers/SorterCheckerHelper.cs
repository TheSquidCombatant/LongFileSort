using LongFileSort.Utilities.CrutchesAndBicycles;
using LongFileSort.Utilities.Indexer;
using LongFileSort.Utilities.Options;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace LongFileSort.Utilities.Helpers;

public static class SorterCheckerHelper
{
    /// <summary>
    /// Triggers execution of file sotring check logic.
    /// </summary>
    public static void Process(SorterOptions options)
    {
        SorterCheckerHelper.ValidateSotringOptions(options);
        SorterCheckerHelper.CheckEncodingBom(options);
        SorterCheckerHelper.CheckRowsCount(options, out var source, out var target);
        SorterCheckerHelper.CheckRowsOrder(options, target);
        SorterCheckerHelper.CheckRowsAvailability(options, source, target);
        SorterCheckerHelper.CheckRowsOccurrences(options, source, target);
    }

    private static void ValidateSotringOptions(SorterOptions options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        if (!File.Exists(options.SourceFilePath))
            throw new FileNotFoundException($"File {options.SourceFilePath} not found.");

        if (Encoding.GetEncoding(options.SourceEncodingName) == null)
            throw new ArgumentOutOfRangeException(nameof(options.SourceEncodingName));

        if (!Directory.Exists(options.ProcessingTemporaryFolder))
            Directory.CreateDirectory(options.ProcessingTemporaryFolder);

        if (!File.Exists(options.TargetFilePath))
            File.Create(options.TargetFilePath, 1, FileOptions.RandomAccess).Close();

        if (options.CacheSizeLimitMegabytes < 1)
            throw new ArgumentOutOfRangeException(nameof(options.CacheSizeLimitMegabytes));
    }

    private static void CheckEncodingBom(SorterOptions options)
    {
        var encoding = Encoding.GetEncoding(options.SourceEncodingName);
        var expectedPreamble = encoding.GetPreamble();
        using var streamReaderSource = new StreamReader(options.SourceFilePath, encoding);
        var actualPreambleSource = new byte[expectedPreamble.Length];
        streamReaderSource.BaseStream.Read(actualPreambleSource, 0, actualPreambleSource.Length);
        using var streamReaderTarget = new StreamReader(options.TargetFilePath, encoding);
        var actualPreambleTarget = new byte[expectedPreamble.Length];
        streamReaderTarget.BaseStream.Read(actualPreambleTarget, 0, actualPreambleTarget.Length);
        var exceptionMessage = "Looks like target file encoding BOM does not match source file encoding BOM.";
        if (expectedPreamble.SequenceEqual(actualPreambleSource) != expectedPreamble.SequenceEqual(actualPreambleTarget))
            throw new FileLoadException(exceptionMessage);
        Console.WriteLine("Encoding BOM is OK.");
    }

    private static void CheckRowsCount(SorterOptions options, out LongFileIndex source, out LongFileIndex target)
    {
        var targetOptions = new IndexerOptions()
        {
            CacheSizeLimitMegabytes = options.CacheSizeLimitMegabytes,
            EnableParallelExecution = options.EnableParallelExecution,
            SourceFilePath = options.TargetFilePath,
            SourceEncoding = Encoding.GetEncoding(options.SourceEncodingName),
            IndexFilePath = Path.Combine(options.ProcessingTemporaryFolder, $"index_{Guid.NewGuid()}.txt")
        };

        target = new LongFileIndex(targetOptions, true, true);

        var sourceOptions = new IndexerOptions()
        {
            CacheSizeLimitMegabytes = options.CacheSizeLimitMegabytes,
            EnableParallelExecution = options.EnableParallelExecution,
            SourceFilePath = options.SourceFilePath,
            SourceEncoding = Encoding.GetEncoding(options.SourceEncodingName),
            IndexFilePath = Path.Combine(options.ProcessingTemporaryFolder, $"index_{Guid.NewGuid()}.txt")
        };

        source = new LongFileIndex(sourceOptions, true, true);

        const string exceptionMessage = "Looks like source and target file have different rows count.";
        if (target.LongCount() != source.LongCount()) throw new IOException(exceptionMessage);
        Console.WriteLine("Rows count is OK.");
    }

    private static void CheckRowsOrder(SorterOptions options, LongFileIndex target)
    {
        var violationIndex = target.IsSorted(0, target.LongCount(), new IndexBlockComparer());
        const string exceptionMessage = "Looks like target file was not sorted properly at row {0}.";
        if (0 <= violationIndex) throw new IOException(string.Format(exceptionMessage, violationIndex + 1));
        Console.WriteLine("Rows order is OK.");
    }

    private static void CheckRowsAvailability(SorterOptions options, LongFileIndex source, LongFileIndex target)
    {
        var comparer = new IndexBlockComparer();

        for (long i = 0; i < source.LongCount(); ++i)
        {
            var j = (target as ILargeList<IndexBlock>).BinarySearch(
                0,
                target.LongCount(),
                source[i],
                comparer);
            if (j < 0)
                throw new Exception($"Looks like target file does not contain row number {i + 1} from source file.");
        }

        Console.WriteLine("Rows availability is OK.");
    }

    private static void CheckRowsOccurrences(SorterOptions options, LongFileIndex source, LongFileIndex target)
    {
        var comparer = new IndexBlockComparer();
        if (options.EnableParallelExecution) source.SortParallel(0, source.LongCount(), comparer);
        else (source as ILargeList<IndexBlock>).Sort(0, source.LongCount(), comparer);

        var sourceIndex = 0;
        var targetIndex = 0;

        while ((sourceIndex < source.LongCount()) && (targetIndex < target.LongCount()))
        {
            var sourceElement = source[sourceIndex];
            var sourceEnd = sourceIndex + 1;

            while (sourceEnd < source.LongCount())
            {
                if (comparer.Compare(sourceElement, source[sourceEnd]) != 0) break;
                ++sourceEnd;
            }

            var targetElement = target[targetIndex];
            var targetEnd = targetIndex + 1;

            while (targetEnd < target.LongCount())
            {
                if (comparer.Compare(targetElement, target[targetEnd]) != 0) break;
                ++targetEnd;
            }

            const string messageAvailabilityException = "Looks like target contains row number {0}" +
                " that is not present in source.";

            if (comparer.Compare(sourceElement, targetElement) != 0)
                throw new Exception(string.Format(messageAvailabilityException, targetIndex + 1));

            const string messageOccurrencesException = "Looks like target contains row number {0}" +
                " which has a different count of occurrences in source.";

            if ((sourceEnd - sourceIndex) != (targetEnd - targetIndex))
                throw new Exception(string.Format(messageOccurrencesException, targetIndex + 1));

            sourceIndex = sourceEnd;
            targetIndex = targetEnd;
        }

        Console.WriteLine("Rows occurrences is OK.");
    }
}
