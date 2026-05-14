namespace Soenneker.TimeZones.Runner.Models;

public sealed record ExtractStats
{
    public required string Name { get; init; }

    public required string CachePath { get; init; }

    public bool Downloaded { get; init; }

    public bool Md5Changed { get; init; }

    public string? UpstreamMd5 { get; init; }

    public long RelationsScanned { get; set; }

    public long TimezoneRelationsFound { get; set; }

    public long WaysLoaded { get; set; }

    public long NodesLoaded { get; set; }

    public long IncompleteRingsDropped { get; set; }
}
