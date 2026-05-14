namespace Soenneker.TimeZones.Runner.Models;

public sealed record ExtractChecksumManifest
{
    public List<ExtractChecksum> Extracts { get; init; } = [];
}
