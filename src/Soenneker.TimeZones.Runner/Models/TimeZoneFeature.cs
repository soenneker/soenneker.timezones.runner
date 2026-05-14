namespace Soenneker.TimeZones.Runner.Models;

public sealed record TimeZoneFeature(string Tzid, List<List<List<Coordinate>>> MultiPolygon, BoundingBox BoundingBox);
