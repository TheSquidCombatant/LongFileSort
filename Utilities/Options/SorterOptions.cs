﻿namespace LongFileSort.Utilities.Options;

public class SorterOptions
{
    public int CacheSizeLimitMegabytes { get; set; }

    public bool EnableParallelExecution { get; set; }

    public string SourceFilePath { get; set; }

    public string SourceEncodingName { get; set; }

    public string TargetFilePath { get; set; }

    public string ProcessingTemporaryFolder { get; set; }
}
