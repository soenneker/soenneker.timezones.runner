using OsmSharp.Tags;

namespace Soenneker.TimeZones.Runner.Osm;

/// <summary>
/// Represents the osm tag util.
/// </summary>
public static class OsmTagUtil
{
    /// <summary>
    /// Attempts to get value.
    /// </summary>
    /// <param name="tags">OSM tags to inspect.</param>
    /// <param name="key">Key used to locate the target entry.</param>
    /// <param name="value">Receives the matching value when the lookup succeeds.</param>
    /// <returns>true if a matching value was found and assigned to the output parameter; otherwise, false.</returns>
    public static bool TryGetValue(TagsCollectionBase? tags, string key, out string value)
    {
        value = "";
        return tags is not null && tags.TryGetValue(key, out value!) && !string.IsNullOrWhiteSpace(value);
    }

    /// <summary>
    /// Gets time zone id.
    /// </summary>
    /// <param name="tags">OSM tags to inspect.</param>
    /// <returns>The requested text.</returns>
    public static string? GetTimeZoneId(TagsCollectionBase? tags)
    {
        if (TryGetValue(tags, "timezone", out string timezone))
            return timezone.Trim();

        if (TryGetValue(tags, "tzid", out string tzid))
            return tzid.Trim();

        return null;
    }

    /// <summary>
    /// Determines whether the Osm Tag timezone Relation.
    /// </summary>
    /// <param name="tags">OSM tags to inspect.</param>
    /// <param name="includeAdminBoundaries">Whether administrative boundaries with timezone tags should also qualify.</param>
    /// <returns>true if the tags identify a qualifying timezone relation; otherwise, false.</returns>
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
