using Clipper2Lib;
using System.Globalization;
using Microsoft.Extensions.Logging;
using OsmSharp;
using OsmSharp.Streams;
using Soenneker.TimeZones.Runner.Configuration;
using Soenneker.TimeZones.Runner.Geometry;
using Soenneker.TimeZones.Runner.Models;
using Soenneker.Utils.File.Abstract;

namespace Soenneker.TimeZones.Runner.Osm;

public sealed class OsmTimeZoneExtractor
{
    private static readonly TimeSpan _progressInterval = TimeSpan.FromSeconds(30);

    private readonly IFileUtil _fileUtil;
    private readonly ILogger<OsmTimeZoneExtractor> _logger;

    public OsmTimeZoneExtractor(IFileUtil fileUtil, ILogger<OsmTimeZoneExtractor> logger)
    {
        _fileUtil = fileUtil;
        _logger = logger;
    }

    public ExtractStats Extract(ExtractDefinition extract, string pbfPath, RunnerOptions options, Dictionary<string, Paths64> globalPaths)
    {
        var stats = new ExtractStats { Name = extract.Name, CachePath = pbfPath };
        var relations = new List<OsmRelationData>();
        var requiredWayIds = new HashSet<long>();
        long totalRead = 0;

        _logger.LogInformation("Starting OSM pass 1/3 for {ExtractName}: scanning relations in {PbfPath}", extract.Name, pbfPath);

        foreach (OsmGeo geo in Read(pbfPath, extract.Name, "1/3 relation scan"))
        {
            totalRead++;

            if (geo is not Relation relation)
                continue;

            stats.RelationsScanned++;

            if (!OsmTagUtil.IsTimezoneRelation(relation.Tags, options.IncludeAdminBoundaries))
                continue;

            string? tzid = OsmTagUtil.GetTimeZoneId(relation.Tags);

            if (tzid is null)
                continue;

            OsmRelationData? relationData = ToRelationData(relation);

            if (relationData is null)
                continue;

            stats.TimezoneRelationsFound++;

            foreach (OsmRelationMemberData member in relationData.Members)
                requiredWayIds.Add(member.WayId);

            relations.Add(relationData);
        }

        _logger.LogInformation(
            "Completed OSM pass 1/3 for {ExtractName}: read {TotalRead:n0} objects, scanned {RelationsScanned:n0} relations, found {TimezoneRelationsFound:n0} timezone relations, required ways {RequiredWayCount:n0}",
            extract.Name, totalRead, stats.RelationsScanned, stats.TimezoneRelationsFound, requiredWayIds.Count);

        var ways = new Dictionary<long, OsmWayData>();
        totalRead = 0;

        _logger.LogInformation("Starting OSM pass 2/3 for {ExtractName}: loading {RequiredWayCount:n0} required ways", extract.Name, requiredWayIds.Count);

        foreach (OsmGeo geo in Read(pbfPath, extract.Name, "2/3 way load"))
        {
            totalRead++;

            if (geo is not Way way || way.Id is null || way.Nodes is null || !requiredWayIds.Contains(way.Id.Value))
                continue;

            ways[way.Id.Value] = new OsmWayData(way.Id.Value, way.Nodes);
        }

        stats.WaysLoaded = ways.Count;
        _logger.LogInformation("Completed OSM pass 2/3 for {ExtractName}: read {TotalRead:n0} objects, loaded {WaysLoaded:n0}/{RequiredWayCount:n0} required ways",
            extract.Name, totalRead, stats.WaysLoaded, requiredWayIds.Count);

        var requiredNodeIds = ways.Values.SelectMany(static x => x.NodeIds).ToHashSet();
        var nodes = new Dictionary<long, Coordinate>();
        totalRead = 0;

        _logger.LogInformation("Starting OSM pass 3/3 for {ExtractName}: loading {RequiredNodeCount:n0} required nodes", extract.Name, requiredNodeIds.Count);

        foreach (OsmGeo geo in Read(pbfPath, extract.Name, "3/3 node load"))
        {
            totalRead++;

            if (geo is not Node node || node.Id is null || node.Latitude is null || node.Longitude is null || !requiredNodeIds.Contains(node.Id.Value))
                continue;

            nodes[node.Id.Value] = new Coordinate(node.Longitude.Value, node.Latitude.Value);
        }

        stats.NodesLoaded = nodes.Count;
        _logger.LogInformation("Completed OSM pass 3/3 for {ExtractName}: read {TotalRead:n0} objects, loaded {NodesLoaded:n0}/{RequiredNodeCount:n0} required nodes",
            extract.Name, totalRead, stats.NodesLoaded, requiredNodeIds.Count);

        AssembleGeometry(extract, options, globalPaths, stats, relations, ways, nodes);

        return stats;
    }

