using Clipper2Lib;
using Soenneker.TimeZones.Runner.Models;

namespace Soenneker.TimeZones.Runner.Geometry;

public static class ClipperGeometry
{
    public const double Scale = 10_000_000D;

    public static Paths64 BuildRelationGeometry(IEnumerable<IReadOnlyList<Coordinate>> outerRings, IEnumerable<IReadOnlyList<Coordinate>> innerRings)
    {
        Paths64 outers = ToPaths(outerRings);
        Paths64 inners = ToPaths(innerRings);

        if (outers.Count == 0)
            return [];

        Paths64 unionedOuters = Normalize(outers);

        if (inners.Count == 0)
            return unionedOuters;

        Paths64 unionedInners = Normalize(inners);
        return Normalize(Clipper.Difference(unionedOuters, unionedInners, FillRule.NonZero));
    }

    public static Paths64 Normalize(Paths64 paths)
    {
        if (paths.Count == 0)
            return paths;

        Paths64 unioned = Clipper.Union(paths, FillRule.NonZero);
        return Clipper.SimplifyPaths(unioned, 2.0, true);
    }

    public static Paths64 ToPaths(IEnumerable<IReadOnlyList<Coordinate>> rings)
    {
        var paths = new Paths64();

        foreach (IReadOnlyList<Coordinate> ring in rings)
        {
            var path = new Path64(ring.Count);

            foreach (Coordinate coordinate in ring)
                path.Add(ToPoint(coordinate));

            if (path.Count > 0 && (path[0].X != path[^1].X || path[0].Y != path[^1].Y))
                path.Add(path[0]);

            paths.Add(path);
        }

        return paths;
    }

    public static List<List<List<Coordinate>>> ToMultiPolygon(Paths64 paths, int minRingPoints)
    {
        var outers = new List<Path64>();
        var holes = new List<Path64>();

        foreach (Path64 path in paths.Where(x => x.Count >= minRingPoints))
        {
            if (Clipper.IsPositive(path))
                outers.Add(path);
            else
                holes.Add(path);
        }

        if (outers.Count == 0 && holes.Count > 0)
        {
            outers.AddRange(holes.Select(Clipper.ReversePath));
            holes.Clear();
        }

        var polygons = outers
            .OrderByDescending(static x => Math.Abs(Clipper.Area(x)))
            .Select(static outer => new List<List<Coordinate>> { ToRing(outer) })
            .ToList();

        foreach (Path64 hole in holes.OrderByDescending(static x => Math.Abs(Clipper.Area(x))))
        {
            Coordinate point = ToCoordinate(hole[0]);
            int outerIndex = FindContainingOuter(polygons, point);

            if (outerIndex >= 0)
                polygons[outerIndex].Add(ToRing(hole));
        }

        return polygons;
    }

    private static int FindContainingOuter(List<List<List<Coordinate>>> polygons, Coordinate point)
    {
        for (var i = 0; i < polygons.Count; i++)
        {
            if (ContainsPoint(polygons[i][0], point))
                return i;
        }

        return -1;
    }

    private static bool ContainsPoint(IReadOnlyList<Coordinate> ring, Coordinate point)
    {
        var inside = false;

        for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
        {
            Coordinate pi = ring[i];
            Coordinate pj = ring[j];

            bool intersects = pi.Latitude > point.Latitude != pj.Latitude > point.Latitude &&
                              point.Longitude < (pj.Longitude - pi.Longitude) * (point.Latitude - pi.Latitude) / (pj.Latitude - pi.Latitude) + pi.Longitude;

            if (intersects)
                inside = !inside;
        }

        return inside;
    }

    private static Point64 ToPoint(Coordinate coordinate) => new(
        (long)Math.Round(coordinate.Longitude * Scale, MidpointRounding.AwayFromZero),
        (long)Math.Round(coordinate.Latitude * Scale, MidpointRounding.AwayFromZero));

    private static List<Coordinate> ToRing(Path64 path)
    {
        var ring = path.Select(ToCoordinate).ToList();

        if (ring.Count > 0 && ring[0] != ring[^1])
            ring.Add(ring[0]);

        return ring;
    }

    private static Coordinate ToCoordinate(Point64 point) => new(point.X / Scale, point.Y / Scale);
}
