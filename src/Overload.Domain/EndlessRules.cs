using System.Collections.Immutable;
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Buffers.Binary;

namespace Overload.Domain;

public enum ResonancePolicy { Balanced, Offense, Defense }
public sealed record ForgeQuote(BigInteger FromGrade, BigInteger ToGrade, BigInteger Gold, BigInteger Alloy);
public sealed record ChapterRoute(string Id, string Region, GearSlot Target);
public sealed record Expedition(Guid Id, BigInteger Sequence, BigInteger Tier, string ContentVersion, string RouteId, ulong Seed,
    BigInteger XpBudget, BigInteger GoldBudget, BigInteger AlloyBudget, ImmutableArray<int> Rooms,
    int NextGroup = 0, ImmutableHashSet<int>? ClaimedGroups = null, Guid? ChainId = null, int ChainLeg = 0)
{
    public bool Completed => NextGroup == 7;
    public ImmutableHashSet<int> Claims => ClaimedGroups ?? [];
    public Region Region { get; init; }
    public ActivityFamily Activity { get; init; }
    public GeneratedLayout? Layout { get; init; }
    public ImmutableArray<string> WorldRules { get; init; } = [];
    public string? Mutation { get; init; }
    public bool Anomaly { get; init; }
    public bool MasteryEarned { get; init; }
    public long ElapsedTicks { get; init; }
    public bool Assisted { get; init; }
    public bool ChainBonusEligible { get; init; } = true;
}

