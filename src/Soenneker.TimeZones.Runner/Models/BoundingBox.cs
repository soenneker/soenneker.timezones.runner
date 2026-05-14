namespace Soenneker.TimeZones.Runner.Models;

public readonly record struct BoundingBox(double MinLat, double MaxLat, double MinLon, double MaxLon)
{
    public static BoundingBox FromRings(IEnumerable<IReadOnlyList<Coordinate>> rings)
    {
        double minLat = double.MaxValue;
        double maxLat = double.MinValue;
        double minLon = double.MaxValue;
        double maxLon = double.MinValue;

        foreach (IReadOnlyList<Coordinate> ring in rings)
        {
            foreach (Coordinate coordinate in ring)
            {
                minLat = Math.Min(minLat, coordinate.Latitude);
                maxLat = Math.Max(maxLat, coordinate.Latitude);
                minLon = Math.Min(minLon, coordinate.Longitude);
                maxLon = Math.Max(maxLon, coordinate.Longitude);
            }
        }

        return new BoundingBox(minLat, maxLat, minLon, maxLon);
    }
}
