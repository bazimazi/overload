using System.Collections.Immutable;
using System.Numerics;
using Overload.Content;
using Overload.Domain;
using Xunit;

namespace Overload.Domain.Tests;
public class SlicePatternTests
{
    private static BalanceProfile Profile => ProfileLoader.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "arena.json")));
    private sealed class World : IActionPreflight
    {
        public ActionWorldSnapshot Capture(ActionIntent intent, SkillDefinition skill, ActorBehaviorProfile behavior) =>
            new(0, Vector2.Zero, Enum.GetValues<ActionImplementation>().ToImmutableDictionary(i => i, _ => new PreflightResult(true, "Ready", Vector2.Zero)));
    }
    [Fact]
    public void ReprieveCanOverhealAndAbsorbsMitigatedDamageWithoutRefillingItsBarrier()
    {
        var p = new PlayerCombat(Profile, new World()); p.TryChangeBindings([new("pattern.recover.reprieve", 0)]); p.SetCombatActive(true);
        p.Memories.ObserveEvadedHit(new(10, 1000, Vector2.UnitX), SourceKind.BasePlayerAction);
        Assert.True(p.TryStart(SkillId.Flask, Vector2.UnitX)); Assert.Equal(2, p.FlaskCharges);
        Assert.Equal(30000, p.Barrier); Assert.Equal(25000, p.Strain); Assert.Null(p.Memories.Echo);
        Assert.False(p.TryStart(SkillId.Flask, Vector2.UnitX)); Assert.Equal(30000, p.Barrier);
        p.ReceiveHit(20000, false); Assert.Equal(p.MaximumLife, p.Life); Assert.Equal(15000, p.Barrier);
        for (var i = 0; i < 180; i++) p.AdvanceClock();
        Assert.Equal(0, p.Barrier); Assert.Equal(p.MaximumLife, p.Life);
    }
    [Fact]
    public void TwoMemoryConvergenceHasOnePaymentAndAnExplicitTellForEveryAssault()
    {
        foreach (var skill in Profile.Skills.Where(s => s.Family == ActionFamily.Assault))
        {
            var p = new PlayerCombat(Profile, new World()); p.TryChangeBindings([new("pattern.assault.convergence", 0)]); p.SetCombatActive(true);
            p.Memories.ObserveLocomotion(new(96, 0), LocomotionKind.Walk);
            p.Memories.ObserveEvadedHit(new(10, 1000, Vector2.UnitX), SourceKind.BasePlayerAction);
            Assert.True(p.TryStart(skill.Id, Vector2.UnitX)); Assert.Equal(15, p.Action!.Definition.Windup);
            Assert.Equal(ActionImplementation.Convergence, p.Action.Implementation); Assert.Equal(45000, p.Strain);
            Assert.Equal(p.MaximumFocus - skill.FocusCost * 1000, p.Focus); Assert.Null(p.Memories.Momentum); Assert.Null(p.Memories.Echo);
        }
    }
    [Fact]
    public void ShelterWrapsElsewhereAndDoesNotRestoreBasePlacementEvasion()
    {
        var p = new PlayerCombat(Profile, new World()); p.TryChangeBindings([new("pattern.traverse.shelter", 0)]);
        p.TryChangeOaths(["oath.elsewhere"]); p.SetCombatActive(true);
        p.Memories.ObserveEvadedHit(new(10, 1000, Vector2.UnitX), SourceKind.BasePlayerAction);
        Assert.True(p.TryStart(SkillId.Traverse, Vector2.UnitX)); Assert.False(p.Evading);
        Assert.Equal(ActionImplementation.Shelter, p.Action!.Implementation); Assert.Equal(TraversePhase.AnchorPlace, p.Action.Traversal);
        for (var i = 0; i < 24; i++) p.AdvanceClock();
        Assert.NotNull(p.Anchor); Assert.True(p.TryStart(SkillId.Traverse, Vector2.UnitX)); Assert.True(p.Evading);
    }
}
