using System.Numerics;

namespace Overload.Domain;

public static class Progression
{
    public static BigInteger Cost(BigInteger level)
    {
        Positive(level);
        return 1000 + 50 * level + 5 * level * level;
    }
    public static BigInteger TotalXp(BigInteger level)
    {
        Positive(level);
        var n = level - 1;
        return 1000 * n + 25 * n * (n + 1) + 5 * n * (n + 1) * (2 * n + 1) / 6;
    }
    public static BigInteger LevelAt(BigInteger xp)
    {
        if (xp < 0) throw new ArgumentOutOfRangeException(nameof(xp));
        BigInteger low = 1, high = 2;
        while (TotalXp(high) <= xp) { low = high; high *= 2; }
        while (high - low > 1)
        {
            var mid = (low + high) / 2;
            if (TotalXp(mid) <= xp) low = mid; else high = mid;
        }
        return low;
    }
    public static BigInteger Resonance(BigInteger level) { Positive(level); return BigInteger.Max(0, level - 60); }
    public static BigInteger ReferenceLevel(BigInteger tier) { Positive(tier); return 60 + 4 * (tier - 1); }
    public static BigInteger TierScaled(BigInteger baseSubunits, BigInteger tier)
    {
        Positive(tier);
        if (baseSubunits < 0) throw new ArgumentOutOfRangeException(nameof(baseSubunits));
        return baseSubunits * (tier + 24) * (tier + 24) / 625;
    }
    private static void Positive(BigInteger value) { if (value < 1) throw new ArgumentOutOfRangeException(nameof(value)); }
}
