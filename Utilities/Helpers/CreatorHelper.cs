using LongFileSort.Utilities.Options;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace LongFileSort.Utilities.Helpers;

public static class CreatorHelper
{
    /// <summary>
    /// Triggers execution of file creation logic.
    /// </summary>
    public static void Process(CreatorOptions options)
    {
        CreatorHelper.ValidateCreatingOptions(options);
        CreatorHelper.CreateRandomPart(options, out var randomStartSeed, out var createdRowsCount);
        CreatorHelper.CreateDuplicationsPart(options, randomStartSeed, createdRowsCount);
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

    private static void CreateRandomPart(CreatorOptions options, out long randomStartSeed, out long createdRowsCount)
    {
        createdRowsCount = 0;
        var random = new Random(DateTime.Now.Millisecond);
        randomStartSeed = random.NextInt64(0, long.MaxValue);

        var encoding = Encoding.GetEncoding(options.SourceEncodingName);
        using var streamWriter = new StreamWriter(options.SourceFilePath, false, encoding);
        streamWriter.AutoFlush = (options.SourceSizeBytes < PredefinedConstants.AutoFlushOutputFileSizeLimit);

        if (!options.SourceOutputWithBom)
        {
            var oldAutoFlush = streamWriter.AutoFlush;
            streamWriter.AutoFlush = true;
            if (streamWriter.BaseStream.Position > 0) streamWriter.BaseStream.Position = 0;
            streamWriter.AutoFlush = oldAutoFlush;
        }

        while (streamWriter.BaseStream.Position < options.SourceSizeBytes / 2)
        {
            var currentStartSeed = (randomStartSeed + createdRowsCount) % int.MaxValue;
            random = new Random((int)currentStartSeed);

            var digitsCount = 0;
            var isFirstDigit = true;
            var previousDigit = (char)0;

            var numberLengthVariation = random.NextInt64(-options.NumberPartLengthVariation, options.NumberPartLengthVariation + 1);
            var numberPartLength = options.NumberPartLength + numberLengthVariation;
            if (numberPartLength < 1) numberPartLength = 1;

            while (digitsCount < numberPartLength)
            {
                var index = random.Next(options.NumberPartDigits.Length);
                var digit = options.NumberPartDigits[index];
                if (PredefinedConstants.NumberPartForbiddenDuplicateSymbols.Contains(digit) && (previousDigit == digit)) continue;
                if (isFirstDigit && PredefinedConstants.NumberPartForbiddenFirstSymbols.Contains(digit)) continue;

                streamWriter.Write(digit);
                if (streamWriter.BaseStream.Position > (options.SourceSizeBytes / 2)) break;

                previousDigit = digit;
                isFirstDigit = false;
                ++digitsCount;
            }
            streamWriter.Write(PredefinedConstants.SourcePartsDelimiter);

            random = new Random((int)currentStartSeed);
            var symbolsCount = 0;
            var isFirstSymbol = true;
            var previousSymbol = (char)0;

            var stringLengthVariation = random.NextInt64(-options.StringPartLengthVariation, options.StringPartLengthVariation + 1);
            var stringPartLength = options.StringPartLength + stringLengthVariation;
            if (stringPartLength < 1) stringPartLength = 1;

            while (symbolsCount < stringPartLength)
            {
                var index = random.Next(options.StringPartSymbols.Length);
                var symbol = options.StringPartSymbols[index];
                if (PredefinedConstants.StringPartForbiddenDuplicateSymbols.Contains(symbol) && (previousSymbol == symbol)) continue;
                if (isFirstSymbol && PredefinedConstants.StringPartForbiddenFirstSymbols.Contains(symbol)) continue;

                streamWriter.Write(isFirstSymbol ? char.ToUpper(symbol) : symbol);
                if (streamWriter.BaseStream.Position > (options.SourceSizeBytes / 2)) break;

                previousSymbol = symbol;
                isFirstSymbol = false;
                ++symbolsCount;
            }
            streamWriter.Write(PredefinedConstants.SourceRowEnding);
            ++createdRowsCount;
        }
    }

    private static void CreateDuplicationsPart(CreatorOptions options, long randomStartSeed, long createdRowsCount)
    {
        var mainRandom = new Random(DateTime.Now.Millisecond);

        var encoding = Encoding.GetEncoding(options.SourceEncodingName);
        using var streamWriter = new StreamWriter(options.SourceFilePath, true, encoding);
        streamWriter.AutoFlush = (options.SourceSizeBytes < PredefinedConstants.AutoFlushOutputFileSizeLimit);

        while (streamWriter.BaseStream.Position < options.SourceSizeBytes)
        {
            var currentStartSeed = (randomStartSeed + mainRandom.NextInt64(createdRowsCount)) % int.MaxValue;
            var random = new Random((int)currentStartSeed);

            var digitsCount = 0;
            var isFirstDigit = true;
            var previousDigit = (char)0;

            var numberLengthVariation = random.NextInt64(-options.NumberPartLengthVariation, options.NumberPartLengthVariation + 1);
            var numberPartLength = options.NumberPartLength + numberLengthVariation;
            if (numberPartLength < 1) numberPartLength = 1;

            while (digitsCount < numberPartLength)
            {
                var index = random.Next(options.NumberPartDigits.Length);
                var digit = options.NumberPartDigits[index];
                if (PredefinedConstants.NumberPartForbiddenDuplicateSymbols.Contains(digit) && (previousDigit == digit)) continue;
                if (isFirstDigit && PredefinedConstants.NumberPartForbiddenFirstSymbols.Contains(digit)) continue;

                streamWriter.Write(digit);
                if (streamWriter.BaseStream.Position > options.SourceSizeBytes) break;

                previousDigit = digit;
                isFirstDigit = false;
                ++digitsCount;
            }
            streamWriter.Write(PredefinedConstants.SourcePartsDelimiter);

            currentStartSeed = (randomStartSeed + mainRandom.NextInt64(createdRowsCount)) % int.MaxValue;
            random = new Random((int)currentStartSeed);

            var symbolsCount = 0;
            var isFirstSymbol = true;
            var previousSymbol = (char)0;

            var stringLengthVariation = random.NextInt64(-options.StringPartLengthVariation, options.StringPartLengthVariation + 1);
            var stringPartLength = options.StringPartLength + stringLengthVariation;
            if (stringPartLength < 1) stringPartLength = 1;

            while (symbolsCount < stringPartLength)
            {
                var index = random.Next(options.StringPartSymbols.Length);
                var symbol = options.StringPartSymbols[index];
                if (PredefinedConstants.StringPartForbiddenDuplicateSymbols.Contains(symbol) && (previousSymbol == symbol)) continue;
                if (isFirstSymbol && PredefinedConstants.StringPartForbiddenFirstSymbols.Contains(symbol)) continue;

                streamWriter.Write(isFirstSymbol ? char.ToUpper(symbol) : symbol);
                if (streamWriter.BaseStream.Position > options.SourceSizeBytes) break;

                previousSymbol = symbol;
                isFirstSymbol = false;
                ++symbolsCount;
            }
            streamWriter.Write(PredefinedConstants.SourceRowEnding);
        }
    }
}
