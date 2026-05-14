namespace Soenneker.TimeZones.Runner.Configuration;

public sealed record ExtractManifest
{
    public List<ExtractDefinition> Extracts { get; init; } = [];
}
