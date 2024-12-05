using System.Text;

namespace LongFileSort.Utilities.Options;

public class IndexerOptions
{

    public string SourceFilePath { get; set; }

    public Encoding SourceEncoding { get; set; }

    public string IndexFilePath { get; set; }
}
