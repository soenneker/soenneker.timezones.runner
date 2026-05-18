namespace Soenneker.TimeZones.Runner.Configuration;

public sealed record ExtractDefinition
{
    public required string Name { get; init; }

    public required string Url { get; init; }

    public required string CacheFileName { get; init; }

    public string? Md5 { get; init; }

    public bool Enabled { get; init; } = true;
}
