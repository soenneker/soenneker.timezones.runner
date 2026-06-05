namespace Soenneker.TimeZones.Runner.Models;

/// <summary>
/// Represents the generation stats record.
/// </summary>
public sealed record GenerationStats
{
    /// <summary>
    /// Gets or sets extracts configured.
    /// </summary>
    public int ExtractsConfigured { get; set; }

    /// <summary>
    /// Gets or sets extracts downloaded.
    /// </summary>
    public int ExtractsDownloaded { get; set; }

    /// <summary>
    /// Gets or sets extracts reused.
    /// </summary>
    public int ExtractsReused { get; set; }

    /// <summary>
    /// Gets or sets extracts processed.
    /// </summary>
    public int ExtractsProcessed { get; set; }

    /// <summary>
    /// Gets per extract.
    /// </summary>
    public List<ExtractStats> PerExtract { get; } = [];

    /// <summary>
    /// Gets or sets global timezone feature count.
    /// </summary>
    public int GlobalTimezoneFeatureCount { get; set; }

    /// <summary>
    /// Gets or sets global incomplete ring count.
    /// </summary>
    public long GlobalIncompleteRingCount => PerExtract.Sum(x => x.IncompleteRingsDropped);
}
