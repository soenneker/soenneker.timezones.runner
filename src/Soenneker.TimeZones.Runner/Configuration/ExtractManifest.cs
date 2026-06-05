namespace Soenneker.TimeZones.Runner.Configuration;

/// <summary>
/// Represents the extract manifest record.
/// </summary>
public sealed record ExtractManifest
{
    /// <summary>
    /// Gets or sets extracts.
    /// </summary>
    public List<ExtractDefinition> Extracts { get; init; } = [];
}
