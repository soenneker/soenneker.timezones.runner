namespace Soenneker.TimeZones.Runner.Models;

/// <summary>
/// Represents the time zone feature record.
/// </summary>
/// <param name="Tzid">The tzid.</param>
/// <param name="MultiPolygon">The multi polygon.</param>
/// <param name="BoundingBox">The bounding box.</param>
public sealed record TimeZoneFeature(string Tzid, List<List<List<Coordinate>>> MultiPolygon, BoundingBox BoundingBox);
