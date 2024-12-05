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
        CreatorCheckerHelper.CheckRowsPattern(options);
        CreatorCheckerHelper.CheckStringsDuplication(options);
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

        if (string.IsNullOrEmpty(options.NumberPartDigits))
            throw new ArgumentOutOfRangeException(nameof(options.NumberPartDigits));

        if (PredefinedConstants.NumberPartStopSymbols.Any(options.NumberPartDigits.Contains))
            throw new ArgumentOutOfRangeException(nameof(options.NumberPartDigits));

        if (options.NumberPartLength < 1)
            throw new ArgumentOutOfRangeException(nameof(options.NumberPartLength));

        if ((options.NumberPartLengthVariation < 0) || (options.NumberPartLength <= options.NumberPartLengthVariation))
            throw new ArgumentOutOfRangeException(nameof(options.NumberPartLengthVariation));

        if (string.IsNullOrEmpty(options.StringPartSymbols))
            throw new ArgumentOutOfRangeException(nameof(options.StringPartSymbols));

        if (PredefinedConstants.StringPartStopSymbols.Any(options.StringPartSymbols.Contains))
            throw new ArgumentOutOfRangeException(nameof(options.StringPartSymbols));

        if (options.StringPartLength < 1)
            throw new ArgumentOutOfRangeException(nameof(options.StringPartLength));

        if ((options.StringPartLengthVariation < 0) || (options.StringPartLength <= options.StringPartLengthVariation))
            throw new ArgumentOutOfRangeException(nameof(options.StringPartLengthVariation));
    }

    private static void CheckFileSize(CreatorOptions options)
    {
        var actualFileSize = new FileInfo(options.SourceFilePath).Length;
        var min = options.SourceSizeBytes * (1 - (double)PredefinedConstants.FileSizeCheckDeviationPercentage / 100);
        var max = options.SourceSizeBytes * (1 + (double)PredefinedConstants.FileSizeCheckDeviationPercentage / 100);
        string exceptionMessage = "Looks like source file has an invalid size.";
        if ((actualFileSize < min) || (max < actualFileSize)) throw new IOException(exceptionMessage);
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

    private static void CheckRowsPattern(CreatorOptions options)
    {
        var encoding = Encoding.GetEncoding(options.SourceEncodingName);
        using var streamReader = new StreamReader(options.SourceFilePath, encoding);
        var exceptionMessage = "Looks like the source file is empty.";
        if (streamReader.EndOfStream) throw new FileLoadException(exceptionMessage);

        const int stateRowStart = 0;
        const int stateNumber = 1;
        const int stateDelimiter = 2;
        const int stateString = 3;
        const int stateEnding = 4;
        const int stateRowFinish = 5;

        var stateCurrent = stateRowStart;
        long symbolIndexInsideState = 0;
        long symbolIndexInsideFile = 0;
        exceptionMessage = "Looks like the source file rows format is broken in position {0}.";

        while (!streamReader.EndOfStream && (stateCurrent != stateRowFinish))
        {
            var nextChar = (char)streamReader.Read();
            switch (stateCurrent)
            {
                case stateString:
                    {
                        if (PredefinedConstants.SourceRowEnding[0] == nextChar)
                        {
                            stateCurrent = (PredefinedConstants.SourceRowEnding.Length > 1 ? stateEnding : stateRowFinish);
                            symbolIndexInsideState = 0;
                        }
                        break;
                    }
                case stateNumber:
                    {
                        if (PredefinedConstants.SourcePartsDelimiter[0] == nextChar)
                        {
                            stateCurrent = (PredefinedConstants.SourcePartsDelimiter.Length > 1 ? stateDelimiter : stateString);
                            symbolIndexInsideState = 0;
                            break;
                        }
                        if (PredefinedConstants.NumberPartStopSymbols.Contains(nextChar))
                        {
                            throw new FileLoadException(string.Format(exceptionMessage, symbolIndexInsideFile));
                        }
                        break;
                    }
                case stateRowStart:
                    {
                        if (PredefinedConstants.NumberPartStopSymbols.Contains(nextChar))
                        {
                            throw new FileLoadException(string.Format(exceptionMessage, symbolIndexInsideFile));
                        }
                        stateCurrent = stateNumber;
                        break;
                    }
                case stateDelimiter:
                    {
                        if (symbolIndexInsideState == PredefinedConstants.SourcePartsDelimiter.Length)
                        {
                            if (PredefinedConstants.StringPartStopSymbols.Contains(nextChar))
                            {
                                throw new FileLoadException(string.Format(exceptionMessage, symbolIndexInsideFile));
                            }
                            stateCurrent = stateString;
                            symbolIndexInsideState = 0;
                            break;
                        }
                        if (PredefinedConstants.SourcePartsDelimiter[(int)symbolIndexInsideState] != nextChar)
                        {
                            throw new FileLoadException(string.Format(exceptionMessage, symbolIndexInsideFile));
                        };
                        break;
                    }
                case stateEnding:
                    {
                        if (PredefinedConstants.SourceRowEnding[(int)symbolIndexInsideState] != nextChar)
                        {
                            throw new FileLoadException(string.Format(exceptionMessage, symbolIndexInsideFile));
                        }
                        if (symbolIndexInsideState == PredefinedConstants.SourceRowEnding.Length - 1)
                        {
                            stateCurrent = stateRowFinish;
                            break;
                        }
                        break;
                    }
            }
            ++symbolIndexInsideFile;
            ++symbolIndexInsideState;
        }

        if ((stateCurrent != stateRowFinish) && (stateCurrent != stateString))
            throw new FileLoadException(exceptionMessage);
        Console.WriteLine("Rows pattern is OK.");
    }

    private static void CheckStringsDuplication(CreatorOptions options)
    {
        var indexerOptions = new IndexerOptions()
        {
            SourceFilePath = options.SourceFilePath,
            SourceEncoding = Encoding.GetEncoding(options.SourceEncodingName),
            IndexFilePath = Path.Combine(options.ProcessingTemporaryFolder, $"index_{Guid.NewGuid()}.txt")
        };

        using var longFileIndex = new LongFileIndex(indexerOptions, true, true);
        var comparer = new IndexBlockComparer();
        (longFileIndex as ILargeList<IndexBlock>).Sort(0, longFileIndex.LongCount(), comparer);

        for (long i = 0; i < longFileIndex.LongCount() - 1; ++i)
            if (IndexBlockComparer.StringPartCoparison(longFileIndex[i], longFileIndex[i + 1]) == 0)
            {
                Console.WriteLine("Strings duplication is OK.");
                return;
            }

        throw new FileLoadException("File must contain rows with repeating string part.");
    }
}
