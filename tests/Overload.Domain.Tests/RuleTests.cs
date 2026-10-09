using System.Numerics;
using System.Text.Json;
using Overload.Content;
using Overload.Domain;
using Xunit;

namespace Overload.Domain.Tests;

public class RuleTests
{
    private static BalanceProfile Profile => ProfileLoader.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "arena.json")));
    private static void Advance(PlayerCombat state, int ticks) { for (var i = 0; i < ticks; i++) state.AdvanceClock(); }

    [Theory]
    [InlineData(59)] [InlineData(60)] [InlineData(61)] [InlineData(99)] [InlineData(100)] [InlineData(101)] [InlineData(999)] [InlineData(1000)] [InlineData(1001)] [InlineData(1000001)]
    public void XpBoundaryNeverLosesRemainder(int level)
    {
        var total = Progression.TotalXp(level);
        Assert.Equal(level, Progression.LevelAt(total));
        Assert.Equal(level - 1, Progression.LevelAt(total - 1));
        Assert.Equal(Progression.Cost(level), Progression.TotalXp(level + 1) - total);
        Assert.Equal(Math.Max(0, level - 60), Progression.Resonance(level));
    }
    [Fact]
    public void HugeValuesRoundTripAndDeriveWithoutPerLevelIteration()
    {
        var level = BigInteger.Pow(10, 50);
        var xp = new Quantity(Progression.TotalXp(level) + 123);
        Assert.Equal(xp, JsonSerializer.Deserialize<Quantity>(JsonSerializer.Serialize(xp)));
        Assert.Equal(level, Progression.LevelAt(xp.Value));
        Assert.Equal(5000, CombatMath.BarBasisPoints(xp.Value, xp.Value * 2));
        Assert.True(Progression.TierScaled(200000, level) > long.MaxValue);
    }
    [Theory]
    [InlineData("123")] [InlineData("\"-1\"")] [InlineData("\"01\"")] [InlineData("\"1e9\"")] [InlineData("\" 1\"")]
    public void UnsafeNumericSaveRepresentationsAreRejected(string json) => Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Quantity>(json));

    [Fact]
    public void RejectedAndCanceledActionsCannotRefundOrDoubleSpend()
    {
        var p = new PlayerCombat(Profile);
        Assert.True(p.TryStart(SkillId.ShieldPulse, Vector2.UnitX));
        var focus = p.Focus;
        Assert.False(p.TryStart(SkillId.ShieldPulse, Vector2.UnitX)); Assert.Equal(focus, p.Focus);
        Assert.True(p.TryStart(SkillId.Traverse, Vector2.UnitY)); Assert.Equal(focus, p.Focus);
        Assert.Equal(SkillId.Traverse, p.Action!.Definition.Id);
        Advance(p, 18); Assert.Null(p.Action);
        Assert.True(p.Cooldown(SkillId.ShieldPulse) > 0);
    }
    [Fact]
    public void BasicRecoveryRequiresItsOpeningFramesBeforeMovementCancels()
    {
        var p = new PlayerCombat(Profile); p.TryStart(SkillId.Cleave, Vector2.UnitX);
        Advance(p, 4);
        Advance(p, 3); Assert.Equal(ActionPhase.Active, p.Action!.Phase(p.Tick));
        Assert.False(p.CancelRecoveryByMovement());
        Advance(p, 10); Assert.False(p.CancelRecoveryByMovement());
        Advance(p, 1); Assert.True(p.CancelRecoveryByMovement()); Assert.Null(p.Action);
    }
    [Fact]
    public void EvasionOnlyRejectsEvadableHitsInsideWindow()
    {
        var p = new PlayerCombat(Profile); p.TryStart(SkillId.Traverse, Vector2.UnitX);
        Assert.False(p.ReceiveHit(10000, true)); Assert.True(p.ReceiveHit(10000, false));
        Advance(p, 7); Assert.True(p.ReceiveHit(10000, true));
        Assert.Equal(185000, p.Life);
    }
    [Fact]
    public void FlaskPaysOnceAndHealsExactAmountAcrossSixtyTicks()
    {
        var p = new PlayerCombat(Profile);
        Assert.False(p.TryStart(SkillId.Flask, Vector2.UnitX)); Assert.Equal(3, p.FlaskCharges);
        p.ReceiveHit(200000, false); var start = p.Life;
        Assert.True(p.TryStart(SkillId.Flask, Vector2.UnitX)); Assert.False(p.TryStart(SkillId.Flask, Vector2.UnitX));
        Advance(p, 59); Assert.True(p.Life < start + 70000);
        Advance(p, 1); Assert.Equal(start + 70000, p.Life); Assert.Equal(2, p.FlaskCharges);
        p.ReceiveHit(BigInteger.Pow(10, 100), false); Assert.True(p.Dead);
        Advance(p, 100); Assert.Equal(BigInteger.Zero, p.Life); Assert.Null(p.Action);
        p.Reset(); Assert.Equal(p.MaximumLife, p.Life); Assert.Equal(3, p.FlaskCharges); Assert.Equal(0, p.Cooldown(SkillId.Flask));
    }
    [Fact]
    public void FocusRegenerationHasNoRoundingDriftAndFreeAttackRemainsUsable()
    {
        var profile = Profile;
        profile = profile with { Hero = profile.Hero with { FocusPerSecond = 0 }, Skills = profile.Skills.Select(s => s.Id == SkillId.ShieldPulse ? s with { FocusCost = 100 } : s).ToArray() };
        var p = new PlayerCombat(profile); p.TryStart(SkillId.ShieldPulse, Vector2.UnitX); Advance(p, 40);
        Assert.Equal(0, p.Focus); Assert.False(p.TryStart(SkillId.ChainLance, Vector2.UnitX)); Assert.True(p.TryStart(SkillId.Cleave, Vector2.UnitX));
        p = new PlayerCombat(Profile); p.TryStart(SkillId.ShieldPulse, Vector2.UnitX); Advance(p, 60);
        Assert.Equal(83000, p.Focus); Advance(p, 600); Assert.Equal(p.MaximumFocus, p.Focus);
    }
    [Fact]
    public void StaggerBreakInterruptsOnceThenHonorsImmunity()
    {
        var enemy = new EnemyCombat(Profile.Enemies.Single(e => e.Role == EnemyRole.Bellkeeper));
        Assert.False(enemy.ReceiveHit(10000, 190, 10)); Assert.True(enemy.ReceiveHit(10000, 15, 11));
        var until = enemy.StunnedUntil; Assert.False(enemy.ReceiveHit(10000, 200, 12)); Assert.Equal(until, enemy.StunnedUntil);
        Assert.True(enemy.ReceiveHit(10000, 200, enemy.ImmuneUntil));
    }
    [Fact]
    public void ContentRejectsDuplicatesAndUnsafeTimelines()
    {
        var profile = Profile;
        Assert.Empty(ProfileLoader.Validate(profile));
        Assert.NotEmpty(ProfileLoader.Validate(profile with { Skills = [profile.Skills[0], .. profile.Skills.Take(4)] }));
        Assert.NotEmpty(ProfileLoader.Validate(profile with { Enemies = profile.Enemies.Select(e => e with { Windup = 0 }).ToArray() }));
        Assert.Throws<InvalidDataException>(() => ProfileLoader.Load("{\"schemaVersion\":1,\"id\":\"balance.arena.v1\"}"));
    }
    [Fact]
    public void DefenseCapsAndRandomizedXpInversesHold()
    {
        Assert.Equal(35000, CombatMath.Mitigate(100000, BigInteger.Pow(10, 80), 120, 65));
        var random = new Random(42);
        for (var i = 0; i < 1000; i++)
        {
            var level = new BigInteger(random.Next(1, 1000000));
            var xp = Progression.TotalXp(level) + random.Next(1000);
            Assert.Equal(level, Progression.LevelAt(xp));
        }
    }
}
