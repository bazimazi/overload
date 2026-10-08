using System.Collections.Immutable;
using System.Numerics;

namespace Overload.Domain;

public enum ProfileMode { Standard, SliceSandbox }
public sealed record CharacterState
{
    public int SchemaVersion { get; init; } = 6;
    public WorldProgress? World { get; init; }
    public FrameId Frame { get; init; }
    public bool RegionalCampaign { get; init; }
    public bool RedCovenantOwned { get; init; }
    public bool RedCovenantSelected { get; init; }
    public bool CovenantMastery { get; init; }
    public string ContentVersion { get; init; } = "slice.v1";
    public Guid CharacterId { get; init; } = Guid.NewGuid();
    public long Revision { get; init; }
    public ProfileMode Mode { get; init; } = ProfileMode.SliceSandbox;
    public BigInteger TotalXp { get; init; } = Progression.TotalXp(17);
    public BigInteger ValidatedLevel { get; init; } = 17;
    public BigInteger Gold { get; init; } = 500;
    public BigInteger Alloy { get; init; } = 100;
    public ImmutableArray<BigInteger> Seals { get; init; } = [120, 120, 120, 120];
    public bool ElsewhereOwned { get; init; }
    public bool ElsewhereSelected { get; init; }
    public ImmutableArray<BindingDefinition> Bindings { get; init; } = [new("pattern.assault.pursuit", 0), new("pattern.assault.afterstrike", 1), new("pattern.traverse.crossing", 2)];
    public Guid RunId { get; init; }
    public int CheckpointRoom { get; init; }
    public ImmutableHashSet<int> ClaimedRooms { get; init; } = [];
    public ImmutableArray<string> RecentTransactions { get; init; } = [];
    public ImmutableArray<string> Journal { get; init; } = [];
    public BigInteger Might { get; init; }
    public BigInteger Resolve { get; init; }
    public bool AutoResonance { get; init; } = true;
    public ResonancePolicy ResonancePolicy { get; init; } = ResonancePolicy.Balanced;
    public bool FractureUnlocked { get; init; }
    public BigInteger ExpeditionSequence { get; init; }
    public Expedition? Fracture { get; init; }
    public string SelectedRoute { get; init; } = "route.ash";
    public ImmutableHashSet<string> KnownRoutes { get; init; } = ["route.ash"];
    public BigInteger PendingChapter { get; init; }
    public BigInteger AttunementGrade { get; init; } = 1;
    public BigInteger HighestClearedTier { get; init; }
    public BigInteger HighestUnlockedTier { get; init; } = 1;
    public BigInteger Chapter { get; init; } = 1;
    public ImmutableArray<string> ChapterOffers { get; init; } = [];
    public int ChainLeg { get; init; }
    public ImmutableHashSet<string> Proofs { get; init; } = [];
    public ImmutableHashSet<Region> Masteries { get; init; } = [];
    public ImmutableHashSet<Region> BreadthBonuses { get; init; } = [];
    public bool TrialCompleted { get; init; }
    public ExpeditionChain? Chain { get; init; }
    public ImmutableArray<BuildRecord> RunRecords { get; init; } = [];
    public ImmutableHashSet<string> SovereignRewards { get; init; } = [];
    public ulong ProgressionSeed { get; init; } = 42;
    public ImmutableArray<GearItem> Inventory { get; init; } = EquipmentRules.StarterItems;
    public ImmutableDictionary<GearSlot, Guid> Equipment { get; init; } = EquipmentRules.StarterItems.ToImmutableDictionary(i => i.Slot, i => i.Id);
    public ImmutableDictionary<SkillId, int> SkillRanks { get; init; } = ImmutableDictionary<SkillId, int>.Empty;
    public ImmutableDictionary<SkillId, Technique> Techniques { get; init; } = ImmutableDictionary<SkillId, Technique>.Empty;
    public ImmutableHashSet<string> Talents { get; init; } = [];
    public ImmutableHashSet<string> Inscriptions { get; init; } = [];
    public ImmutableHashSet<int> SkillMilestones { get; init; } = [];
    public ImmutableArray<SkillId> EquippedSkills { get; init; } = [SkillId.ShieldPulse, SkillId.ChainLance, SkillId.Faultline];
}

