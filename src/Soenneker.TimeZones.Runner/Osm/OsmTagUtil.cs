using OsmSharp.Tags;

namespace Soenneker.TimeZones.Runner.Osm;

public static class OsmTagUtil
{
    public static bool TryGetValue(TagsCollectionBase? tags, string key, out string value)
    {
        value = "";
        return tags is not null && tags.TryGetValue(key, out value!) && !string.IsNullOrWhiteSpace(value);
    }

    public static string? GetTimeZoneId(TagsCollectionBase? tags)
    {
        if (TryGetValue(tags, "timezone", out string timezone))
            return timezone.Trim();

        if (TryGetValue(tags, "tzid", out string tzid))
            return tzid.Trim();

        return null;
    }

    public static bool IsTimezoneRelation(TagsCollectionBase? tags, bool includeAdminBoundaries)
    {
        if (tags is null)
            return false;

        bool hasTimezoneId = GetTimeZoneId(tags) is not null;

        if (!hasTimezoneId)
            return false;

        if (TryGetValue(tags, "boundary", out string boundary))
        {
            if (string.Equals(boundary, "timezone", StringComparison.OrdinalIgnoreCase))
                return true;

            return includeAdminBoundaries && string.Equals(boundary, "administrative", StringComparison.OrdinalIgnoreCase) &&
                   TryGetValue(tags, "timezone", out _);
        }

        return tags.ContainsKey("timezone") || tags.ContainsKey("tzid");
    }
}