    public ExtractStats ExtractComplete(ExtractDefinition extract, string pbfPath, RunnerOptions options, Dictionary<string, Paths64> globalPaths)
    {
        var stats = new ExtractStats { Name = extract.Name, CachePath = pbfPath };
        var relations = new List<OsmRelationData>();
        var ways = new Dictionary<long, OsmWayData>();
        var nodes = new Dictionary<long, Coordinate>();
        long totalRead = 0;

        _logger.LogInformation("Starting OSM single pass for {ExtractName}: loading complete filtered extract {PbfPath}", extract.Name, pbfPath);

        foreach (OsmGeo geo in Read(pbfPath, extract.Name, "1/1 filtered closure load"))
        {
            totalRead++;

            switch (geo)
            {
                case Node node when node.Id is not null && node.Latitude is not null && node.Longitude is not null:
                    long nodeId = node.Id.Value;
                    nodes[nodeId] = new Coordinate(node.Longitude.Value, node.Latitude.Value);
                    break;
                case Way way when way.Id is not null && way.Nodes is not null:
                    long wayId = way.Id.Value;
                    ways[wayId] = new OsmWayData(wayId, way.Nodes);
                    break;
                case Relation relation:
                {
                    stats.RelationsScanned++;

                    if (!OsmTagUtil.IsTimezoneRelation(relation.Tags, options.IncludeAdminBoundaries))
                        break;

                    OsmRelationData? relationData = ToRelationData(relation);

                    if (relationData is null)
                        break;

                    stats.TimezoneRelationsFound++;
                    relations.Add(relationData);
                    break;
                }
            }
        }

        stats.WaysLoaded = ways.Count;
        stats.NodesLoaded = nodes.Count;

        _logger.LogInformation(
            "Completed OSM single pass for {ExtractName}: read {TotalRead:n0} objects, scanned {RelationsScanned:n0} relations, found {TimezoneRelationsFound:n0} timezone relations, loaded {WaysLoaded:n0} ways and {NodesLoaded:n0} nodes",
            extract.Name, totalRead, stats.RelationsScanned, stats.TimezoneRelationsFound, stats.WaysLoaded, stats.NodesLoaded);

        AssembleGeometry(extract, options, globalPaths, stats, relations, ways, nodes);

        return stats;
    }

