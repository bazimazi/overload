using System.Collections.Immutable;
using System.Numerics;

namespace Overload.Domain;
public enum TraversePhase { Standard, AnchorPlace, AnchorSwap }
public sealed record ReturnAnchor(Vector2 Position, string RoomId, long ExpiresAt);

public sealed class ActionExecution(SkillDefinition definition, long id, long startedAt, Vector2 aim, SourceKind source = SourceKind.BasePlayerAction,
    ActionImplementation implementation = ActionImplementation.Base, string? selectedDefinitionId = null, Vector2 origin = default,
    ImmutableArray<MemoryToken> consumedMemories = default, Vector2? destination = null, TraversePhase traversal = TraversePhase.Standard)
{
    public SkillDefinition Definition { get; } = definition;
    public long RootActionId { get; } = id;
    public long StartedAt { get; } = startedAt;
    public Vector2 Aim { get; } = aim;
    public SourceKind Source { get; } = source;
    public ActionImplementation Implementation { get; } = implementation;
    public string SelectedDefinitionId { get; } = selectedDefinitionId ?? definition.ContentId;
    public Vector2 Origin { get; } = origin;
    public Vector2? Destination { get; } = destination;
    public TraversePhase Traversal { get; } = traversal;
    public ImmutableArray<MemoryToken> ConsumedMemories { get; } = consumedMemories.IsDefault ? [] : consumedMemories;
    public bool Emitted { get; set; }
    public HashSet<int> Victims { get; } = [];
    public int Age(long tick) => checked((int)(tick - StartedAt));
    public ActionPhase Phase(long tick) => Age(tick) < Definition.Windup ? ActionPhase.Windup
        : Age(tick) < Definition.Windup + Definition.Active ? ActionPhase.Active : ActionPhase.Recovery;
}

/// <summary>Only this authority spends resources and advances the player's action clock.</summary>
public sealed class PlayerCombat
{
    private readonly BalanceProfile balance;
    private readonly IActionPreflight preflight;
    private readonly object planOwner = new();
    private long revision;
    private long? lastOverloadTick;
    private long? outsideCombatSince;
    private int strainRemainder;
    private readonly BigInteger damageScale;
    private readonly BigInteger grade;
    private readonly BigInteger encounterTier;
    private readonly bool quietRest;
    private readonly Dictionary<SkillId, long> readyAt = [];
    private long nextActionId;
    private int focusRemainder;
    private int healAge;
    private BigInteger healTotal;
    public long Tick { get; private set; }
    public long Revision => revision;
    public ActorBehaviorProfile Behavior { get; private set; }
    public int Strain { get; private set; }
    public int MaximumStrain => balance.Overload.MaximumStrain * 1000;
    public ActionSelection? LastSelection { get; private set; }
    public bool ElsewhereActive { get; private set; }
    public bool RedCovenantActive { get; private set; }
    private readonly List<LifeReservation> reservations = [];
    public IReadOnlyList<LifeReservation> Reservations => reservations.AsReadOnly();
    public int ReservedPermille => reservations.Sum(r=>r.Permille);
    public int PeakReservation { get; private set; }
    public bool HealedUnderReservation { get; private set; }
    public BigInteger UnreservedMaximumLife { get; private set; }
    public ReturnAnchor? Anchor { get; private set; }
    private (Vector2 Position, string RoomId, long CompleteAt)? pendingAnchor;
    public CombatMemories Memories { get; }
    public BigInteger MaximumLife => UnreservedMaximumLife * (1000-ReservedPermille)/1000;
    public BigInteger Life { get; private set; }
    public BigInteger Barrier { get; private set; }
    public long BarrierExpiresAt { get; private set; }
    public int Focus { get; private set; }
    public int MaximumFocus => balance.Hero.Focus * 1000;
    public int FlaskCharges { get; private set; }
    public ActionExecution? Action { get; private set; }
    public bool Dead => Life.IsZero;
    public bool Evading => Action?.Definition.Id == SkillId.Traverse && Action.Traversal != TraversePhase.AnchorPlace && Action.Age(Tick) < balance.Hero.EvadeWindow;
    public string LastReason { get; private set; } = "Ready";