/// <summary>Exact shared formula model for the runtime, forge, reports and acceptance checks.</summary>
public static class EndlessRules
{
    public const string Version = "fracture.v1";
    public static readonly ImmutableArray<ChapterRoute> Routes =
    [new("route.ash", "Ash", GearSlot.Weapon), new("route.glass", "Glass", GearSlot.Chest), new("route.hollow", "Hollow", GearSlot.OffHand), new("route.crown", "Crown", GearSlot.Gloves)];
    public static BigInteger ChapterAt(BigInteger tier) { Positive(tier); return 1 + (tier - 1) / 10; }
    public static BigInteger ExpeditionXp(BigInteger tier) => Progression.Cost(Progression.ReferenceLevel(tier)) / 5;
    public static BigInteger PlayerAmount(BigInteger foundationSubunits, BigInteger resonance, BigInteger grade)
    {
        Positive(grade); if (foundationSubunits < 0 || resonance < 0) throw new ArgumentOutOfRangeException();
        return foundationSubunits * (50 + resonance) * (grade + 24) / 1250;
    }
    public static BigInteger Mitigated(BigInteger damage, int baseDefense, BigInteger grade, BigInteger tier, bool supernatural = false)
    {
        Positive(grade); Positive(tier);
        // The common /25 in scaled defense and K cancels before the final rounding boundary.
        return CombatMath.Mitigate(damage, baseDefense * (grade + 24), 120 * (tier + 24), supernatural ? 60 : 65);
    }
    public static ForgeQuote Quote(BigInteger from, BigInteger to)
    {
        Positive(from); if (to <= from) throw new InvalidOperationException("Choose a higher attunement grade");
        var sum = (to * (to + 1) - from * (from + 1)) / 2;
        return new(from, to, 50 * sum, 25 * sum);
    }
    public static CharacterState Forge(CharacterState s, BigInteger target)
    {
        if (!s.FractureUnlocked || target > s.HighestClearedTier) throw new InvalidOperationException("Clear that tier before buying its attunement grade");
        var q = Quote(s.AttunementGrade, target);
        if (q.Gold > s.Gold || q.Alloy > s.Alloy) throw new InvalidOperationException("Not enough gold or Alloy for this upgrade");
        return s with { AttunementGrade = target, Gold = s.Gold - q.Gold, Alloy = s.Alloy - q.Alloy };
    }
    public static BigInteger AffordableGrade(CharacterState s)
    {
        var low = s.AttunementGrade; var high = BigInteger.Max(low, s.HighestClearedTier) + 1;
        while (high - low > 1)
        {
            var mid = (low + high) / 2; var q = Quote(s.AttunementGrade, mid);
            if (q.Gold <= s.Gold && q.Alloy <= s.Alloy) low = mid; else high = mid;
        }
        return low;
    }
    public static ulong Seed(BigInteger tier, Guid runId, string route, string version)
    {
        Positive(tier);
        return BinaryPrimitives.ReadUInt64LittleEndian(SHA256.HashData(Encoding.UTF8.GetBytes($"{tier.ToString(CultureInfo.InvariantCulture)}|{runId:N}|{route}|{version}")));
    }
    public static CharacterState Begin(CharacterState s, BigInteger tier, Guid id)
    {
        if (!s.FractureUnlocked || tier < 1 || tier > s.HighestUnlockedTier) throw new InvalidOperationException("Choose an unlocked tier");
        if (id == Guid.Empty || s.Fracture is { Completed: false }) throw new InvalidOperationException("Resume or abandon the active expedition first");
        if (!s.ChapterOffers.IsEmpty) throw new InvalidOperationException("Choose your saved chapter route first");
        var seed = Seed(tier, id, s.SelectedRoute, Version);
        // A bounded selection of authored rooms; procedural layout generation belongs to P02.
        var rooms = RoomsFor(seed);
        var sequence = s.ExpeditionSequence + 1;
        return s with { ExpeditionSequence = sequence, Fracture = new(id, sequence, tier, Version, s.SelectedRoute, seed,
            ExpeditionXp(tier), 200 * tier, 100 * tier, rooms, ClaimedGroups: []) };
    }
    private static ulong RoomKey(ulong seed, int room) => BinaryPrimitives.ReadUInt64LittleEndian(SHA256.HashData(Encoding.UTF8.GetBytes($"{seed.ToString(CultureInfo.InvariantCulture)}:{room.ToString(CultureInfo.InvariantCulture)}")));
    public static ImmutableArray<int> RoomsFor(ulong seed) => [.. Enumerable.Range(0, 7).OrderBy(i => RoomKey(seed, i)).Take(6), 7];
    public static BigInteger GroupXp(Expedition run, int group)
    {
        if (group is < 0 or > 6) throw new ArgumentOutOfRangeException(nameof(group));
        var piece = run.XpBudget / 10;
        return group < 6 ? piece : run.XpBudget - piece * 6;
    }
    public static CharacterState Claim(CharacterState s, Guid runId, BigInteger sequence, int group)
    {
        var run = s.Fracture;
        if (run is null || run.Id != runId || run.Sequence != sequence || group is < 0 or > 6) throw new InvalidOperationException("Stale expedition reward");
        if (run.Claims.Contains(group)) return s;
        if (group != run.NextGroup || run.Completed) throw new InvalidOperationException("Complete the active encounter group first");
        var updated = FoundationRules.AddXp(s, GroupXp(run, group)) with { Fracture = run with { NextGroup = group + 1, ClaimedGroups = run.Claims.Add(group) } };
        if (group < 6) return updated;
        var cleared = BigInteger.Max(s.HighestClearedTier, run.Tier);
        var unlocked = BigInteger.Max(s.HighestUnlockedTier, run.Tier + 1);
        var nextChapter = ChapterAt(unlocked);
        var offers = s.ChapterOffers;
        if (nextChapter > s.Chapter && offers.IsEmpty)
            offers = [.. Routes.OrderBy(r => Seed(nextChapter, run.Id, r.Id, Version)).Take(3).Select(r => r.Id)];
        var slot = Routes.Single(r => r.Id == run.RouteId).Target;
        var item = EquipmentRules.StarterItems.Single(i => i.Slot == slot) with { Id = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"{run.Id:N}:loot"))[..16]), Name = "Fracture " + slot, Band = 3, BaseValue = slot == GearSlot.Weapon ? 64 : slot == GearSlot.Chest ? 140 : 40 };
        var full = updated.Inventory.Length >= EquipmentRules.Capacity;
        var result = updated with { HighestClearedTier = cleared, HighestUnlockedTier = unlocked,
            Gold = updated.Gold + run.GoldBudget, Alloy = updated.Alloy + run.AlloyBudget,
            Inventory = full ? updated.Inventory : updated.Inventory.Add(item),
            PendingChapter = offers.IsEmpty ? 0 : nextChapter, ChapterOffers = offers,
            Journal = [.. s.Journal.TakeLast(31), $"Fracture {run.Tier}: {run.XpBudget} XP, {run.GoldBudget} gold, {run.AlloyBudget} Alloy banked. {(full ? "Bag full: targeted item left behind (wallet reward unchanged)." : item.Name + " recovered.")}"] };
        return RegionalContent.IsRegional(run.ContentVersion) ? EndgameRules.Completed(result,run) : result;
    }
    public static CharacterState ChooseRoute(CharacterState s, string route)
    {
        if (!s.ChapterOffers.Contains(route) || s.PendingChapter <= s.Chapter) throw new InvalidOperationException("Choose a saved chapter offer");
        return s with { Chapter = s.PendingChapter, PendingChapter = 0, SelectedRoute = route, ChapterOffers = [], KnownRoutes = s.KnownRoutes.Add(route) };
    }
    public static CharacterState SelectKnownRoute(CharacterState s,string route)
    {
        if(!s.KnownRoutes.Contains(route) || s.Fracture is { Completed:false } || !s.ChapterOffers.IsEmpty)
            throw new InvalidOperationException("Choose a known route between expeditions, after resolving any chapter offer");
        return s with { SelectedRoute=route };
    }
    public static CharacterState Abandon(CharacterState s)
    {
        if (s.Fracture is null) return s;
        return s with { Fracture = null }; // Already saved group XP is retained; no completion payout or frontier change.
    }
    public static void Validate(CharacterState s)
    {
        if (s.ExpeditionSequence < 0 || s.HighestUnlockedTier != s.HighestClearedTier + 1 || s.Chapter < 1
            || s.Chapter > ChapterAt(s.HighestUnlockedTier) || s.ChainLeg != 0 || !Routes.Any(r => r.Id == s.SelectedRoute)
            || s.KnownRoutes is null || !s.KnownRoutes.Contains(s.SelectedRoute) || s.KnownRoutes.Count>Routes.Length || s.KnownRoutes.Any(id=>!Routes.Any(r=>r.Id==id))
            || s.ChapterOffers.IsDefault || s.ChapterOffers.Length is not (0 or 3) || s.ChapterOffers.Distinct().Count() != s.ChapterOffers.Length
            || s.ChapterOffers.Any(r => !Routes.Any(t => t.Id == r)) || s.ChapterOffers.IsEmpty && s.PendingChapter != 0
            || !s.ChapterOffers.IsEmpty && (s.PendingChapter <= s.Chapter || s.PendingChapter != ChapterAt(s.HighestUnlockedTier)))
            throw new InvalidDataException("Invalid frontier or saved chapter routes");
        if (s.Fracture is not { } r) return;
        if (!s.FractureUnlocked || r.Id == Guid.Empty || r.Sequence != s.ExpeditionSequence || r.Sequence < 1 || r.Tier < 1 || r.Tier > s.HighestUnlockedTier
            || r.ContentVersion is not (Version or EndgameRules.Version or RegionalContent.ExpeditionVersion) || !Routes.Any(t => t.Id == r.RouteId)
            || r.Seed != Seed(r.Tier, r.Id, r.ContentVersion==Version?r.RouteId:$"{r.RouteId}|{r.Region}|{r.Activity}|{(r.Anomaly?r.Mutation:null)}", r.ContentVersion)
            || r.XpBudget != ExpeditionXp(r.Tier) || r.GoldBudget != 200 * r.Tier || r.AlloyBudget != 100 * r.Tier
            || r.Rooms.IsDefault || r.Rooms.Length != 7 || r.ContentVersion!=RegionalContent.ExpeditionVersion && (r.Rooms[^1] != 7 || r.Rooms.Take(6).Any(i => i is < 0 or > 6))
            || r.Rooms.Take(6).Distinct().Count() != 6 || r.NextGroup is < 0 or > 7 || r.ClaimedGroups is null
            || r.ContentVersion!=RegionalContent.ExpeditionVersion && !r.Rooms.SequenceEqual(RoomsFor(r.Seed))
            || r.Claims.Count != r.NextGroup || !Enumerable.Range(0, r.NextGroup).All(r.Claims.Contains)
            || r.ElapsedTicks<0 || r.Completed && s.HighestClearedTier < r.Tier)
            throw new InvalidDataException("Invalid active expedition identity, immutable budget or checkpoint");
        if(r.ContentVersion==Version && (r.ChainId is not null || r.ChainLeg!=0 || r.Layout is not null)) throw new InvalidDataException("Invalid legacy expedition");
        if(RegionalContent.IsRegional(r.ContentVersion) && (r.Layout is null || !r.Rooms.SequenceEqual(r.Layout.Rooms) || !ExpeditionGenerator.Same(r.Layout,r.ContentVersion==RegionalContent.ExpeditionVersion?RegionalContent.Generate(r.Seed,r.Region):ExpeditionGenerator.Generate(r.Seed))
            || !Enum.IsDefined(r.Region) || !Enum.IsDefined(r.Activity) || r.WorldRules.IsDefault || !r.WorldRules.SequenceEqual(WorldLaws.ForRoute(r.RouteId,ChapterAt(r.Tier)))
            || r.Mutation is null || !WorldLaws.Mutations.Contains(r.Mutation) || r.ChainId is null && r.ChainLeg!=0
            || r.ChainId is not null && (r.ChainLeg is <1 or >3 || s.Chain is not { } c || c.Id!=r.ChainId || c.Tier!=r.Tier || c.Regions[r.ChainLeg-1]!=r.Region
                || c.CompletedLegs!=(r.Completed?r.ChainLeg:r.ChainLeg-1) || !r.Completed&&c.Closed))) throw new InvalidDataException("Invalid generated activity or chain checkpoint");
    }
    private static void Positive(BigInteger value) { if (value < 1) throw new ArgumentOutOfRangeException(nameof(value)); }
}
