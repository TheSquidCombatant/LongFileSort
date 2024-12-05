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
    }

    private static void GenerateSortedFile(SorterOptions options)
    {
        var indexerOptions = new IndexerOptions()
        {
            SourceFilePath = options.SourceFilePath,
            SourceEncoding = Encoding.GetEncoding(options.SourceEncodingName),
            IndexFilePath = Path.Combine(options.ProcessingTemporaryFolder, $"index_{Guid.NewGuid()}.txt")
        };

        using var longFileIndex = new LongFileIndex(indexerOptions, true, false);
        var comparer = new IndexBlockComparer();
        longFileIndex.SortParallel(0, longFileIndex.LongCount(), comparer);
        longFileIndex.Dispose();

        IndexBlockParser.ConvertIndexToTargetFile(
            options.SourceFilePath,
            indexerOptions.IndexFilePath,
            options.TargetFilePath,
            indexerOptions.SourceEncoding);

        File.Delete(indexerOptions.IndexFilePath);
    }
}
