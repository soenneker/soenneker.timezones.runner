namespace Soenneker.TimeZones.Runner.Configuration;

/// <summary>
/// Represents the default extract manifest.
/// </summary>
public static class DefaultExtractManifest
{
    /// <summary>
    /// Gets world.
    /// </summary>
    public static ExtractManifest World { get; } = new()
    {
        Extracts =
        [
            new ExtractDefinition
            {
                Name = "OpenStreetMap Weekly Planet PBF",
                Url = "https://planet.openstreetmap.org/pbf/planet-latest.osm.pbf",
                CacheFileName = "planet-latest.osm.pbf"
            }
        ]
    };

    /// <summary>
    /// Gets or sets continent.
    /// </summary>
    public static ExtractManifest Continent => World;
}
