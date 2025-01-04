using LongFileSort.Utilities.CrutchesAndBicycles;
using LongFileSort.Utilities.Indexer;
using LongFileSort.Utilities.Options;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace LongFileSort.Utilities.Helpers;

public static class CreatorCheckerHelper
{
    /// <summary>
    /// Triggers execution of file creation check logic.
    /// </summary>
    public static void Process(CreatorOptions options)
    {
        CreatorCheckerHelper.ValidateCreatingOptions(options);
        CreatorCheckerHelper.CheckFileSize(options);
        CreatorCheckerHelper.CheckEncodingBom(options);
        CreatorCheckerHelper.CheckRowsPattern(options, out var index);
        CreatorCheckerHelper.CheckStringsDuplication(index);
    }

    private static void ValidateCreatingOptions(CreatorOptions options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        if (!Directory.Exists(options.ProcessingTemporaryFolder))
            Directory.CreateDirectory(options.ProcessingTemporaryFolder);

        if (!File.Exists(options.SourceFilePath))
            File.Create(options.SourceFilePath, 1, FileOptions.RandomAccess).Close();

        if (Encoding.GetEncoding(options.SourceEncodingName) == null)
            throw new ArgumentOutOfRangeException(nameof(options.SourceEncodingName));

        if (options.SourceSizeBytes < 1)
            throw new ArgumentOutOfRangeException(nameof(options.SourceSizeBytes));
    }

    private static void CheckFileSize(CreatorOptions options)
    {
        var sourceFileInfo = new FileInfo(options.SourceFilePath);
        var exceptionNotFound = $"Looks like source file {sourceFileInfo.FullName} not found.";
        if (!sourceFileInfo.Exists) throw new FileNotFoundException(exceptionNotFound);
        var exceptionIsEmpty = $"Looks like source file {sourceFileInfo.FullName} is empty.";
        if (sourceFileInfo.Length == 0) throw new IOException(exceptionIsEmpty);
        var min = options.SourceSizeBytes * (1 - (double)PredefinedConstants.FileSizeCheckDeviationPercentage / 100);
        var max = options.SourceSizeBytes * (1 + (double)PredefinedConstants.FileSizeCheckDeviationPercentage / 100);
        var exceptionMessage = $"Looks like source file {sourceFileInfo.FullName} has an invalid size.";
        if ((sourceFileInfo.Length < min) || (max < sourceFileInfo.Length)) throw new IOException(exceptionMessage);
        Console.WriteLine("File size is OK.");
    }

    private static void CheckEncodingBom(CreatorOptions options)
    {
        var encoding = Encoding.GetEncoding(options.SourceEncodingName);
        using var streamReader = new StreamReader(options.SourceFilePath, encoding);
        var expectedPreamble = encoding.GetPreamble();
        var actualPreamble = new byte[expectedPreamble.Length];
        streamReader.BaseStream.Read(actualPreamble, 0, actualPreamble.Length);
        var exceptionMessage = "Looks like source file encoding BOM does not match the settings.";
        if (expectedPreamble.SequenceEqual(actualPreamble) != options.SourceOutputWithBom)
            throw new FileLoadException(exceptionMessage);
        Console.WriteLine("Encoding BOM is OK.");
    }

    private static void CheckRowsPattern(CreatorOptions options, out LongFileIndex index)
    {
        var indexerOptions = new IndexerOptions()
        {
            CacheSizeLimitMegabytes = 4,
            EnableParallelExecution = false,
            SourceFilePath = options.SourceFilePath,
            SourceEncoding = Encoding.GetEncoding(options.SourceEncodingName),
            IndexFilePath = Path.Combine(options.ProcessingTemporaryFolder, $"index_{Guid.NewGuid()}.txt"),
        };

        index = new LongFileIndex(indexerOptions, true, true);

        Console.WriteLine("Rows pattern is OK.");
    }

    private static void CheckStringsDuplication(LongFileIndex index)
    {
        using var longFileIndex = index;
        var comparer = new IndexBlockComparer(longFileIndex);
        (longFileIndex as ILargeList<IndexBlockData>).Sort(0, longFileIndex.LongCount(), comparer);

        for (long i = 0; i < longFileIndex.LongCount() - 1; ++i)
        {
            if (comparer.StringPartCoparison(longFileIndex[i], longFileIndex[i + 1]) == 0)
            {
                Console.WriteLine("Strings duplication is OK.");
                return;
            }
        }

        throw new FileLoadException("File must contain rows with repeating string part.");
    }
}
