namespace LongFileSort.Utilities.Options;

public class CreatorOptions
{
    public string SourceFilePath { get; set; }

    public string SourceEncodingName { get; set; }

    public long SourceSizeBytes { get; set; }

    public bool SourceOutputWithBom { get; set; }

    public string ProcessingTemporaryFolder { get; set; }

    public string NumberPartDigits { get; set; }

    public long NumberPartLength { get; set; }

    public long NumberPartLengthVariation { get; set; }

    public string StringPartSymbols { get; set; }

    public long StringPartLength { get; set; }

    public long StringPartLengthVariation { get; set; }
}
