namespace Soenneker.TimeZones.Runner.Configuration;

public static class DefaultExtractManifest
{
    public static ExtractManifest World { get; } = new()
    {
        Extracts =
        [
            new ExtractDefinition { Name = "Africa", Url = "https://download.geofabrik.de/africa-latest.osm.pbf", CacheFileName = "africa-latest.osm.pbf" },
            new ExtractDefinition { Name = "Antarctica", Url = "https://download.geofabrik.de/antarctica-latest.osm.pbf", CacheFileName = "antarctica-latest.osm.pbf" },
            new ExtractDefinition { Name = "Asia", Url = "https://download.geofabrik.de/asia-latest.osm.pbf", CacheFileName = "asia-latest.osm.pbf" },
            new ExtractDefinition { Name = "Australia/Oceania", Url = "https://download.geofabrik.de/australia-oceania-latest.osm.pbf", CacheFileName = "australia-oceania-latest.osm.pbf" },
            new ExtractDefinition { Name = "Central America", Url = "https://download.geofabrik.de/central-america-latest.osm.pbf", CacheFileName = "central-america-latest.osm.pbf" },
            new ExtractDefinition { Name = "Europe", Url = "https://download.geofabrik.de/europe-latest.osm.pbf", CacheFileName = "europe-latest.osm.pbf" },
            new ExtractDefinition { Name = "North America", Url = "https://download.geofabrik.de/north-america-latest.osm.pbf", CacheFileName = "north-america-latest.osm.pbf" },
            new ExtractDefinition { Name = "South America", Url = "https://download.geofabrik.de/south-america-latest.osm.pbf", CacheFileName = "south-america-latest.osm.pbf" }
        ]
    };

    public static ExtractManifest Continent => World;
}
