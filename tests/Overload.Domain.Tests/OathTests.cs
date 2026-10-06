using System.Collections.Immutable;
using System.Numerics;
using Overload.Content;
using Overload.Domain;
using Xunit;

namespace Overload.Domain.Tests;
public class OathTests
{
    private static BalanceProfile Profile => ProfileLoader.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "arena.json")));
    private sealed class World : IActionPreflight
    {
        public bool Blocked;
        public ActionWorldSnapshot Capture(ActionIntent intent, SkillDefinition skill, ActorBehaviorProfile behavior) =>
            new(0, new(40, 50), ImmutableDictionary<ActionImplementation, PreflightResult>.Empty.Add(ActionImplementation.Base,
                Blocked ? PreflightResult.Blocked("Door closed") : new(true, "Ready", new(40, 50))));
    }
    private static void Advance(PlayerCombat p, int ticks) { for (var i = 0; i < ticks; i++) p.AdvanceClock(); }
    [Fact]
    public void PlaceLockSwapAndExpiryUseDistinctContracts()
    {
        var p = new PlayerCombat(Profile, new World()); Assert.True(p.TryChangeOaths(["oath.elsewhere"])); p.SetCombatActive(true);
        Assert.True(p.TryStart(SkillId.Traverse, Vector2.UnitX)); Assert.False(p.Evading); Assert.Null(p.Anchor);
        Assert.Equal(TraversePhase.AnchorPlace, p.Action!.Traversal); Advance(p, 9); Assert.NotNull(p.Anchor);
        Assert.False(p.TryStart(SkillId.Traverse, Vector2.UnitX)); Advance(p, 15);
        Assert.True(p.TryStart(SkillId.Traverse, Vector2.UnitX)); Assert.True(p.Evading); Assert.Null(p.Anchor);
        Assert.Equal(150, p.Cooldown(SkillId.Traverse)); Advance(p, 7); Assert.False(p.Evading);
        Advance(p, 143); Assert.True(p.TryStart(SkillId.Traverse, Vector2.UnitX)); Advance(p, 249);
        Assert.Null(p.Anchor); Assert.Equal(30, p.Cooldown(SkillId.Traverse));
    }
    [Fact]
    public void InvalidSwapKeepsAnchorAndPaysNothing()
    {
        var world = new World(); var p = new PlayerCombat(Profile, world); p.TryChangeOaths(["oath.elsewhere"]); p.SetCombatActive(true);
        p.TryStart(SkillId.Traverse, Vector2.UnitX); Advance(p, 24); var anchor = p.Anchor; world.Blocked = true;
        Assert.False(p.TryStart(SkillId.Traverse, Vector2.UnitX)); Assert.Same(anchor, p.Anchor); Assert.Equal(0, p.Cooldown(SkillId.Traverse));
        Assert.Equal(0, p.Strain); Assert.Equal(p.MaximumFocus, p.Focus);
    }
    [Fact]
    public void CompatibilityDeathResetAndSingleOathAreEnforced()
    {
        var p = new PlayerCombat(Profile, new World()); Assert.False(p.TryChangeOaths(["oath.elsewhere", "oath.elsewhere"]));
        p.TryChangeOaths(["oath.elsewhere"]); p.SetCombatActive(true); p.Memories.ObserveLocomotion(new(96, 0), LocomotionKind.Walk);
        var plan = p.Preview(SkillId.Traverse, Vector2.UnitX); Assert.Equal(ActionImplementation.Base, plan.Selection.Implementation);
        Assert.Contains(plan.Selection.Rejections, r => r.Reason.Contains("capabilities")); p.TryCommit(plan); Advance(p, 9);
        p.ReceiveHit(10000000, false); Assert.Null(p.Anchor); p.Reset(); Assert.Null(p.Anchor);
        Assert.True(p.TryChangeOaths([])); Assert.True(p.TryStart(SkillId.Traverse, Vector2.UnitX)); Assert.True(p.Evading);
    }
}
