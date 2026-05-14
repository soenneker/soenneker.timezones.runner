using Soenneker.TimeZones.Runner.Models;

namespace Soenneker.TimeZones.Runner.Validation;

public static class TimeZoneDatasetValidator
{
    public static void Validate(IReadOnlyCollection<TimeZoneFeature> features, int minRingPoints)
    {
        foreach (TimeZoneFeature feature in features)
        {
            if (string.IsNullOrWhiteSpace(feature.Tzid))
                throw new InvalidOperationException("Timezone feature has an empty tzid.");

            if (feature.MultiPolygon.Count == 0)
                throw new InvalidOperationException($"Timezone feature '{feature.Tzid}' has empty geometry.");

            if (feature.BoundingBox.MinLat > feature.BoundingBox.MaxLat || feature.BoundingBox.MinLon > feature.BoundingBox.MaxLon)
                throw new InvalidOperationException($"Timezone feature '{feature.Tzid}' has an invalid bounding box.");

            foreach (List<List<Coordinate>> polygon in feature.MultiPolygon)
            {
                if (polygon.Count == 0)
                    throw new InvalidOperationException($"Timezone feature '{feature.Tzid}' contains an empty polygon.");

                foreach (List<Coordinate> ring in polygon)
                {
                    if (ring.Count < minRingPoints)
                        throw new InvalidOperationException($"Timezone feature '{feature.Tzid}' contains a ring with fewer than {minRingPoints} points.");

                    if (ring[0] != ring[^1])
                        throw new InvalidOperationException($"Timezone feature '{feature.Tzid}' contains an open ring.");

                    foreach (Coordinate coordinate in ring)
                    {
                        if (coordinate.Latitude is < -90 or > 90)
                            throw new InvalidOperationException($"Timezone feature '{feature.Tzid}' contains invalid latitude {coordinate.Latitude}.");

                        if (coordinate.Longitude is < -180 or > 180)
                            throw new InvalidOperationException($"Timezone feature '{feature.Tzid}' contains invalid longitude {coordinate.Longitude}.");
                    }
                }
            }
        }
    }
}
