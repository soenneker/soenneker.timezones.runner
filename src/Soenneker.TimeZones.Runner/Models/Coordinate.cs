namespace Soenneker.TimeZones.Runner.Models;

/// <summary>
/// Represents the coordinate record structure.
/// </summary>
/// <param name="Longitude">The longitude.</param>
/// <param name="Latitude">The latitude.</param>
public readonly record struct Coordinate(double Longitude, double Latitude);
