namespace Soenneker.TimeZones.Runner.Configuration;

public sealed record RunnerOptions
{
    public string Scope { get; init; } = "world";

    public string ExtractUrl { get; init; } = "https://download.geofabrik.de/north-america/us-latest.osm.pbf";

    public string? ExtractListPath { get; init; }

    public string CacheDirectory { get; init; } = "artifacts/osm";

    public string OutputPath { get; init; } = Constants.DataFileRelativePath;

    public bool ForceDownload { get; init; }

    public bool IncludeAdminBoundaries { get; init; }

    public int MinRingPoints { get; init; } = 4;

    public bool Verbose { get; init; }
}
