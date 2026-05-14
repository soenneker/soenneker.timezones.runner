namespace Soenneker.TimeZones.Runner.Geometry;

public sealed record RingStitchResult(List<long[]> Rings, int IncompleteChainsDropped);

public static class RingStitcher
{
    public static RingStitchResult Stitch(IEnumerable<long[]> wayNodeIds, int minRingPoints)
    {
        var chains = wayNodeIds.Where(x => x.Length >= 2).Select(x => x.ToList()).ToList();
        var rings = new List<long[]>();
        var dropped = 0;

        while (chains.Count > 0)
        {
            List<long> chain = chains[0];
            chains.RemoveAt(0);

            bool changed;

            do
            {
                changed = false;

                for (var i = 0; i < chains.Count; i++)
                {
                    List<long> candidate = chains[i];

                    if (TryJoin(chain, candidate))
                    {
                        chains.RemoveAt(i);
                        changed = true;
                        break;
                    }
                }
            } while (changed && !IsClosed(chain));

            if (IsClosed(chain) && chain.Count >= minRingPoints)
                rings.Add(chain.ToArray());
            else
                dropped++;
        }

        return new RingStitchResult(rings, dropped);
    }

    private static bool TryJoin(List<long> chain, List<long> candidate)
    {
        long chainFirst = chain[0];
        long chainLast = chain[^1];
        long candidateFirst = candidate[0];
        long candidateLast = candidate[^1];

        if (chainLast == candidateFirst)
        {
            chain.AddRange(candidate.Skip(1));
            return true;
        }

        if (chainLast == candidateLast)
        {
            candidate.Reverse();
            chain.AddRange(candidate.Skip(1));
            return true;
        }

        if (chainFirst == candidateLast)
        {
            chain.InsertRange(0, candidate.Take(candidate.Count - 1));
            return true;
        }

        if (chainFirst == candidateFirst)
        {
            candidate.Reverse();
            chain.InsertRange(0, candidate.Take(candidate.Count - 1));
            return true;
        }

        return false;
    }

    private static bool IsClosed(List<long> chain) => chain.Count > 2 && chain[0] == chain[^1];
}
