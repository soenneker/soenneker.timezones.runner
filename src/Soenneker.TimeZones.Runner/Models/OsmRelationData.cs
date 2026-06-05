namespace Soenneker.TimeZones.Runner.Models;

/// <summary>
/// Represents the osm relation data record.
/// </summary>
/// <param name="Id">The id.</param>
/// <param name="TimeZoneId">The time zone id.</param>
/// <param name="Members">The members.</param>
public sealed record OsmRelationData(long Id, string TimeZoneId, List<OsmRelationMemberData> Members);
