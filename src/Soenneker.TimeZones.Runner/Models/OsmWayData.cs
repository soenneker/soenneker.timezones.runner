namespace Soenneker.TimeZones.Runner.Models;

/// <summary>
/// Represents the osm way data record.
/// </summary>
/// <param name="Id">The id.</param>
/// <param name="NodeIds">The node ids.</param>
public sealed record OsmWayData(long Id, long[] NodeIds);
