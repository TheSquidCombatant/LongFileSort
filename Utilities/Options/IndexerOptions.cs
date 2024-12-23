using System.Text;

namespace LongFileSort.Utilities.Options;

public class IndexerOptions
{
    public int CacheSizeLimitMegabytes { get; set; }

    public bool EnableParallelExecution { get; set; }

    public string SourceFilePath { get; set; }

    public Encoding SourceEncoding { get; set; }

    public string IndexFilePath { get; set; }
}
