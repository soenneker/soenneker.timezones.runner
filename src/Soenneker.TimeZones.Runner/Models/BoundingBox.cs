namespace Soenneker.TimeZones.Runner.Models;

/// <summary>
/// Represents the bounding box record structure.
/// </summary>
/// <param name="MinLat">The min lat.</param>
/// <param name="MaxLat">The max lat.</param>
/// <param name="MinLon">The min lon.</param>
/// <param name="MaxLon">The max lon.</param>
public readonly record struct BoundingBox(double MinLat, double MaxLat, double MinLon, double MaxLon)
{
    /// <summary>
    /// Executes the from rings operation.
    /// </summary>
    /// <param name="rings">The rings.</param>
    /// <returns>The result of the operation.</returns>
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
