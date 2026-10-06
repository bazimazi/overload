using System.Numerics;
using Overload.Content;
using Overload.Domain;
using Xunit;

namespace Overload.Domain.Tests;

public class MemoryTests
{
    private static BalanceProfile Profile => ProfileLoader.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "arena.json")));
    private static CombatMemories Memories()
    {
        var memories = new CombatMemories(Profile.Memories);
        memories.SetCombatActive(true);
        return memories;
    }
    private static void Move(CombatMemories memories, long tick, float distance, LocomotionKind kind = LocomotionKind.Walk)
    { memories.AdvanceTo(tick); memories.ObserveLocomotion(new Vector2(distance, 0), kind); }

    [Fact]
    public void MomentumRequiresActualDistanceInRollingWindow()
    {
        var m = Memories();
        Move(m, 1, 48); Move(m, 121, 47); // Tick 1 is outside (tick-120, tick].
        Assert.Null(m.Momentum);
        Move(m, 122, 48); Assert.Null(m.Momentum);
        Move(m, 123, 1); Assert.NotNull(m.Momentum);
        Assert.Equal(Vector2.UnitX, m.Momentum.Direction); Assert.Equal(0, m.MomentumDistance);
        Assert.Equal(423, m.Momentum.ExpiresAt);
    }
    [Fact]
    public void WalkingRefreshesOneTokenOnlyAfterGenerationCooldown()
    {
        var m = Memories(); Move(m, 1, 96); var first = m.Momentum;
        Move(m, 2, 96); Assert.Same(first, m.Momentum);
        m.AdvanceTo(120); Assert.Same(first, m.Momentum);
        m.AdvanceTo(121); m.ObserveLocomotion(Vector2.UnitY, LocomotionKind.Walk);
        Assert.NotEqual(first!.Id, m.Momentum!.Id);
        Assert.Equal(Vector2.UnitY, m.Momentum.Direction); Assert.Equal(421, m.Momentum.ExpiresAt);
        Assert.Equal(0, m.MomentumDistance);
    }
    [Fact]
    public void DuplicateMovementObservationAndZeroDisplacementCannotFarmMomentum()
    {
        var m = Memories(); Move(m, 1, 48);
        for (var i = 0; i < 1000; i++) m.ObserveLocomotion(new Vector2(48, 0), LocomotionKind.Walk);
        Assert.Null(m.Momentum); Assert.Equal(48, m.MomentumDistance);
        for (var tick = 2; tick <= 10000; tick++) Move(m, tick, 0);
        Assert.Null(m.Momentum); Assert.Equal(0, m.MomentumDistance);
    }
    [Theory]
    [InlineData(LocomotionKind.Teleport)] [InlineData(LocomotionKind.Knockback)] [InlineData(LocomotionKind.OverloadedAction)]
    public void NonLocomotionCannotGenerateOrPrimeMomentum(LocomotionKind kind)
    {
        var m = Memories(); Move(m, 1, 500, kind); Move(m, 2, 1);
        Assert.Null(m.Momentum); Assert.Equal(1, m.MomentumDistance);
    }
    [Fact]
    public void StandardEvadeCountsButOverloadedRootAndDescendantsDoNot()
    {
        var m = Memories(); Move(m, 1, 96, LocomotionKind.StandardEvade); Assert.NotNull(m.Momentum);
        m.Reset(); m.SetCombatActive(true);
        Assert.False(m.ObserveLocomotion(new(96, 0), LocomotionKind.Walk, SourceKind.OverloadedPlayerAction));
        Assert.False(m.ObserveLocomotion(new(96, 0), LocomotionKind.StandardEvade, SourceKind.SecondaryEffect));
        Assert.False(m.ObserveEvadedHit(new(1, 600, Vector2.UnitX), SourceKind.OverloadedPlayerAction));
        Assert.False(m.ObserveEvadedHit(new(1, 600, Vector2.UnitX), SourceKind.SecondaryEffect));
        Assert.Null(m.Momentum); Assert.Null(m.Echo); Assert.Equal(0, m.MomentumDistance);
    }
    [Fact]
    public void OneHostileRootCannotRefreshEchoEvenAfterCooldown()
    {
        var m = Memories(); var hit = new HostileAttackEvidence(42, 500, -Vector2.UnitX);
        Assert.True(m.ObserveEvadedHit(hit, SourceKind.BasePlayerAction)); var first = m.Echo;
        m.AdvanceTo(120);
        Assert.False(m.ObserveEvadedHit(hit, SourceKind.BasePlayerAction)); Assert.Same(first, m.Echo);
        m.AdvanceTo(300); Assert.Null(m.Echo);
        Assert.False(m.ObserveEvadedHit(hit, SourceKind.BasePlayerAction)); Assert.Null(m.Echo);
        m.AdvanceTo(500); Assert.Equal(0, m.TrackedEchoAttackCount);
        Assert.False(m.ObserveEvadedHit(hit, SourceKind.BasePlayerAction));
    }
    [Fact]
    public void EchoRefreshReplacesPayloadAndDoesNotStack()
    {
        var m = Memories(); Assert.True(m.ObserveEvadedHit(new(1, 500, Vector2.UnitX), SourceKind.BasePlayerAction));
        m.AdvanceTo(119); Assert.False(m.ObserveEvadedHit(new(2, 500, Vector2.UnitY), SourceKind.BasePlayerAction));
        m.AdvanceTo(120); Assert.True(m.ObserveEvadedHit(new(2, 500, Vector2.UnitY), SourceKind.BasePlayerAction));
        Assert.Equal(2, m.Echo!.HostileAttackId); Assert.Equal(Vector2.UnitY, m.Echo.Direction); Assert.Equal(420, m.Echo.ExpiresAt);
    }
    [Fact]
    public void ExpirationPrecedesSelectionAndReadsNeverAdvanceTimers()
    {
        var m = Memories(); Move(m, 1, 96); m.ObserveEvadedHit(new(1, 600, Vector2.UnitX), SourceKind.BasePlayerAction);
        m.AdvanceTo(300); var token = m.Momentum;
        for (var i = 0; i < 1000; i++) { Assert.Equal(1, m.RemainingTicks(MemoryType.Momentum)); Assert.Same(token, m.Get(MemoryType.Momentum)); }
        Assert.Equal(300, m.Tick);
        m.AdvanceTo(301); Assert.Null(m.Momentum); Assert.Null(m.Echo);
        Assert.Equal(0, m.RemainingTicks(MemoryType.Echo));
    }
    [Fact]
    public void OutsideCombatDoesNotAccumulateDistanceOrEcho()
    {
        var m = new CombatMemories(Profile.Memories);
        Move(m, 1, 96); Assert.False(m.ObserveEvadedHit(new(1, 600, Vector2.UnitX), SourceKind.BasePlayerAction));
        m.SetCombatActive(true); Move(m, 2, 1); Assert.Null(m.Momentum); Assert.Null(m.Echo);
        Move(m, 3, 95); Assert.NotNull(m.Momentum);
        m.SetCombatActive(false); Assert.Null(m.Momentum); Assert.Equal(0, m.MomentumDistance);
    }
    [Theory]
    [InlineData(MemoryClearReason.Death)] [InlineData(MemoryClearReason.EncounterExit)] [InlineData(MemoryClearReason.Checkpoint)]
    [InlineData(MemoryClearReason.EquipmentChanged)] [InlineData(MemoryClearReason.BuildChanged)]
    public void LifecycleClearsAllTransientMemoryState(MemoryClearReason reason)
    {
        var m = Memories(); Move(m, 1, 96); m.ObserveEvadedHit(new(1, 600, Vector2.UnitX), SourceKind.BasePlayerAction); Move(m, 2, 48);
        m.Clear(reason);
        Assert.Null(m.Momentum); Assert.Null(m.Echo); Assert.Equal(0, m.MomentumDistance); Assert.Equal(0, m.TrackedEchoAttackCount);
        Assert.Equal(0, m.CooldownTicks(MemoryType.Echo)); Assert.Equal(0, m.CooldownTicks(MemoryType.Momentum)); Assert.Equal(reason, m.LastClearReason);
    }
    [Fact]
    public void EchoOnlyComesFromActualEvasionRejection()
    {
        var p = new PlayerCombat(Profile); p.Memories.SetCombatActive(true);
        var hit = new HostileAttackEvidence(1, 600, Vector2.UnitX);
        Assert.True(p.ReceiveHit(10000, true, hostile: hit)); Assert.Null(p.Memories.Echo);
        p.TryStart(SkillId.Traverse, Vector2.UnitX); Assert.Null(p.Memories.Echo); // Empty dodge.
        Assert.True(p.ReceiveHit(10000, false, hostile: hit)); Assert.Null(p.Memories.Echo);
        Assert.False(p.ReceiveHit(10000, true, hostile: hit)); Assert.NotNull(p.Memories.Echo);
        for (var i = 0; i < 7; i++) p.AdvanceClock();
        var token = p.Memories.Echo;
        Assert.True(p.ReceiveHit(10000, true, hostile: hit with { RootActionId = 2 })); Assert.Same(token, p.Memories.Echo);
        p.ReceiveHit(10000000, false); Assert.Null(p.Memories.Echo); Assert.False(p.Memories.InCombat); Assert.Equal(MemoryClearReason.Death, p.Memories.LastClearReason);
        p.Reset(); Assert.Equal(0, p.Memories.Tick); Assert.Equal(0, p.Memories.TrackedEchoAttackCount);
    }
    [Fact]
    public void AttackReceiptsStayBoundedByLiveRootLifetimes()
    {
        var m = Memories();
        for (var i = 0; i < 10000; i++)
        {
            var tick = i * 120L; m.AdvanceTo(tick);
            Assert.True(m.ObserveEvadedHit(new(i + 1, tick + 360, Vector2.UnitX), SourceKind.BasePlayerAction));
            Assert.InRange(m.TrackedEchoAttackCount, 1, 3);
        }
    }
    [Fact]
    public void PhysicsRoundoffDoesNotLoseAFullEvadesMemory()
    {
        var m = Memories(); Move(m, 1, 95.99991f); Assert.NotNull(m.Momentum);
        m.Reset(); m.SetCombatActive(true); Move(m, 1, 95.99f); Assert.Null(m.Momentum);
    }
    [Fact]
    public void ContentRejectsUnsafeMemoryRules()
    {
        var p = Profile;
        Assert.NotEmpty(ProfileLoader.Validate(p with { Memories = p.Memories with { LifetimeTicks = 0 } }));
        Assert.NotEmpty(ProfileLoader.Validate(p with { Memories = p.Memories with { MomentumWindowTicks = int.MaxValue } }));
        Assert.NotEmpty(ProfileLoader.Validate(p with { Memories = p.Memories with { MomentumDistancePixels = float.NaN } }));
    }
}
