using System.Numerics;
using Overload.Content;
using Overload.Domain;
using Xunit;

namespace Overload.Domain.Tests;
public class EndlessTests
{
    private static BalanceProfile Profile => ProfileLoader.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "arena.json")));
    public static CharacterState Reference(BigInteger tier) => FoundationRules.AddXp(new CharacterState { TotalXp = 0, ValidatedLevel = 1,
        FractureUnlocked = true, HighestClearedTier = tier - 1, HighestUnlockedTier = tier, AttunementGrade = BigInteger.Max(1, tier - 1),
        Chapter = EndlessRules.ChapterAt(tier), Gold = BigInteger.Pow(10, 80), Alloy = BigInteger.Pow(10, 80) }, Progression.TotalXp(Progression.ReferenceLevel(tier)));
    public static CharacterState Complete(CharacterState state)
    {
        var r = state.Fracture!;
        for (var i = r.NextGroup; i < 7; i++) state = EndlessRules.Claim(state, r.Id, r.Sequence, i);
        return state;
    }
    [Theory]
    [InlineData(59,61)] [InlineData(99,101)] [InlineData(999,1001)]
    public void EP01_LevelBoundariesConserveXpAndPoints(int from, int to)
    {
        var s = FoundationRules.AddXp(new CharacterState { TotalXp = 0, ValidatedLevel = 1 }, Progression.TotalXp(from));
        s = FoundationRules.AddXp(s, Progression.TotalXp(to) - s.TotalXp + 123);
        Assert.Equal(to, s.ValidatedLevel); Assert.Equal(Progression.TotalXp(to) + 123, s.TotalXp);
        Assert.Equal(Progression.Resonance(to), s.Might + s.Resolve + FoundationRules.UnspentResonance(s));
        Assert.Equal(Math.Min(20, to / 2), FoundationRules.SkillBudget(s)); Assert.Equal(Math.Min(30, to / 2), FoundationRules.TalentBudget(s));
    }
    [Theory]
    [InlineData(9)] [InlineData(10)] [InlineData(11)] [InlineData(999)] [InlineData(1000)]
    public void EP02_FrontierNeverEndsAndOneRunConservesItsBudget(int tier)
    {
        var s = EndlessRules.Begin(Reference(tier), tier, Guid.NewGuid()); var before = s; var r = s.Fracture!;
        Assert.Equal(r.XpBudget, Enumerable.Range(0,7).Aggregate(BigInteger.Zero, (sum, i) => sum + EndlessRules.GroupXp(r,i)));
        s = Complete(s);
        Assert.Equal(tier, s.HighestClearedTier); Assert.Equal(tier + 1, s.HighestUnlockedTier);
        Assert.Equal(before.TotalXp + r.XpBudget, s.TotalXp); Assert.Equal(before.Gold + 200 * tier, s.Gold); Assert.Equal(before.Alloy + 100 * tier, s.Alloy);
        Assert.Equal(7, s.Fracture!.Claims.Count); CharacterRules.Validate(s);
    }
    [Fact]
    public void EP03_ReplayPaysOnceAndOldRunCannotReturnAfterReceiptCompaction()
    {
        var s = EndlessRules.Begin(Reference(11), 3, Guid.NewGuid()); var old = s.Fracture!; s = Complete(s);
        var earned = s.TotalXp;
        Assert.Equal(s, EndlessRules.Claim(s, old.Id, old.Sequence, 6));
        Assert.Equal(10, s.HighestClearedTier); Assert.Equal(11, s.HighestUnlockedTier);
        for (var i = 0; i < 70; i++) s = Complete(EndlessRules.Begin(s, 1, Guid.NewGuid()));
        Assert.Equal(earned + 70 * EndlessRules.ExpeditionXp(1), s.TotalXp);
        Assert.Throws<InvalidOperationException>(() => EndlessRules.Claim(s, old.Id, old.Sequence, 6));
        Assert.Equal(11, s.HighestUnlockedTier); Assert.True(s.Journal.Length <= 32);
    }
    [Fact]
    public void EP04_OldContentIgnoresPlayerLevelAndNewRunsSnapshotRewards()
    {
        var low = Reference(10); var high = FoundationRules.AddXp(low, Progression.TotalXp(1000000));
        var id = Guid.NewGuid(); low = EndlessRules.Begin(low, 3, id); high = EndlessRules.Begin(high, 3, id);
        Assert.Equal(low.Fracture!.XpBudget, high.Fracture!.XpBudget); Assert.True(low.Fracture.Rooms.SequenceEqual(high.Fracture.Rooms));
        var e = Profile.Enemies[0]; Assert.Equal(new EnemyCombat(e,3).MaximumLife, Progression.TierScaled(CombatMath.Points(e.Life),3));
    }
    [Theory]
    [InlineData(1)] [InlineData(10)] [InlineData(100)] [InlineData(1000)]
    public void EP05_MatchedScalingAndDefenseAgreeAtOneFinalRoundingBoundary(int tier)
    {
        var s = Reference(tier) with { HighestClearedTier = tier, HighestUnlockedTier = tier + 1, AttunementGrade = tier };
        var profile = FoundationRules.Build(Profile, s); var p = new PlayerCombat(profile, character:s, tier:tier);
        Assert.Equal(Progression.TierScaled(CombatMath.Points(profile.Hero.Life), tier), p.MaximumLife);
        foreach (var skill in profile.Skills.Where(s => s.Family == ActionFamily.Assault))
        {
            var expected = Progression.TierScaled(CombatMath.Points(skill.Damage), tier);
            Assert.Equal(expected, p.AttackBudget(skill));
            Assert.Equal(expected * 120 / 100, PatternExecution.Damage(expected, ActionImplementation.Afterstrike).First + PatternExecution.Damage(expected, ActionImplementation.Afterstrike).Repeat);
        }
        var damage = BigInteger.Pow(10, 30);
        Assert.Equal(CombatMath.Mitigate(damage, profile.Hero.Armor,120,65), EndlessRules.Mitigated(damage,profile.Hero.Armor,tier,tier));
        Assert.Equal(profile.Hero.Focus * 1000,p.MaximumFocus);
    }
    [Fact]
    public void EP06_HugeCountersRoundTripAndNeverEnterPhysicsOrPercentageTypes()
    {
        var tier = BigInteger.Pow(10,60); var s = Reference(tier) with { Gold = BigInteger.Pow(10,150), Alloy = BigInteger.Pow(10,150) };
        s = Complete(EndlessRules.Begin(s,tier,Guid.NewGuid()));
        s = EndlessRules.Forge(s,tier); var restored = CharacterStore.Decode(CharacterStore.Encode(s));
        Assert.Equal(s.TotalXp,restored.TotalXp); Assert.Equal(tier,restored.AttunementGrade); Assert.Equal(tier+1,restored.HighestUnlockedTier);
        var enemy = new EnemyCombat(Profile.Enemies[0],tier); Assert.True(enemy.Damage > long.MaxValue);
        Assert.InRange(CombatMath.BarBasisPoints(enemy.Life/3,enemy.MaximumLife),3332,3334);
        Assert.Equal(Profile.Enemies[0].Speed,enemy.Definition.Speed);
    }
    [Fact]
    public void EP07_MillionLevelRewardUsesFiniteFoundationGrants()
    {
        var s = FoundationRules.AddXp(new CharacterState(),Progression.TotalXp(1000017)-Progression.TotalXp(17));
        Assert.Equal(1000017,s.ValidatedLevel); Assert.Equal(20,FoundationRules.SkillBudget(s)); Assert.Equal(30,FoundationRules.TalentBudget(s));
        Assert.Equal(999957,s.Might+s.Resolve); Assert.Empty(s.SkillMilestones);
    }
    [Fact]
    public void EP08_BulkForgeConservesWalletAndCannotBuyUnearnedPower()
    {
        var s = Reference(101) with { AttunementGrade = 1 }; var start = s;
        var bulk = EndlessRules.Forge(s,100);
        for (var i = 2; i <= 100; i++) s = EndlessRules.Forge(s,i);
        Assert.Equal(bulk.Gold,s.Gold); Assert.Equal(bulk.Alloy,s.Alloy); Assert.Equal(100,s.AttunementGrade);
        Assert.Throws<InvalidOperationException>(() => EndlessRules.Forge(s,101)); Assert.Throws<InvalidOperationException>(() => EndlessRules.Forge(s,100));
        var poor = start with { Gold = 500, Alloy = 250 };
        Assert.Equal(4,EndlessRules.AffordableGrade(poor)); Assert.Throws<InvalidOperationException>(() => EndlessRules.Forge(poor,5));
    }
    [Fact]
    public void EP09_ChapterOffersAndChosenRouteRemainStableAcrossReload()
    {
        var s = Complete(EndlessRules.Begin(Reference(10),10,Guid.NewGuid()));
        Assert.Equal(3,s.ChapterOffers.Length); Assert.Equal(2,s.PendingChapter);
        var restored = CharacterStore.Decode(CharacterStore.Encode(s)); Assert.True(s.ChapterOffers.SequenceEqual(restored.ChapterOffers));
        Assert.Throws<InvalidOperationException>(() => EndlessRules.Begin(restored,11,Guid.NewGuid()));
        restored = EndlessRules.ChooseRoute(restored,s.ChapterOffers[1]);
        Assert.Equal(2,restored.Chapter); Assert.Empty(restored.ChapterOffers);
        Assert.Throws<InvalidOperationException>(() => EndlessRules.ChooseRoute(restored,s.ChapterOffers[1]));
        Assert.Equal(restored.SelectedRoute, CharacterStore.Decode(CharacterStore.Encode(restored)).SelectedRoute);
        var oldFrontier=restored.HighestUnlockedTier; restored=EndlessRules.SelectKnownRoute(restored,"route.ash");
        Assert.Equal(oldFrontier,restored.HighestUnlockedTier);Assert.Equal(2,restored.Chapter);
    }
    [Fact]
    public void EP10_AuthoredAssemblyAndEnemyTimingBudgetsStayBounded()
    {
        foreach (var tier in new[] { BigInteger.One, new BigInteger(1000), BigInteger.Pow(10,80) })
        {
            var s = Reference(tier) with { Gold = BigInteger.Pow(10,180), Alloy = BigInteger.Pow(10,180) };
            var run = EndlessRules.Begin(s,tier,Guid.NewGuid()).Fracture!;
            Assert.Equal(7,run.Rooms.Length); Assert.Equal(7,run.Rooms[^1]);
            foreach (var e in Profile.Enemies)
            { var enemy = new EnemyCombat(e,tier); Assert.True(enemy.Definition.Windup >= 24); Assert.Equal(e.StaggerThreshold,enemy.Definition.StaggerThreshold); }
        }
    }
    [Fact]
    public void AutoRatiosManualAllocationAndFreeRespecConserveAllPoints()
    {
        foreach (var policy in Enum.GetValues<ResonancePolicy>())
        {
            var s = FoundationRules.AddXp(new CharacterState { ResonancePolicy = policy },Progression.TotalXp(1001));
            Assert.Equal(Progression.Resonance(s.ValidatedLevel),s.Might+s.Resolve);
            s = FoundationRules.Respec(s); var total = FoundationRules.UnspentResonance(s);
            s = FoundationRules.Allocate(s,true,total); Assert.Equal(0,FoundationRules.UnspentResonance(s));
            Assert.Throws<InvalidOperationException>(() => FoundationRules.Allocate(s,false,1));
        }
    }
    [Fact]
    public void EP11_HighLevelDoesNotBypassProductionQualification()
    {
        var s=FoundationRules.AddXp(new CharacterState { Mode=ProfileMode.Standard },Progression.TotalXp(1000000));
        Assert.False(s.ElsewhereOwned);Assert.False(s.ElsewhereSelected);
        Assert.Throws<InvalidOperationException>(()=>CharacterRules.UnlockSandboxOath(s));
        Assert.Equal(120,s.Seals[0]);
    }
}
