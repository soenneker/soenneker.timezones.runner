namespace Soenneker.TimeZones.Runner.Models;

public sealed record ExtractChecksum
{
    public required string Name { get; init; }

    public required string Url { get; init; }

    public required string CacheFileName { get; init; }

    public required string Md5 { get; init; }
}
