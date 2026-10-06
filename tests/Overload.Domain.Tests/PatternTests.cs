using System.Numerics;
using Overload.Content;
using Overload.Domain;
using Xunit;

namespace Overload.Domain.Tests;

public class PatternTests
{
    private static BalanceProfile Profile => ProfileLoader.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "arena.json")));
    [Theory]
    [InlineData(ActionImplementation.Base, 100, 0)]
    [InlineData(ActionImplementation.Pursuit, 110, 0)]
    [InlineData(ActionImplementation.Afterstrike, 60, 60)]
    [InlineData(ActionImplementation.Convergence, 135, 0)]
    public void AuthoredBudgetsApplyOnce(ActionImplementation implementation, int first, int repeat)
    {
        var result = PatternExecution.Damage(100000, implementation);
        Assert.Equal(first * 1000, result.First); Assert.Equal(repeat * 1000, result.Repeat);
    }
    [Fact]
    public void AfterstrikeConservesItsTotalBudgetWithHugeAndIndivisibleValues()
    {
        foreach (var budget in new[] { BigInteger.One, new BigInteger(1001), BigInteger.Pow(10, 80) + 7 })
        {
            var parts = PatternExecution.Damage(budget, ActionImplementation.Afterstrike);
            Assert.Equal(budget * 120 / 100, parts.First + parts.Repeat);
            Assert.True(BigInteger.Abs(parts.First - parts.Repeat) <= 1);
        }
    }
    [Theory]
    [InlineData(SkillId.Cleave)] [InlineData(SkillId.ShieldPulse)] [InlineData(SkillId.ChainLance)]
    public void AdaptersDeclareGeometryHitLimitsAndScaledStagger(SkillId id)
    {
        var skill = Profile.Skills.Single(s => s.Id == id);
        var pursuit = PatternExecution.Recipe(skill, ActionImplementation.Pursuit);
        Assert.Equal(AttackGeometry.Lane, pursuit.Geometry); Assert.Equal(8, pursuit.HalfWidth); Assert.Equal(0, pursuit.Push);
        Assert.Equal(id == SkillId.ChainLance ? 3 : 64, pursuit.MaxVictims);
        var afterstrike = PatternExecution.Recipe(skill, ActionImplementation.Afterstrike);
        Assert.Equal(id == SkillId.ChainLance ? AttackGeometry.Projectile : AttackGeometry.Sector, afterstrike.Geometry);
        Assert.Equal(skill.Stagger * 60 / 100, afterstrike.Stagger); Assert.Equal(skill.Reach, afterstrike.Reach);
    }
    [Fact]
    public void UnauthoredAdaptersCannotSilentlyUseBaseDamage()
    {
        var cleave = Profile.Skills.Single(s => s.Id == SkillId.Cleave);
        Assert.True(PatternExecution.Supports(cleave, ActionImplementation.Convergence));
        Assert.False(PatternExecution.Supports(cleave, ActionImplementation.Crossing));
        Assert.Equal(AttackGeometry.Sector, PatternExecution.Recipe(cleave, ActionImplementation.Convergence).Geometry);
        Assert.Throws<ArgumentException>(() => PatternExecution.Damage(100, ActionImplementation.Crossing));
    }
    [Fact]
    public void ChildEffectsRetainRootSourceAndEnforceCountAndDepth()
    {
        var root = new EffectRoot(42, SourceKind.OverloadedPlayerAction); var parent = root.Primary;
        for (var i = 1; i <= 4; i++)
        {
            Assert.True(root.TryChild(parent, out var child)); Assert.Equal(parent.EffectId, child.ParentEffectId);
            Assert.Equal(42, child.RootActionId); Assert.Equal(SourceKind.OverloadedPlayerAction, child.Source); Assert.Equal(i, child.Depth); parent = child;
        }
        Assert.False(root.TryChild(parent, out _));
        for (var i = 4; i < 64; i++) Assert.True(root.TryChild(root.Primary, out _));
        Assert.False(root.TryChild(root.Primary, out _));
    }
    [Fact]
    public void ForeignOrReclassifiedParentsCannotSpawnChildren()
    {
        var root = new EffectRoot(42, SourceKind.OverloadedPlayerAction);
        Assert.False(root.TryChild(root.Primary with { Source = SourceKind.BasePlayerAction }, out _));
        Assert.False(root.TryChild(root.Primary with { RootActionId = 43 }, out _));
        Assert.False(root.TryChild(new(42, 999, 0, SourceKind.OverloadedPlayerAction, 1), out _));
    }
    [Fact]
    public void TimelineIsBoundedStableAndNeverDrainsNewWorkRecursively()
    {
        var queue = new EffectTimeline<string>(3);
        Assert.True(queue.TryEnqueue(21, "first")); Assert.True(queue.TryEnqueue(21, "second")); Assert.True(queue.TryEnqueue(20, "earlier"));
        Assert.False(queue.TryEnqueue(1, "overflow")); Assert.Empty(queue.TakeDue(19));
        Assert.Equal(new[] { "earlier" }, queue.TakeDue(20));
        var batch = queue.TakeDue(21); Assert.True(queue.TryEnqueue(21, "new"));
        Assert.Equal(new[] { "first", "second" }, batch); Assert.Equal(new[] { "new" }, queue.TakeDue(21));
        queue.TryEnqueue(42, "cancel"); queue.Clear(); Assert.Equal(0, queue.Count); Assert.Empty(queue.TakeDue(100));
    }
}
