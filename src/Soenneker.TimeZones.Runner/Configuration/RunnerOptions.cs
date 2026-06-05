namespace Soenneker.TimeZones.Runner.Configuration;

/// <summary>
/// Represents the runner options record.
/// </summary>
public sealed record RunnerOptions
{
    /// <summary>
    /// Gets or sets scope.
    /// </summary>
    public string Scope { get; init; } = "world";

    /// <summary>
    /// Gets or sets extract url.
    /// </summary>
    public string ExtractUrl { get; init; } = "https://planet.openstreetmap.org/pbf/planet-latest.osm.pbf";

    /// <summary>
    /// Gets or sets extract list path.
    /// </summary>
    public string? ExtractListPath { get; init; }

    /// <summary>
    /// Gets or sets cache directory.
    /// </summary>
    public string CacheDirectory { get; init; } = "artifacts/osm";

    /// <summary>
    /// Gets or sets output path.
    /// </summary>
    public string OutputPath { get; init; } = Constants.DataFileRelativePath;

    /// <summary>
    /// Gets or sets a value indicating whether force download.
    /// </summary>
    public bool ForceDownload { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether skip md5 checking.
    /// </summary>
    public bool SkipMd5Checking { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether include admin boundaries.
    /// </summary>
    public bool IncludeAdminBoundaries { get; init; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether use pyosmium prefilter.
    /// </summary>
    public bool UsePyosmiumPrefilter { get; init; } = true;

    /// <summary>
    /// Gets or sets python version.
    /// </summary>
    public string PythonVersion { get; init; } = "3.12";

    /// <summary>
    /// Gets or sets a value indicating whether auto install python.
    /// </summary>
    public bool AutoInstallPython { get; init; } = true;

    /// <summary>
    /// Gets or sets min ring points.
    /// </summary>
    public int MinRingPoints { get; init; } = 4;

    /// <summary>
    /// Gets or sets a value indicating whether verbose.
    /// </summary>
    public bool Verbose { get; init; }
}
