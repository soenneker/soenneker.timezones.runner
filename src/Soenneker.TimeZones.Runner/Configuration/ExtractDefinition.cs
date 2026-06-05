namespace Soenneker.TimeZones.Runner.Configuration;

/// <summary>
/// Represents the extract definition record.
/// </summary>
public sealed record ExtractDefinition
{
    /// <summary>
    /// Gets or sets name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets url.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// Gets or sets cache file name.
    /// </summary>
    public required string CacheFileName { get; init; }

    /// <summary>
    /// Gets or sets md5.
    /// </summary>
    public string? Md5 { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether enabled.
    /// </summary>
    public bool Enabled { get; init; } = true;
}