    private void AssembleGeometry(ExtractDefinition extract, RunnerOptions options, Dictionary<string, Paths64> globalPaths, ExtractStats stats,
        List<OsmRelationData> relations, Dictionary<long, OsmWayData> ways, Dictionary<long, Coordinate> nodes)
    {
        _logger.LogInformation("Starting geometry assembly for {ExtractName}: {RelationCount:n0} timezone relations", extract.Name, relations.Count);
        var relationIndex = 0;

        foreach (OsmRelationData relation in relations)
        {
            relationIndex++;

            if (relationIndex == 1 || relationIndex % 100 == 0 || relationIndex == relations.Count)
            {
                _logger.LogInformation(
                    "Geometry assembly for {ExtractName}: processing relation {RelationIndex:n0}/{RelationCount:n0}, current global tzids {GlobalTimeZoneCount:n0}, incomplete rings dropped {IncompleteRingsDropped:n0}",
                    extract.Name, relationIndex, relations.Count, globalPaths.Count, stats.IncompleteRingsDropped);
            }

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

        _logger.LogInformation(
            "Completed geometry assembly for {ExtractName}: processed {RelationCount:n0} relations, global tzids {GlobalTimeZoneCount:n0}, incomplete rings dropped {IncompleteRingsDropped:n0}",
            extract.Name, relations.Count, globalPaths.Count, stats.IncompleteRingsDropped);
    }

    private static OsmRelationData? ToRelationData(Relation relation)
    {
        string? tzid = OsmTagUtil.GetTimeZoneId(relation.Tags);

        if (tzid is null)
            return null;

        var members = new List<OsmRelationMemberData>();

        foreach (RelationMember member in relation.Members ?? [])
        {
            if (member.Type != OsmGeoType.Way)
                continue;

            string role = string.IsNullOrWhiteSpace(member.Role) ? "outer" : member.Role.Trim().ToLowerInvariant();

            if (role is not "inner")
                role = "outer";

            members.Add(new OsmRelationMemberData(member.Id, role));
        }

        return members.Count == 0 ? null : new OsmRelationData(relation.Id ?? 0, tzid, members);
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

    private IEnumerable<OsmGeo> Read(string pbfPath, string extractName, string passName)
    {
        _logger.LogInformation("Opening OSM PBF stream for {ExtractName}, pass {PassName}: {PbfPath}", extractName, passName, pbfPath);
        using FileStream stream = _fileUtil.OpenRead(pbfPath);
        long streamLength = stream.Length;
        _logger.LogInformation("Opened OSM PBF stream for {ExtractName}, pass {PassName}. Size: {PbfSize:n0} bytes. Creating OsmSharp PBF source...",
            extractName, passName, streamLength);

        var source = new PBFOsmStreamSource(stream);
        _logger.LogInformation("OsmSharp PBF source created for {ExtractName}, pass {PassName}. Starting object decode...", extractName, passName);

        var yielded = 0L;
        long lastYielded = 0;
        DateTimeOffset lastProgress = DateTimeOffset.UtcNow;

        foreach (OsmGeo geo in source)
        {
            yielded++;

            if (yielded == 1)
            {
                _logger.LogInformation("First OSM object decoded for {ExtractName}, pass {PassName}. PBF read progress: {PbfProgress}",
                    extractName, passName, FormatProgress(stream.Position, streamLength));
                lastProgress = DateTimeOffset.UtcNow;
                lastYielded = yielded;
            }
            else
            {
                DateTimeOffset now = DateTimeOffset.UtcNow;

                if (now - lastProgress >= _progressInterval)
                {
                    long decodedSinceLastLog = yielded - lastYielded;

                    _logger.LogInformation(
                        "Decoded {DecodedCount:n0} OSM objects for {ExtractName}, pass {PassName}. PBF read progress: {PbfProgress}. Decode rate since last log: {DecodeRate:n0} objects/sec",
                        yielded, extractName, passName, FormatProgress(stream.Position, streamLength),
                        decodedSinceLastLog / Math.Max(1D, (now - lastProgress).TotalSeconds));

                    lastProgress = now;
                    lastYielded = yielded;
                }
            }

            yield return geo;
        }

        _logger.LogInformation("Finished object decode for {ExtractName}, pass {PassName}. Decoded {DecodedCount:n0} objects. PBF read progress: {PbfProgress}",
            extractName, passName, yielded, FormatProgress(stream.Position, streamLength));
    }

    private static string FormatProgress(long position, long length)
    {
        if (length <= 0)
            return "unknown";

        double percent = Math.Clamp(position / (double)length * 100D, 0D, 100D);
        return string.Create(CultureInfo.InvariantCulture, $"{percent:0.00}% ({position:n0}/{length:n0} bytes)");
    }
}
