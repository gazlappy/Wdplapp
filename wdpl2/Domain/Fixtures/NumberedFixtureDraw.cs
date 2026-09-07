namespace Wdpl2.Services;

public static class NumberedFixtureDraw
{
    public sealed record Pairing(int Round, int Home, int Away);

    public static List<Pairing> Create(int slotCount, int legs)
    {
        if (slotCount < 2 || slotCount % 2 != 0) throw new ArgumentOutOfRangeException(nameof(slotCount));
        if (legs < 1) throw new ArgumentOutOfRangeException(nameof(legs));
        int rounds = slotCount - 1;
        var left = Enumerable.Range(0, slotCount / 2)
            .Select(i => 2 * i + (i > 0 && i % 2 == 0 ? 1 : 0)).ToList();
        var rotation = left.Concat(left.Select(slot => slot ^ 1).Reverse()).ToList();
        var firstLeg = new List<Pairing>();
        for (int round = 0; round < rounds; round++)
        {
            var opponents = new int[slotCount];
            for (int row = 0; row < slotCount / 2; row++)
            {
                int a = rotation[row], b = rotation[slotCount - 1 - row];
                opponents[a] = b;
                opponents[b] = a;
            }
            // Opponent edges and odd/even partner edges form even cycles.
            // Give adjacent vertices opposite home roles; number 1 anchors the top row.
            var home = new bool?[slotCount];
            for (int root = 0; root < slotCount; root++)
            {
                if (home[root].HasValue) continue;
                home[root] = round % 2 == 0;
                var pending = new Queue<int>();
                pending.Enqueue(root);
                while (pending.TryDequeue(out int slot))
                    foreach (int other in new[] { opponents[slot], slot ^ 1 })
                    {
                        bool expected = !home[slot]!.Value;
                        if (home[other].HasValue)
                        {
                            if (home[other] != expected)
                                throw new InvalidOperationException("The numbered draw contains conflicting home roles.");
                            continue;
                        }
                        home[other] = expected;
                        pending.Enqueue(other);
                    }
            }
            for (int row = 0; row < slotCount / 2; row++)
            {
                int a = rotation[row], b = rotation[slotCount - 1 - row];
                firstLeg.Add(new(round, (home[a]!.Value ? a : b) + 1, (home[a]!.Value ? b : a) + 1));
            }
            int last = rotation[^1];
            rotation.RemoveAt(rotation.Count - 1);
            rotation.Insert(1, last);
        }
        return Enumerable.Range(0, legs).SelectMany(leg => firstLeg.Select(p => new Pairing(
            p.Round + leg * rounds, leg % 2 == 0 ? p.Home : p.Away, leg % 2 == 0 ? p.Away : p.Home))).ToList();
    }

    public static bool AreTablePartners(int first, int second) =>
        first > 0 && second > 0 && ((first - 1) ^ 1) == second - 1;
}
