using Clipper2Lib;
using OsmSharp;
using OsmSharp.Streams;
using Soenneker.TimeZones.Runner.Configuration;
using Soenneker.TimeZones.Runner.Geometry;
using Soenneker.TimeZones.Runner.Models;

namespace Soenneker.TimeZones.Runner.Osm;

public sealed class OsmTimeZoneExtractor
{
    public ExtractStats Extract(ExtractDefinition extract, string pbfPath, RunnerOptions options, Dictionary<string, Paths64> globalPaths)
    {
        var stats = new ExtractStats { Name = extract.Name, CachePath = pbfPath };
        var relations = new List<OsmRelationData>();
        var requiredWayIds = new HashSet<long>();

        foreach (OsmGeo geo in Read(pbfPath))
        {
            if (geo is not Relation relation)
                continue;

            stats.RelationsScanned++;

            if (!OsmTagUtil.IsTimezoneRelation(relation.Tags, options.IncludeAdminBoundaries))
                continue;

            string? tzid = OsmTagUtil.GetTimeZoneId(relation.Tags);

            if (tzid is null)
                continue;

            var members = new List<OsmRelationMemberData>();

            foreach (RelationMember member in relation.Members ?? [])
            {
                if (member.Type != OsmGeoType.Way)
                    continue;

                string role = string.IsNullOrWhiteSpace(member.Role) ? "outer" : member.Role.Trim().ToLowerInvariant();

                if (role is not "inner")
                    role = "outer";

                members.Add(new OsmRelationMemberData(member.Id, role));
                requiredWayIds.Add(member.Id);
            }

            if (members.Count == 0)
                continue;

            stats.TimezoneRelationsFound++;
            relations.Add(new OsmRelationData(relation.Id ?? 0, tzid, members));
        }

        var ways = new Dictionary<long, OsmWayData>();

        foreach (OsmGeo geo in Read(pbfPath))
        {
            if (geo is not Way way || way.Id is null || way.Nodes is null || !requiredWayIds.Contains(way.Id.Value))
                continue;

            ways[way.Id.Value] = new OsmWayData(way.Id.Value, way.Nodes);
        }

        stats.WaysLoaded = ways.Count;

        var requiredNodeIds = ways.Values.SelectMany(static x => x.NodeIds).ToHashSet();
        var nodes = new Dictionary<long, Coordinate>();

        foreach (OsmGeo geo in Read(pbfPath))
        {
            if (geo is not Node node || node.Id is null || node.Latitude is null || node.Longitude is null || !requiredNodeIds.Contains(node.Id.Value))
                continue;

            nodes[node.Id.Value] = new Coordinate(node.Longitude.Value, node.Latitude.Value);
        }

        stats.NodesLoaded = nodes.Count;

        foreach (OsmRelationData relation in relations)
        {
            Paths64 relationPaths = BuildRelationPaths(relation, ways, nodes, options.MinRingPoints, stats);

            if (relationPaths.Count == 0)
                continue;

            if (!globalPaths.TryGetValue(relation.TimeZoneId, out Paths64? existing))
            {
                existing = [];
                globalPaths[relation.TimeZoneId] = existing;
            }

            existing.AddRange(relationPaths);
        }

        return stats;
    }

    private static Paths64 BuildRelationPaths(OsmRelationData relation, Dictionary<long, OsmWayData> ways, Dictionary<long, Coordinate> nodes,
        int minRingPoints, ExtractStats stats)
    {
        List<long[]> outerWays = GetWayNodeIds(relation, ways, "outer");
        List<long[]> innerWays = GetWayNodeIds(relation, ways, "inner");

        RingStitchResult outerResult = RingStitcher.Stitch(outerWays, minRingPoints);
        RingStitchResult innerResult = RingStitcher.Stitch(innerWays, minRingPoints);

        stats.IncompleteRingsDropped += outerResult.IncompleteChainsDropped + innerResult.IncompleteChainsDropped;

        List<IReadOnlyList<Coordinate>> outerRings = ConvertRings(outerResult.Rings, nodes, minRingPoints, stats);
        List<IReadOnlyList<Coordinate>> innerRings = ConvertRings(innerResult.Rings, nodes, minRingPoints, stats);

        return ClipperGeometry.BuildRelationGeometry(outerRings, innerRings);
    }

    private static List<long[]> GetWayNodeIds(OsmRelationData relation, Dictionary<long, OsmWayData> ways, string role)
    {
        var result = new List<long[]>();

        foreach (OsmRelationMemberData member in relation.Members)
        {
            if (member.Role != role || !ways.TryGetValue(member.WayId, out OsmWayData? way))
                continue;

            result.Add(way.NodeIds);
        }

        return result;
    }

    private static List<IReadOnlyList<Coordinate>> ConvertRings(List<long[]> rings, Dictionary<long, Coordinate> nodes, int minRingPoints, ExtractStats stats)
    {
        var result = new List<IReadOnlyList<Coordinate>>();

        foreach (long[] ring in rings)
        {
            var coordinates = new List<Coordinate>(ring.Length);
            var complete = true;

            foreach (long nodeId in ring)
            {
                if (!nodes.TryGetValue(nodeId, out Coordinate coordinate))
                {
                    complete = false;
                    break;
                }

                coordinates.Add(coordinate);
            }

            if (!complete || coordinates.Count < minRingPoints)
            {
                stats.IncompleteRingsDropped++;
                continue;
            }

            if (coordinates[0] != coordinates[^1])
                coordinates.Add(coordinates[0]);

            result.Add(coordinates);
        }

        return result;
    }

    private static IEnumerable<OsmGeo> Read(string pbfPath)
    {
        using var stream = File.OpenRead(pbfPath);
        var source = new PBFOsmStreamSource(stream);

        foreach (OsmGeo geo in source)
            yield return geo;
    }
}
