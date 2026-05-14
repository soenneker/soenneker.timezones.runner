namespace Soenneker.TimeZones.Runner.Models;

public sealed record GenerationStats
{
    public int ExtractsConfigured { get; set; }

    public int ExtractsDownloaded { get; set; }

    public int ExtractsReused { get; set; }

    public int ExtractsProcessed { get; set; }

    public List<ExtractStats> PerExtract { get; } = [];

    public int GlobalTimezoneFeatureCount { get; set; }

    public long GlobalIncompleteRingCount => PerExtract.Sum(x => x.IncompleteRingsDropped);
}
