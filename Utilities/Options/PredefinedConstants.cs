﻿using System;

namespace LongFileSort.Utilities.Options;

internal static class PredefinedConstants
{
    public static readonly char[] NumberPartStopSymbols = [(char)0, '\n', '.', ' '];

    public static readonly char[] StringPartStopSymbols = [(char)0, '\n', '.'];

    public static readonly string SourcePartsDelimiter = ". ";

    public static readonly string SourceRowEnding = "\n";

    public static readonly char StringCacheFiller = (char)0;

    public static readonly byte StringPartCacheSymbolsCount = 16;

    public static readonly string NumberPartForbiddenFirstSymbols = "0";

    public static readonly string StringPartForbiddenFirstSymbols = " ";

    public static readonly string NumberPartForbiddenDuplicateSymbols = "";

    public static readonly string StringPartForbiddenDuplicateSymbols = " ";

    public static readonly long AutoFlushOutputFileSizeLimit = 1024;

    public static readonly int DefaultFileStreamBufferSize = 4096;

    public static readonly int FileStreamBufferPageSize = 32768;

    public static readonly int FileStreamBufferPagesCount = Environment.ProcessorCount * 2;

    public static readonly int SortMaximumDegreeOfParallelism = Environment.ProcessorCount;

    public static readonly byte FileSizeCheckDeviationPercentage = 5;
}
