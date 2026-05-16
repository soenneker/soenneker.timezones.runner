namespace Soenneker.TimeZones.Runner.Configuration;

public sealed record RunnerOptions
{
    public string Scope { get; init; } = "world";

    public string ExtractUrl { get; init; } = "https://planet.openstreetmap.org/pbf/planet-latest.osm.pbf";

    public string? ExtractListPath { get; init; }

    public string CacheDirectory { get; init; } = "artifacts/osm";

    public string OutputPath { get; init; } = Constants.DataFileRelativePath;

    public bool ForceDownload { get; init; }

    public bool IncludeAdminBoundaries { get; init; }

    public bool UsePyosmiumPrefilter { get; init; } = true;

    public string PythonVersion { get; init; } = "3.11";

    public bool AutoInstallPython { get; init; } = true;

    public int MinRingPoints { get; init; } = 4;

    public bool Verbose { get; init; }
}
