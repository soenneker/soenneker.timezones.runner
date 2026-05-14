namespace Soenneker.TimeZones.Runner.Configuration;

public static class DefaultExtractManifest
{
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

    public static ExtractManifest Continent => World;
}