    public PlayerCombat(BalanceProfile profile, IActionPreflight? actionPreflight = null, CharacterState? character = null, BigInteger? tier = null)
    {
        balance = profile with { Skills = [.. profile.Skills], Enemies = [.. profile.Enemies] };
        preflight = actionPreflight ?? new BaseActionPreflight();
        Behavior = ActorBehaviorProfile.Compile(profile.Overload);
        Memories = new(profile.Memories);
        damageScale = 50 + (character?.Might ?? 0); quietRest = character?.Inscriptions.Contains("quiet-rest") == true;
        grade = character?.AttunementGrade ?? 1; encounterTier = tier ?? 1;
        if (encounterTier < 1) throw new ArgumentOutOfRangeException(nameof(tier));
        UnreservedMaximumLife = EndlessRules.PlayerAmount(CombatMath.Points(profile.Hero.Life), character?.Resolve ?? 0, grade);
        Reset();
    }
    public void Reset()
    {
        reservations.Clear(); PeakReservation=0; HealedUnderReservation=false;
        Life = MaximumLife; Barrier = 0; BarrierExpiresAt = 0; Focus = MaximumFocus; FlaskCharges = balance.Hero.FlaskCharges;
        Tick = 0; Action = null; readyAt.Clear(); healTotal = 0; healAge = 0; focusRemainder = 0;
        LastReason = "Ready";
        Strain = 0; strainRemainder = 0; lastOverloadTick = null; outsideCombatSince = 0; LastSelection = null; revision++;
        Memories.Reset();
        Anchor = null; pendingAnchor = null;
        // Action identity remains monotonic across encounter retries.
    }
    public void AdvanceClock()
    {
        if (Dead) return;
        Tick++;
        reservations.RemoveAll(r=>r.ExpiresAt<=Tick);
        if (Tick >= BarrierExpiresAt) Barrier = 0;
        revision++;
        Memories.AdvanceTo(Tick);
        if (quietRest && !Memories.InCombat && Tick % 60 == 0) Life = BigInteger.Min(MaximumLife, Life + MaximumLife / 100);
        if (pendingAnchor is { } pending && Tick >= pending.CompleteAt)
        { Anchor = new(pending.Position, pending.RoomId, Tick + 240); pendingAnchor = null; }
        if (Anchor is { } anchor && Tick >= anchor.ExpiresAt) { Anchor = null; readyAt[SkillId.Traverse] = Tick + 30; }
        if (Memories.InCombat) outsideCombatSince = null;
        else outsideCombatSince ??= Tick - 1;
        if (outsideCombatSince is { } left && Tick - left >= balance.Overload.ExitClearTicks) { Strain = 0; strainRemainder = 0; }
        else if (lastOverloadTick is { } last && Tick - last > balance.Overload.DecayDelayTicks && Strain > 0)
        {
            strainRemainder += balance.Overload.DecayPerSecond * 1000;
            Strain = Math.Max(0, Strain - strainRemainder / 60);
            strainRemainder = Strain == 0 ? 0 : strainRemainder % 60;
        }
        focusRemainder += RedCovenantActive ? 0 : balance.Hero.FocusPerSecond * 1000;
        Focus = Math.Min(MaximumFocus, Focus + focusRemainder / 60);
        focusRemainder %= 60;
        if (healAge < balance.Hero.FlaskTicks && healTotal > 0)
        {
            var amount = healTotal * (healAge + 1) / balance.Hero.FlaskTicks - healTotal * healAge / balance.Hero.FlaskTicks;
            Life = BigInteger.Min(MaximumLife, Life + amount);
            if(ReservedPermille>0 && amount>0)HealedUnderReservation=true;
            healAge++;
        }
        if (Action is { } action && action.Age(Tick) >= action.Definition.Duration) Action = null;
    }
    public bool TryStart(SkillId id, Vector2 aim)
    {
        return TryCommit(Preview(id, aim));
    }
    public ActionPlan Preview(SkillId id, Vector2 aim)
    {
        if (!Enum.IsDefined(id)) throw new ArgumentOutOfRangeException(nameof(id));
        var validAim = float.IsFinite(aim.X) && float.IsFinite(aim.Y) && float.IsFinite(aim.LengthSquared());
        var intent = new ActionIntent(id, validAim && aim.LengthSquared() > 0 ? Vector2.Normalize(aim) : Vector2.UnitX);
        return new(planOwner, intent, Snapshot(intent, validAim ? null : "Invalid aim"));
    }
    private CombatSnapshot Snapshot(ActionIntent intent, string? failure = null)
    {
        var definition = balance.Skills.FirstOrDefault(s => s.Id == intent.Skill);
        if(definition is null) { failure="Skill is unavailable to this Frame";definition=new(intent.Skill,"action.unavailable",ActionFamily.Assault,0,1,0,0,0,0,0,0,0,0); }
        if (ElsewhereActive && intent.Skill == SkillId.Traverse)
            definition = definition with { ContentId = Anchor is null ? "action.elsewhere.place" : "action.elsewhere.swap",
                Windup = 0, Active = Anchor is null ? 9 : 7, Recovery = 0, Cooldown = Anchor is null ? 24 : 150, Capabilities = ActionCapabilities.None };
        failure ??= Dead ? "You have fallen" : Cooldown(intent.Skill) > 0 ? "Cooling down"
            : Action is { } old && !CanCancel(old, intent.Skill == SkillId.Traverse) ? "Action committed"
            : RedCovenantActive && !CanReserve(definition.FocusCost) ? "Life reservation would exceed 40% or leave less than 1 Life"
            : !RedCovenantActive && Focus < definition.FocusCost * 1000 ? "Not enough Focus"
            : intent.Skill == SkillId.Flask && (FlaskCharges == 0 || healAge < balance.Hero.FlaskTicks && healTotal > 0) ? "Flask unavailable" : null;
        var snapshot = new CombatSnapshot(revision, Memories.Revision, Tick, definition, Behavior, Focus, Strain, MaximumStrain,
            Memories.Momentum, Memories.Echo, failure, preflight.Capture(intent, definition, Behavior),Memories.Stillness,Memories.Rupture,RedCovenantActive);
        if (intent.Skill == SkillId.Flask && Life == MaximumLife && ActionResolver.Select(snapshot).Implementation != ActionImplementation.Reprieve)
            snapshot = snapshot with { CommonFailure = "Life is full" };
        return snapshot;
    }
    public bool TryCommit(ActionPlan plan)
    {
        if (!ReferenceEquals(plan.Owner, planOwner)) return Reject("Plan belongs to another actor");
        if (plan.SnapshotRevision != revision || plan.Snapshot.MemoryRevision != Memories.Revision)
            return Reject("Combat state changed; choose again");
        if (!plan.Selection.Accepted) return Reject(plan.Selection.Reason);
        // Re-query once immediately before commitment. A changed world cannot silently switch the promised action.
        var fresh = Snapshot(plan.Intent);
        var selected = ActionResolver.Select(fresh);
        if (!selected.Accepted) return Reject(selected.Reason);
        if (ElsewhereActive && plan.Intent.Skill == SkillId.Traverse && Anchor is not null && fresh.World.For(selected.Implementation).Destination is null)
            return Reject("Return point is unavailable");
        if (selected.DefinitionId != plan.Selection.DefinitionId || selected.Implementation != plan.Selection.Implementation)
            return Reject(selected.Rejections.FirstOrDefault(r => r.DefinitionId == plan.Selection.DefinitionId)?.Reason ?? "World changed; choose again");
        var executedSkill = selected.Implementation == ActionImplementation.Convergence ? fresh.Skill with { Windup = 15, Active = 1 }
            : selected.Implementation==ActionImplementation.Cascade ? fresh.Skill with { Windup=21,Active=1 } : fresh.Skill;
        var execution = new ActionExecution(executedSkill, nextActionId + 1, Tick, plan.Intent.Aim, selected.Source,
            selected.Implementation, selected.DefinitionId, fresh.World.Origin, selected.Tokens, fresh.World.For(selected.Implementation).Destination,
            ElsewhereActive && plan.Intent.Skill == SkillId.Traverse ? Anchor is null ? TraversePhase.AnchorPlace : TraversePhase.AnchorSwap : TraversePhase.Standard);
        if (!Memories.TryConsume(selected.Tokens)) return Reject("Memory changed; choose again");
        if(RedCovenantActive && fresh.Skill.FocusCost>0)
        {
            reservations.Add(new(nextActionId+1,fresh.Skill.FocusCost*4,Tick+240));
            PeakReservation=Math.Max(PeakReservation,ReservedPermille);
            Life=BigInteger.Min(Life,MaximumLife);
        }
        else if(!RedCovenantActive)Focus -= selected.FocusCost;
        Strain += selected.StrainCost;
        if (selected.Implementation != ActionImplementation.Base) { lastOverloadTick = Tick; strainRemainder = 0; }
        readyAt[plan.Intent.Skill] = Tick + selected.CooldownTicks;
        nextActionId++; Action = execution; revision++; LastSelection = selected;
        if (execution.Traversal == TraversePhase.AnchorPlace) pendingAnchor = (fresh.World.Origin, fresh.World.RoomId, Tick + 9);
        if (execution.Traversal == TraversePhase.AnchorSwap) Anchor = null;
        if (plan.Intent.Skill == SkillId.Flask)
        {
            FlaskCharges--;
            healAge = 0;
            healTotal = MaximumLife * (selected.Implementation == ActionImplementation.Reprieve ? 25 : balance.Hero.FlaskPercent) / 100;
            if (selected.Implementation == ActionImplementation.Reprieve) { Barrier = MaximumLife * 15 / 100; BarrierExpiresAt = Tick + 180; }
        }
        LastReason = selected.DefinitionId;
        return true;
    }
    public void SetCombatActive(bool active)
    {
        if (Memories.InCombat == active) return;
        Memories.SetCombatActive(active); outsideCombatSince = active ? null : Tick; revision++;
        if (!active) { Anchor = null; pendingAnchor = null; }
    }
    public bool TryChangeBindings(ImmutableArray<BindingDefinition> bindings)
    {
        if (Memories.InCombat || Action is not null || Dead) return Reject("Build changes require an idle actor outside combat");
        var definition = balance.Overload with { Bindings = bindings };
        var errors = ActorBehaviorProfile.Validate(definition);
        if (errors.Count > 0) return Reject(errors[0]);
        Behavior = ActorBehaviorProfile.Compile(definition);
        Memories.Clear(MemoryClearReason.BuildChanged); Anchor = null; pendingAnchor = null; revision++; LastSelection = null; LastReason = "Bindings updated";
        return true;
    }
    public bool TryChangeOaths(ImmutableArray<string> oaths)
    {
        if (Memories.InCombat || Action is not null || Dead) return Reject("Oaths change only at Hearth");
        if (oaths.IsDefault || oaths.Length > 1 || oaths.Any(o => o is not ("oath.elsewhere" or "oath.red-covenant"))) return Reject("Choose at most one known oath");
        ElsewhereActive = oaths.Contains("oath.elsewhere"); RedCovenantActive=oaths.Contains("oath.red-covenant");
        reservations.Clear();focusRemainder=0; Anchor = null; pendingAnchor = null; readyAt.Remove(SkillId.Traverse);
        Memories.Clear(MemoryClearReason.BuildChanged); revision++; return true;
    }
    public bool CancelRecoveryByMovement()
    {
        if (Action is not { } action || action.Definition.Id is SkillId.Traverse or SkillId.Flask || !CanCancel(action, false)) return false;
        Action = null; revision++;
        return true;
    }
    private bool CanCancel(ActionExecution action, bool evade)
    {
        if (action.Definition.Id is SkillId.Traverse or SkillId.Flask) return false;
        var age = action.Age(Tick);
        return age >= action.Definition.Windup + action.Definition.Active + 6
            || evade && age < action.Definition.Windup - 3;
    }
    public int Cooldown(SkillId id) => (int)Math.Max(0, readyAt.GetValueOrDefault(id) - Tick);
    private bool CanReserve(int focusCost) => ReservedPermille+focusCost*4<=400 && UnreservedMaximumLife*(1000-ReservedPermille-focusCost*4)/1000>=1000;
    public void ChangeMaximumLife(BigInteger maximum)
    {
        if(maximum<1000)throw new ArgumentOutOfRangeException(nameof(maximum));
        if(maximum*(1000-ReservedPermille)/1000<1000)throw new InvalidOperationException("Maximum Life would invalidate active reservations");
        UnreservedMaximumLife=maximum;Life=BigInteger.Min(Life,MaximumLife);revision++;
    }
    public void GrantBarrier(int percent, int ticks)
    {
        if(Dead||percent is <1 or >20||ticks is <1 or >300)return;
        Barrier=BigInteger.Max(Barrier,MaximumLife*percent/100);BarrierExpiresAt=Tick+ticks;revision++;
    }
    public BigInteger AttackBudget(SkillDefinition skill) => EndlessRules.PlayerAmount(CombatMath.Points(skill.Damage), damageScale - 50, grade);
    public bool ReceiveHit(BigInteger preDefenseDamage, bool evadable, bool supernatural = false, HostileAttackEvidence? hostile = null)
    {
        if (preDefenseDamage < 0) throw new ArgumentOutOfRangeException(nameof(preDefenseDamage));
        if (Dead) return false;
        if (evadable && Evading)
        {
            if (hostile is { } evidence) Memories.ObserveEvadedHit(evidence, Action!.Source);
            return false;
        }
        var damage = EndlessRules.Mitigated(preDefenseDamage, supernatural ? balance.Hero.Resistance : balance.Hero.Armor, grade, encounterTier, supernatural);
        var absorbed = BigInteger.Min(Barrier, damage); Barrier -= absorbed; damage -= absorbed;
        Life = BigInteger.Max(0, Life - damage);
        revision++;
        if (Dead) { reservations.Clear(); Action = null; Barrier = 0; Anchor = null; pendingAnchor = null; healTotal = 0; Strain = 0; strainRemainder = 0; lastOverloadTick = null; LastReason = "Fallen"; SetCombatActive(false); Memories.Clear(MemoryClearReason.Death); }
        return true;
    }
    private bool Reject(string reason) { LastReason = reason; return false; }
}
public sealed record LifeReservation(long ActionId, int Permille, long ExpiresAt);
