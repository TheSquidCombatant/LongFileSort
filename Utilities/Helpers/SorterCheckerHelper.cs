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
        SorterCheckerHelper.CheckRowsOrder(options);
        SorterCheckerHelper.CheckRowsAvailability(options);
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

    private static void CheckRowsOrder(SorterOptions options)
    {
        var indexerOptions = new IndexerOptions()
        {
            SourceFilePath = options.TargetFilePath,
            SourceEncoding = Encoding.GetEncoding(options.SourceEncodingName),
            IndexFilePath = Path.Combine(options.ProcessingTemporaryFolder, $"index_{Guid.NewGuid()}.txt")
        };

        using var longFileIndex = new LongFileIndex(indexerOptions, true, true);
        var comparer = new IndexBlockComparer();
        var violationIndex = longFileIndex.IsSorted(0, longFileIndex.LongCount(), comparer);

        const string exceptionMessage = "Looks like target file was not sorted properly at row {0}.";
        if (0 <= violationIndex) throw new IOException(string.Format(exceptionMessage, violationIndex + 1));
        Console.WriteLine("Rows order in sorted file is OK.");
    }

    private static void CheckRowsAvailability(SorterOptions options)
    {
        var indexerOptionsSource = new IndexerOptions()
        {
            SourceFilePath = options.SourceFilePath,
            SourceEncoding = Encoding.GetEncoding(options.SourceEncodingName),
            IndexFilePath = Path.Combine(options.ProcessingTemporaryFolder, $"index_{Guid.NewGuid()}.txt")
        };

        using var fileIndexSource = new LongFileIndex(indexerOptionsSource, true, true);

        var indexerOptionTarget = new IndexerOptions()
        {
            SourceFilePath = options.TargetFilePath,
            SourceEncoding = Encoding.GetEncoding(options.SourceEncodingName),
            IndexFilePath = Path.Combine(options.ProcessingTemporaryFolder, $"index_{Guid.NewGuid()}.txt")
        };

        using var fileIndexTarget = new LongFileIndex(indexerOptionTarget, true, true);

        if (fileIndexSource.LongCount() != fileIndexTarget.LongCount())
            throw new Exception("Looks like source and target file have different rows count.");

        var comparer = new IndexBlockComparer();

        for (long i = 0; i < fileIndexSource.LongCount(); ++i)
        {
            var j = (fileIndexTarget as ILargeList<IndexBlock>).BinarySearch(
                0,
                fileIndexTarget.LongCount(),
                fileIndexSource[i],
                comparer);
            if (j < 0)
                throw new Exception($"Looks like target file does not contain row number {i + 1} from source file.");
        }

        Console.WriteLine("Rows availability in sorted file is OK.");
    }
}
