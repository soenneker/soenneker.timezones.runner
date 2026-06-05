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
    /// <param name="tags">The tags.</param>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    /// <returns>A value indicating whether the operation succeeded.</returns>
    public static bool TryGetValue(TagsCollectionBase? tags, string key, out string value)
    {
        value = "";
        return tags is not null && tags.TryGetValue(key, out value!) && !string.IsNullOrWhiteSpace(value);
    }

    /// <summary>
    /// Gets time zone id.
    /// </summary>
    /// <param name="tags">The tags.</param>
    /// <returns>The result of the operation.</returns>
    public static string? GetTimeZoneId(TagsCollectionBase? tags)
    {
        if (TryGetValue(tags, "timezone", out string timezone))
            return timezone.Trim();

        if (TryGetValue(tags, "tzid", out string tzid))
            return tzid.Trim();

        return null;
    }

    /// <summary>
    /// Executes the is timezone relation operation.
    /// </summary>
    /// <param name="tags">The tags.</param>
    /// <param name="includeAdminBoundaries">The include admin boundaries.</param>
    /// <returns>A value indicating whether the operation succeeded.</returns>
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
