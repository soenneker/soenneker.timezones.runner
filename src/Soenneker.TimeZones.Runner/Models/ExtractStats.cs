namespace Soenneker.TimeZones.Runner.Models;

/// <summary>
/// Represents the extract stats record.
/// </summary>
public sealed record ExtractStats
{
    /// <summary>
    /// Gets or sets name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets cache path.
    /// </summary>
    public required string CachePath { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether downloaded.
    /// </summary>
    public bool Downloaded { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether md5 changed.
    /// </summary>
    public bool Md5Changed { get; init; }

    /// <summary>
    /// Gets or sets upstream md5.
    /// </summary>
    public string? UpstreamMd5 { get; init; }

    /// <summary>
    /// Gets or sets relations scanned.
    /// </summary>
    public long RelationsScanned { get; set; }

    /// <summary>
    /// Gets or sets timezone relations found.
    /// </summary>
    public long TimezoneRelationsFound { get; set; }

    /// <summary>
    /// Gets or sets ways loaded.
    /// </summary>
    public long WaysLoaded { get; set; }

    /// <summary>
    /// Gets or sets nodes loaded.
    /// </summary>
    public long NodesLoaded { get; set; }

    /// <summary>
    /// Gets or sets incomplete rings dropped.
    /// </summary>
    public long IncompleteRingsDropped { get; set; }
}
