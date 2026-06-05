namespace Soenneker.TimeZones.Runner.Models;

/// <summary>
/// Represents the osm relation member data record.
/// </summary>
/// <param name="WayId">The way id.</param>
/// <param name="Role">The role.</param>
public sealed record OsmRelationMemberData(long WayId, string Role);
