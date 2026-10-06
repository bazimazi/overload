using System.Collections.Immutable;
using System.Numerics;
using Overload.Content;
using Overload.Domain;
using Xunit;

namespace Overload.Domain.Tests;

public class ResolverTests
{
    private const string Pursuit = "pattern.assault.pursuit", Afterstrike = "pattern.assault.afterstrike", Convergence = "pattern.assault.convergence";
    private static BalanceProfile Profile => ProfileLoader.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "arena.json")));
    private sealed class World : IActionPreflight
    {
        public long Revision;
        public Vector2 Origin = new(20, 30);
        public ImmutableDictionary<ActionImplementation, PreflightResult> Results = Enum.GetValues<ActionImplementation>()
            .ToImmutableDictionary(i => i, _ => PreflightResult.Ready);
        public ActionWorldSnapshot Capture(ActionIntent intent, SkillDefinition skill, ActorBehaviorProfile behavior) => new(Revision, Origin, Results);
        public void Block(ActionImplementation implementation, string reason = "Path blocked")
        { Results = Results.SetItem(implementation, PreflightResult.Blocked(reason)); Revision++; }
    }
    private static BalanceProfile FastProfile => Profile with
    {
        Skills = Profile.Skills.Select(s => s with { Windup = 0, Active = 1, Recovery = 0, Cooldown = 0 }).ToArray(),
        Hero = Profile.Hero with { FocusPerSecond = 0 },
        Memories = Profile.Memories with { GenerationCooldownTicks = 1 }
    };
    private static PlayerCombat Player(BalanceProfile? profile = null, World? world = null)
    { var p = new PlayerCombat(profile ?? Profile, world ?? new World()); p.SetCombatActive(true); return p; }
    private static void Grant(PlayerCombat p, bool momentum = true, bool echo = false)
    {
        if (momentum) Assert.True(p.Memories.ObserveLocomotion(new(96, 0), LocomotionKind.Walk));
        if (echo) Assert.True(p.Memories.ObserveEvadedHit(new(p.Tick + 1, p.Tick + 600, Vector2.UnitY), SourceKind.BasePlayerAction));
    }
    private static void Advance(PlayerCombat p, int ticks) { for (var i = 0; i < ticks; i++) p.AdvanceClock(); }
    private static BalanceProfile Bind(BalanceProfile p, params BindingDefinition[] bindings) => p with { Overload = p.Overload with { Bindings = [.. bindings] } };
    private sealed record ObservedState(long Revision, long MemoryRevision, long Tick, int Focus, int Strain, BigInteger Life, int Flasks,
        ActionExecution? Action, MemoryToken? Momentum, MemoryToken? Echo, int PulseCooldown, int GenerationCooldown, string Reason);
    private static ObservedState State(PlayerCombat p) =>
        new(p.Revision, p.Memories.Revision, p.Tick, p.Focus, p.Strain, p.Life, p.FlaskCharges, p.Action, p.Memories.Momentum, p.Memories.Echo,
            p.Cooldown(SkillId.ShieldPulse), p.Memories.CooldownTicks(MemoryType.Momentum), p.LastReason);

    [Fact]
    public void OL01_NoMemoriesUsesExactlyOneBaseAction()
    {
        var p = Player(); var plan = p.Preview(SkillId.Cleave, Vector2.UnitX);
        Assert.Equal(ActionImplementation.Base, plan.Selection.Implementation); Assert.Empty(plan.Selection.Tokens);
        Assert.True(p.TryCommit(plan)); var action = p.Action;
        Assert.False(p.TryCommit(plan)); Assert.Same(action, p.Action);
        Assert.Equal(1, action!.RootActionId); Assert.Equal(SourceKind.BasePlayerAction, action.Source);
    }
    [Fact]
    public void OL02_PursuitCommitsOnePaymentAndPreservesGenerationLocks()
    {
        var p = Player(); Grant(p, echo: true); var echo = p.Memories.Echo; var momentum = p.Memories.Momentum;
        var plan = p.Preview(SkillId.ShieldPulse, Vector2.UnitX);
        Assert.Equal(Pursuit, plan.Selection.DefinitionId); Assert.True(p.TryCommit(plan));
        Assert.Equal(75000, p.Focus); Assert.Equal(25000, p.Strain); Assert.Equal(360, p.Cooldown(SkillId.ShieldPulse));
        Assert.Null(p.Memories.Momentum); Assert.Same(echo, p.Memories.Echo);
        Assert.Equal(120, p.Memories.CooldownTicks(MemoryType.Momentum));
        Assert.Equal(momentum, Assert.Single(p.Action!.ConsumedMemories));
        Assert.Equal(new Vector2(20, 30), p.Action.Origin); Assert.Equal(SourceKind.OverloadedPlayerAction, p.Action.Source);
        Assert.False(p.TryCommit(plan)); Assert.Equal(75000, p.Focus); Assert.Equal(25000, p.Strain);
        Assert.False(p.TryStart(SkillId.ShieldPulse, Vector2.UnitX)); Assert.Same(echo, p.Memories.Echo);
    }
    [Fact]
    public void OL03_TwoMemoriesWinIndependentOfContentAndRowOrder()
    {
        var profile = Bind(Profile, new(Pursuit, 0), new(Afterstrike, 1), new(Convergence, 999));
        foreach (var reverse in new[] { false, true })
        {
            var p = Player(reverse ? profile with { Overload = profile.Overload with { Patterns = [.. profile.Overload.Patterns.Reverse()], Bindings = [.. profile.Overload.Bindings.Reverse()] } } : profile);
            Grant(p, echo: true); Assert.True(p.TryStart(SkillId.Cleave, Vector2.UnitX));
            Assert.Equal(Convergence, p.Action!.SelectedDefinitionId); Assert.Equal(45000, p.Strain);
            Assert.Null(p.Memories.Momentum); Assert.Null(p.Memories.Echo); Assert.Equal(2, p.Action.ConsumedMemories.Length);
        }
    }
    [Theory]
    [InlineData(0, 1, Pursuit)] [InlineData(2, 1, Afterstrike)] [InlineData(1, 1, Afterstrike)]
    public void OL04_SavedPriorityThenStableIdBreakEqualSpecificityTies(int pursuitPriority, int echoPriority, string expected)
    {
        var p = Player(Bind(Profile, new(Pursuit, pursuitPriority), new(Afterstrike, echoPriority)));
        Grant(p, echo: true); Assert.True(p.TryStart(SkillId.Cleave, Vector2.UnitX));
        Assert.Equal(expected, p.Action!.SelectedDefinitionId);
        Assert.Equal(expected == Afterstrike, p.Memories.Momentum is not null);
        Assert.Equal(expected == Pursuit, p.Memories.Echo is not null);
    }
    [Fact]
    public void OL05_NinetyStrainFallsBackWithoutDeletingMemoriesOrChangingStrain()
    {
        var p = Player(FastProfile);
        for (var i = 0; i < 3; i++) { Grant(p, false, true); Assert.True(p.TryStart(SkillId.Cleave, Vector2.UnitX)); p.AdvanceClock(); }
        Assert.Equal(90000, p.Strain); Grant(p, echo: true); var m = p.Memories.Momentum; var e = p.Memories.Echo;
        var plan = p.Preview(SkillId.Cleave, Vector2.UnitX);
        Assert.Equal(ActionImplementation.Base, plan.Selection.Implementation); Assert.Equal(2, plan.Selection.Rejections.Length);
        Assert.True(p.TryCommit(plan)); Assert.Equal(90000, p.Strain); Assert.Same(m, p.Memories.Momentum); Assert.Same(e, p.Memories.Echo);
    }
    [Fact]
    public void OL06_ExactHundredStrainIsAllowed()
    {
        var p = Player(FastProfile);
        for (var i = 0; i < 4; i++) { Grant(p); Assert.True(p.TryStart(SkillId.Cleave, Vector2.UnitX)); p.AdvanceClock(); }
        Assert.Equal(100000, p.Strain); Assert.Equal(ActionImplementation.Pursuit, p.Action?.Implementation ?? p.LastSelection!.Implementation);
        Grant(p); Assert.True(p.TryStart(SkillId.Cleave, Vector2.UnitX)); Assert.Equal(ActionImplementation.Base, p.Action!.Implementation);
        Assert.Equal(100000, p.Strain); Assert.NotNull(p.Memories.Momentum);
    }
    [Fact]
    public void OL07_ExpensiveSpecificCandidateFallsThroughToAffordablePattern()
    {
        var profile = Bind(FastProfile, new(Convergence, 0), new(Pursuit, 1), new(Afterstrike, 2));
        var p = Player(profile);
        for (var i = 0; i < 3; i++) { Grant(p); p.TryStart(SkillId.Cleave, Vector2.UnitX); p.AdvanceClock(); }
        Grant(p, echo: true); var plan = p.Preview(SkillId.Cleave, Vector2.UnitX);
        Assert.Equal(Pursuit, plan.Selection.DefinitionId);
        Assert.Equal(new CandidateRejection(Convergence, "Not enough Strain capacity"), Assert.Single(plan.Selection.Rejections));
        Assert.True(p.TryCommit(plan)); Assert.Equal(plan.Selection.DefinitionId, p.Action!.SelectedDefinitionId);
        Assert.Equal(100000, p.Strain); Assert.NotNull(p.Memories.Echo);
    }
    [Fact]
    public void OL08_ExpiryPrecedesAcceptanceAndOldPreviewCannotSpend()
    {
        var p = Player(); Grant(p); Advance(p, 299); var plan = p.Preview(SkillId.Cleave, Vector2.UnitX);
        Assert.Equal(Pursuit, plan.Selection.DefinitionId); p.AdvanceClock(); Assert.Null(p.Memories.Momentum);
        Assert.False(p.TryCommit(plan)); Assert.Null(p.Action); Assert.Equal(0, p.Strain);
        Assert.True(p.TryStart(SkillId.Cleave, Vector2.UnitX)); Assert.Equal(ActionImplementation.Base, p.Action!.Implementation);
    }
    [Fact]
    public void OL11_ThousandsOfPreviewsAreReadOnlyAndMatchTheUnchangedSnapshot()
    {
        var p = Player(); Grant(p, echo: true); var before = State(p);
        ActionPlan? plan = null;
        for (var i = 0; i < 10000; i++) plan = p.Preview(SkillId.ShieldPulse, Vector2.UnitY);
        Assert.Equal(before, State(p)); Assert.True(p.TryCommit(plan!));
        Assert.Equal(plan!.Selection.DefinitionId, p.Action!.SelectedDefinitionId);
        Assert.Equal(plan.Selection.FocusCost, before.Focus - p.Focus);
        Assert.Equal(Vector2.UnitY, p.Action.Aim);
    }
    [Fact]
    public void OL12_ChangedGeometryRejectsAtomicallyAndNextInputCanFallback()
    {
        var world = new World(); var p = Player(world: world); Grant(p, echo: true);
        var plan = p.Preview(SkillId.ShieldPulse, Vector2.UnitX); var before = State(p);
        world.Block(ActionImplementation.Pursuit);
        Assert.False(p.TryCommit(plan)); Assert.Equal("Path blocked", p.LastReason);
        Assert.Equal(before, State(p) with { Reason = before.Reason }); // Only the visible reason changes.
        var fallback = p.Preview(SkillId.ShieldPulse, Vector2.UnitX);
        Assert.Equal(Afterstrike, fallback.Selection.DefinitionId); Assert.True(p.TryCommit(fallback));
        Assert.Equal(75000, p.Focus); Assert.Equal(30000, p.Strain); Assert.NotNull(p.Memories.Momentum); Assert.Null(p.Memories.Echo);
        Assert.Equal(1, p.Action!.RootActionId);
    }
    [Fact]
    public void ChangedButLegalWorldRevalidatesAndUsesFreshOrigin()
    {
        var world = new World(); var p = Player(world: world); Grant(p);
        var plan = p.Preview(SkillId.Cleave, Vector2.UnitX); world.Revision++; world.Origin = new(50, 60);
        Assert.True(p.TryCommit(plan)); Assert.Equal(world.Origin, p.Action!.Origin); Assert.NotEqual(plan.Origin, p.Action.Origin);
    }
    [Fact]
    public void RefreshedTokenInvalidatesAPlanEvenWithinTheSameActorRevision()
    {
        var p = Player(); Grant(p); Advance(p, 120);
        var plan = p.Preview(SkillId.ShieldPulse, Vector2.UnitX);
        Grant(p); var replacement = p.Memories.Momentum;
        Assert.False(p.TryCommit(plan)); Assert.Same(replacement, p.Memories.Momentum);
        Assert.Equal(100000, p.Focus); Assert.Equal(0, p.Strain); Assert.Equal(0, p.Cooldown(SkillId.ShieldPulse));
    }
    [Fact]
    public void PlansCannotCrossActorsOrCheckpointResets()
    {
        var p = Player(); var other = Player(); Grant(p); Grant(other);
        var plan = p.Preview(SkillId.Cleave, Vector2.UnitX);
        Assert.False(other.TryCommit(plan)); Assert.NotNull(other.Memories.Momentum); Assert.Equal(0, other.Strain);
        p.Reset(); Assert.False(p.TryCommit(plan)); Assert.Null(p.Action);
    }
    [Fact]
    public void BaselineCostFailureRejectsBeforeConsideringPatternsAndExtraCostCanFallback()
    {
        var profile = FastProfile;
        profile = profile with { Overload = profile.Overload with { Patterns = [.. profile.Overload.Patterns.Select(p => p.Id == Pursuit ? p with { ExtraFocusCost = 100 } : p)] } };
        var p = Player(profile); Grant(p, echo: true);
        Assert.Equal(Afterstrike, p.Preview(SkillId.ShieldPulse, Vector2.UnitX).Selection.DefinitionId);
        Assert.True(p.TryStart(SkillId.ShieldPulse, Vector2.UnitX)); Assert.Equal(75000, p.Focus); Assert.NotNull(p.Memories.Momentum);
        for (var i = 0; i < 3; i++) { p.AdvanceClock(); Assert.True(p.TryStart(SkillId.ShieldPulse, Vector2.UnitX)); }
        p.AdvanceClock(); var plan = p.Preview(SkillId.ShieldPulse, Vector2.UnitX);
        Assert.False(plan.Selection.Accepted); Assert.Equal("Not enough Focus", plan.Selection.Reason); Assert.Empty(plan.Selection.Rejections);
        Assert.False(p.TryCommit(plan)); Assert.Equal(0, p.Focus); Assert.NotNull(p.Memories.Momentum);
        Assert.True(p.TryStart(SkillId.Cleave, Vector2.UnitX));
    }
    [Fact]
    public void CapabilityAndGeometryFailuresTryTheNextSignatureThenBase()
    {
        var profile = Profile with { Skills = Profile.Skills.Select(s => s with { Capabilities = s.Capabilities & ~ActionCapabilities.GroundAdvance }).ToArray() };
        var world = new World(); var p = Player(profile, world); Grant(p, echo: true);
        Assert.Equal(Afterstrike, p.Preview(SkillId.Cleave, Vector2.UnitX).Selection.DefinitionId);
        world.Block(ActionImplementation.Afterstrike, "No legal target");
        var plan = p.Preview(SkillId.Cleave, Vector2.UnitX); Assert.Equal(ActionImplementation.Base, plan.Selection.Implementation);
        Assert.Equal(2, plan.Selection.Rejections.Length); Assert.True(p.TryCommit(plan));
        Assert.NotNull(p.Memories.Momentum); Assert.NotNull(p.Memories.Echo); Assert.Equal(0, p.Strain);
    }
    [Fact]
    public void FailedBasePreflightSpendsNothing()
    {
        var world = new World(); world.Block(ActionImplementation.Base, "No legal base geometry"); var p = Player(world: world);
        Assert.False(p.TryStart(SkillId.ShieldPulse, Vector2.UnitX)); Assert.Equal("No legal base geometry", p.LastReason);
        Assert.Equal(100000, p.Focus); Assert.Equal(0, p.Cooldown(SkillId.ShieldPulse)); Assert.Null(p.Action);
    }
    [Fact]
    public void StrainDecayUsesExactSubunitsAndBaseActionsDoNotRestartDelay()
    {
        var p = Player(); Grant(p); p.TryStart(SkillId.Cleave, Vector2.UnitX);
        Advance(p, 60); Assert.Equal(25000, p.Strain); p.TryStart(SkillId.Cleave, Vector2.UnitX);
        p.AdvanceClock(); Assert.Equal(24800, p.Strain); Advance(p, 59); Assert.Equal(13000, p.Strain);
        Advance(p, 65); Assert.Equal(0, p.Strain); Advance(p, 500); Assert.Equal(0, p.Strain);
    }
    [Fact]
    public void SuccessfulOverloadRestartsDecayDelay()
    {
        var p = Player(FastProfile); Grant(p); p.TryStart(SkillId.Cleave, Vector2.UnitX); Advance(p, 70);
        Assert.Equal(23000, p.Strain); Grant(p); p.TryStart(SkillId.Cleave, Vector2.UnitX);
        Advance(p, 60); Assert.Equal(48000, p.Strain); p.AdvanceClock(); Assert.Equal(47800, p.Strain);
    }
    [Fact]
    public void ExitClearsStrainAtFiveSecondsAndReentryCancelsTheExitTimer()
    {
        var p = Player(FastProfile);
        for (var i = 0; i < 4; i++) { Grant(p); p.TryStart(SkillId.Cleave, Vector2.UnitX); p.AdvanceClock(); }
        p.SetCombatActive(false); Advance(p, 299); Assert.True(p.Strain > 0);
        p.SetCombatActive(true); p.AdvanceClock(); Assert.True(p.Strain > 0);
        p.SetCombatActive(false); Advance(p, 300); Assert.Equal(0, p.Strain);
    }
    [Fact]
    public void DeathAndCheckpointClearStrainImmediately()
    {
        var p = Player(); Grant(p); p.TryStart(SkillId.Cleave, Vector2.UnitX);
        p.ReceiveHit(10000000, false); Assert.Equal(0, p.Strain); Assert.Null(p.Memories.Momentum);
        p.Reset(); p.SetCombatActive(true); Grant(p); p.TryStart(SkillId.Cleave, Vector2.UnitX); Assert.Equal(25000, p.Strain);
        p.Reset(); Assert.Equal(0, p.Strain); Assert.Null(p.LastSelection); Assert.Null(p.Action);
    }
    [Fact]
    public void CancelingAcceptedWindupNeverRefundsPaymentOrReevaluatesItsSignature()
    {
        var p = Player(); Grant(p); p.TryStart(SkillId.ShieldPulse, Vector2.UnitX); var action = p.Action;
        Assert.True(p.TryStart(SkillId.Traverse, Vector2.UnitY)); Assert.NotSame(action, p.Action);
        Assert.Equal(75000, p.Focus); Assert.Equal(25000, p.Strain); Assert.Null(p.Memories.Momentum);
        Assert.Equal(360, p.Cooldown(SkillId.ShieldPulse));
    }
    [Fact]
    public void BuildChangesAreValidatedIdleOnlyAndInvalidateProposals()
    {
        var p = Player(); Grant(p); var before = p.Behavior;
        Assert.False(p.TryChangeBindings([])); Assert.Same(before, p.Behavior); Assert.NotNull(p.Memories.Momentum);
        p.SetCombatActive(false); var plan = p.Preview(SkillId.Cleave, Vector2.UnitX);
        Assert.False(p.TryChangeBindings([new("unknown", 0)])); Assert.Same(before, p.Behavior);
        Assert.True(p.TryChangeBindings([new(Afterstrike, 0)])); Assert.False(p.TryCommit(plan));
        Assert.Single(p.Behavior.Bindings); Assert.Equal(MemoryClearReason.BuildChanged, p.Memories.LastClearReason);
    }
    [Fact]
    public void DefaultAdapterNeverSpendsTokensForUnimplementedEffects()
    {
        var p = new PlayerCombat(Profile); p.SetCombatActive(true); Grant(p, echo: true);
        var plan = p.Preview(SkillId.Cleave, Vector2.UnitX); Assert.Equal(ActionImplementation.Base, plan.Selection.Implementation);
        Assert.Equal(2, plan.Selection.Rejections.Length); Assert.True(p.TryCommit(plan));
        Assert.NotNull(p.Memories.Momentum); Assert.NotNull(p.Memories.Echo); Assert.Equal(0, p.Strain);
    }
    [Fact]
    public void InvalidAimIsRejectedWithoutPayment()
    {
        var p = Player(); Grant(p);
        Assert.False(p.TryStart(SkillId.ShieldPulse, new(float.NaN, 0))); Assert.Null(p.Action); Assert.Equal(100000, p.Focus); Assert.NotNull(p.Memories.Momentum);
        Assert.True(p.TryStart(SkillId.Cleave, Vector2.Zero)); Assert.Equal(Vector2.UnitX, p.Action!.Aim);
    }
    [Fact]
    public void InvalidDefinitionsAndIndistinguishableNormalizedSignaturesFailValidation()
    {
        var d = Profile.Overload; var p = d.Patterns[0];
        var invalid = new[]
        {
            d with { Bindings = [new("unknown", 0)] }, d with { BindingSlots = 1 }, d with { MaximumStrain = 101 },
            d with { BindingSlots = 2, Bindings = [new(Convergence, 0)] },
            d with { Patterns = [p with { RequiredMemories = [MemoryType.Momentum, MemoryType.Momentum] }] },
            d with { Patterns = [p with { StrainCost = -1 }] }, d with { Patterns = [p with { ImplementationId = "System.Reflection" }] },
            d with { Patterns = [p with { Family = ActionFamily.Recover }] }, d with { Patterns = [p with { RequiredCapabilities = ActionCapabilities.None }] },
            d with { Bindings = [new(Pursuit, 0), new(Pursuit, 1)] }, d with { Bindings = [new(Pursuit, -1)] }
        };
        foreach (var definition in invalid) Assert.NotEmpty(ActorBehaviorProfile.Validate(definition));
        var one = p with { RequiredMemories = [MemoryType.Momentum, MemoryType.Echo] };
        var two = one with { Id = "duplicate", RequiredMemories = [MemoryType.Echo, MemoryType.Momentum] };
        Assert.Throws<ArgumentException>(() => ActorBehaviorProfile.Compile(d with { Patterns = [one, two], Bindings = [new(one.Id, 0), new(two.Id, 1)] }));
        Assert.Throws<InvalidDataException>(() => ProfileLoader.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "arena.json")).Replace("assault.pursuit.v1", "unregistered")));
    }
    [Fact]
    public void AcceptedCrossingCarriesOverloadedProvenanceAndCannotGenerateEcho()
    {
        var p = Player(); Grant(p);
        Assert.True(p.TryStart(SkillId.Traverse, Vector2.UnitX));
        Assert.Equal(ActionImplementation.Crossing, p.Action!.Implementation);
        Assert.Equal(SourceKind.OverloadedPlayerAction, p.Action.Source); Assert.Equal(25000, p.Strain); Assert.Null(p.Memories.Momentum);
        Assert.False(p.ReceiveHit(10000, true, hostile: new(42, 600, Vector2.UnitY))); Assert.Null(p.Memories.Echo);
        Assert.False(p.Memories.ObserveLocomotion(new(160, 0), LocomotionKind.StandardEvade, p.Action.Source));
        Assert.Null(p.Memories.Momentum);
    }
    [Fact]
    public void SpendingEchoDoesNotEraseItsRootReceipt()
    {
        var p = Player(); Grant(p, false, true); Assert.True(p.TryStart(SkillId.Cleave, Vector2.UnitX));
        Assert.Null(p.Memories.Echo); Assert.Equal(1, p.Memories.TrackedEchoAttackCount);
        Assert.Equal(120, p.Memories.CooldownTicks(MemoryType.Echo)); Advance(p, 120);
        Assert.False(p.Memories.ObserveEvadedHit(new(1, 600, Vector2.UnitY), SourceKind.BasePlayerAction)); Assert.Null(p.Memories.Echo);
    }
    [Fact]
    public void InvalidWorldEvidenceCannotCommitResources()
    {
        var world = new World(); var p = Player(world: world); Grant(p);
        var plan = p.Preview(SkillId.ShieldPulse, Vector2.UnitX); world.Origin = new(float.NaN, 0);
        Assert.False(p.TryCommit(plan)); Assert.Equal("Invalid world origin", p.LastReason);
        Assert.Null(p.Action); Assert.Equal(100000, p.Focus); Assert.NotNull(p.Memories.Momentum); Assert.Equal(0, p.Strain);
    }
    [Fact]
    public void SeededActionSequencesConserveTokensResourcesAndSingleExecution()
    {
        var p = Player(FastProfile); var random = new Random(771);
        for (var i = 0; i < 2000; i++)
        {
            p.AdvanceClock();
            if (random.Next(2) == 0) Grant(p, echo: random.Next(2) == 0);
            var id = random.Next(2) == 0 ? SkillId.Cleave : SkillId.ShieldPulse;
            var plan = p.Preview(id, Vector2.UnitX); var focus = p.Focus; var strain = p.Strain;
            var momentum = p.Memories.Momentum; var echo = p.Memories.Echo; var action = p.Action;
            Assert.Equal(plan.Selection.Accepted, p.TryCommit(plan));
            if (plan.Selection.Accepted)
            {
                Assert.Equal(focus - plan.Selection.FocusCost, p.Focus); Assert.Equal(strain + plan.Selection.StrainCost, p.Strain);
                Assert.Equal(plan.Selection.Tokens.Any(t => t.Type == MemoryType.Momentum) ? null : momentum, p.Memories.Momentum);
                Assert.Equal(plan.Selection.Tokens.Any(t => t.Type == MemoryType.Echo) ? null : echo, p.Memories.Echo);
                var accepted = p.Action; Assert.False(p.TryCommit(plan)); Assert.Same(accepted, p.Action);
            }
            else { Assert.Equal(focus, p.Focus); Assert.Equal(strain, p.Strain); Assert.Same(action, p.Action); }
            Assert.InRange(p.Focus, 0, p.MaximumFocus); Assert.InRange(p.Strain, 0, p.MaximumStrain);
        }
    }
}