public static class CharacterRules
{
    public static void Validate(CharacterState s)
    {
        EquipmentRules.Validate(s);
        FoundationRules.Validate(s);
        if (s.EquippedSkills.IsDefault || s.EquippedSkills.Length != 3 || s.EquippedSkills.Distinct().Count() != 3
            || s.EquippedSkills.Any(id => !FrameRules.Skills(s.Frame).Contains(id) || id == FrameRules.Basic(s.Frame)))
            throw new InvalidDataException("Choose three distinct active skills from your Frame");
        if (s.SchemaVersion != 6 || !Enum.IsDefined(s.Frame) || s.RedCovenantSelected && (!s.RedCovenantOwned || s.ElsewhereSelected)
            || s.ContentVersion != "slice.v1" || s.CharacterId == Guid.Empty || s.Revision < 0 || !Enum.IsDefined(s.Mode) || !Enum.IsDefined(s.ResonancePolicy) || s.TotalXp < 0
            || s.ValidatedLevel != Progression.LevelAt(s.TotalXp) || s.Gold < 0 || s.Alloy < 0 || s.Seals.IsDefault || s.Seals.Length != 4 || s.Seals.Any(v => v < 0)
            || s.ElsewhereSelected && !s.ElsewhereOwned || s.Bindings.IsDefault || s.Bindings.Length > EndgameRules.BindingSlots(s) || s.Bindings.Any(b => b is null) || s.Bindings.Select(b => b.PatternId).Distinct().Count() != s.Bindings.Length
            || s.Bindings.Any(b => b.Priority < 0 || !KnownPatterns.Contains(b.PatternId) || !EndgameRules.PatternAvailable(s,b.PatternId)) || s.CheckpointRoom < 0 || s.CheckpointRoom > JourneyRules.Length(s) || s.ClaimedRooms is null || s.ClaimedRooms.Any(r => r < 0 || r >= JourneyRules.Length(s))
            || s.RecentTransactions.IsDefault || s.RecentTransactions.Length > 64 || s.Journal.IsDefault
            || s.Might < 0 || s.Resolve < 0 || s.Might + s.Resolve > Progression.Resonance(s.ValidatedLevel)
            || s.AttunementGrade < 1 || s.AttunementGrade > BigInteger.Max(1, s.HighestClearedTier) || s.HighestClearedTier < 0 || s.HighestUnlockedTier < 1)
            throw new InvalidDataException("Invalid character balances, references or progression");
        if (s.ClaimedRooms.Count != s.CheckpointRoom || !Enumerable.Range(0, s.CheckpointRoom).All(s.ClaimedRooms.Contains)
            || s.RunId == Guid.Empty && s.CheckpointRoom != 0) throw new InvalidDataException("Inconsistent journey checkpoint");
        if(s.World is {} world) WorldRules.Validate(world);
        EndgameRules.Validate(s);
        EndlessRules.Validate(s);
    }
    public static readonly ImmutableHashSet<string> KnownPatterns = ["pattern.assault.pursuit", "pattern.assault.afterstrike", "pattern.traverse.crossing", "pattern.assault.convergence", "pattern.traverse.shelter", "pattern.recover.reprieve", "pattern.assault.focused", "pattern.assault.shatter", "pattern.assault.cascade"];
    public static CharacterState UnlockSandboxOath(CharacterState s)
    {
        if (s.Mode != ProfileMode.SliceSandbox) throw new InvalidOperationException("Sandbox ritual cannot qualify a standard character");
        if (s.ElsewhereOwned) throw new InvalidOperationException("Oath already owned");
        if (s.Seals.Any(v => v < 120)) throw new InvalidOperationException("Need 120 of each regional Seal");
        return s with { ElsewhereOwned = true, Seals = [.. s.Seals.Select(v => v - 120)] };
    }
}
