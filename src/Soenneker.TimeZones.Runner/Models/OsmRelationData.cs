namespace Soenneker.TimeZones.Runner.Models;

public sealed record OsmRelationData(long Id, string TimeZoneId, List<OsmRelationMemberData> Members);
