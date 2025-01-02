using LongFileSort.Utilities.CrutchesAndBicycles;
using LongFileSort.Utilities.Indexer;
using LongFileSort.Utilities.Options;
using System;
using System.IO;
using System.Text;

namespace LongFileSort.Utilities.Helpers;

public static class SorterHelper
{
    /// <summary>
    /// Triggers execution of file sotring logic.
    /// </summary>
    public static void Process(SorterOptions options)
    {
        SorterHelper.ValidateSotringOptions(options);
        SorterHelper.GenerateSortedFile(options);
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

    private static void GenerateSortedFile(SorterOptions options)
    {
        var indexFilePath = Path.Combine(options.ProcessingTemporaryFolder, $"index_{Guid.NewGuid()}.txt");
        var encoding = Encoding.GetEncoding(options.SourceEncodingName);

        var rowsTotalCount = IndexBlockParser.ConvertDataToIndexFile(
            options.SourceFilePath,
            indexFilePath,
            encoding,
            append: false);

        var rowsTotalLength = new FileInfo(options.SourceFilePath).Length;
        var rowsAverageLength = rowsTotalLength / rowsTotalCount;
        var enableParallelExecution = PredefinedConstants.ParallelThresholdRowLengthBytes < rowsAverageLength;

        var indexerOptions = new IndexerOptions()
        {
            CacheSizeLimitMegabytes = options.CacheSizeLimitMegabytes,
            EnableParallelExecution = enableParallelExecution,
            SourceFilePath = options.SourceFilePath,
            SourceEncoding = encoding,
            IndexFilePath = indexFilePath
        };

        using var longFileIndex = new LongFileIndex(indexerOptions, false, false);
        var comparer = new IndexBlockComparer();
        if (enableParallelExecution) longFileIndex.SortParallel(0, longFileIndex.LongCount(), comparer);
        else (longFileIndex as ILargeList<IndexBlock>).Sort(0, longFileIndex.LongCount(), comparer);
        longFileIndex.Dispose();

        var rowsResultCount = IndexBlockParser.ConvertIndexToDataFile(
            options.SourceFilePath,
            indexerOptions.IndexFilePath,
            options.TargetFilePath,
            indexerOptions.SourceEncoding);

        File.Delete(indexerOptions.IndexFilePath);
        const string mismatchMessage = "The number of rows before and after sorting does not match.";
        if (rowsTotalCount != rowsResultCount) throw new Exception(mismatchMessage);
    }
}
