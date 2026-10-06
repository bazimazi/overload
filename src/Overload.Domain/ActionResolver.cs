using System.Collections.Immutable;
using System.Numerics;

namespace Overload.Domain;

public readonly record struct ActionIntent(SkillId Skill, Vector2 Aim);
public sealed record PreflightResult(bool Allowed, string Reason, Vector2? Destination = null)
{
    public static PreflightResult Ready { get; } = new(true, "Ready");
    public static PreflightResult Blocked(string reason) => new(false, reason);
}

/// <summary>Copied world-query results. Capturing must not mutate the world, actor, or RNG.</summary>
public sealed record ActionWorldSnapshot(long Revision, Vector2 Origin, ImmutableDictionary<ActionImplementation, PreflightResult> Results, string RoomId = "arena")
{
    public PreflightResult For(ActionImplementation implementation) => Results.GetValueOrDefault(implementation)
        ?? PreflightResult.Blocked("Pattern execution is not available yet");
}
public interface IActionPreflight
{
    ActionWorldSnapshot Capture(ActionIntent intent, SkillDefinition skill, ActorBehaviorProfile behavior);
}

/// <summary>Engine-independent default. Pattern support must be supplied explicitly by a world adapter.</summary>
public sealed class BaseActionPreflight : IActionPreflight
{
    public ActionWorldSnapshot Capture(ActionIntent intent, SkillDefinition skill, ActorBehaviorProfile behavior) =>
        new(0, Vector2.Zero, ImmutableDictionary<ActionImplementation, PreflightResult>.Empty.Add(ActionImplementation.Base, PreflightResult.Ready));
}

public sealed record CombatSnapshot(long Revision, long MemoryRevision, long Tick, SkillDefinition Skill,
    ActorBehaviorProfile Behavior, int Focus, int Strain, int MaximumStrain, MemoryToken? Momentum, MemoryToken? Echo,
    string? CommonFailure, ActionWorldSnapshot World, MemoryToken? Stillness=null,MemoryToken? Rupture=null, bool LifePayment=false);
public sealed record CandidateRejection(string DefinitionId, string Reason);
public sealed record ActionSelection(bool Accepted, string DefinitionId, ActionImplementation Implementation,
    int FocusCost, int StrainCost, int CooldownTicks, ImmutableArray<MemoryToken> Tokens,
    ImmutableArray<CandidateRejection> Rejections, string Reason)
{
    public SourceKind Source => Implementation == ActionImplementation.Base ? SourceKind.BasePlayerAction : SourceKind.OverloadedPlayerAction;
}

/// <summary>Pure selection shared by prediction and authority. Never expires tokens or performs a payment.</summary>
public static class ActionResolver
{
    public static ActionSelection Select(CombatSnapshot snapshot)
    {
        var s = snapshot;
        var rejected = ImmutableArray.CreateBuilder<CandidateRejection>();
        if (s.CommonFailure is { } failure) return Failed(failure);
        if (!s.LifePayment && s.Focus < s.Skill.FocusCost * 1000) return Failed("Not enough Focus");
        if (!float.IsFinite(s.World.Origin.X) || !float.IsFinite(s.World.Origin.Y)) return Failed("Invalid world origin");
        foreach (var binding in s.Behavior.Bindings)
        {
            var p = binding.Pattern;
            if (p.Family != s.Skill.Family) continue;
            var tokens = p.RequiredMemories.Select(m => m switch { MemoryType.Momentum=>s.Momentum,MemoryType.Echo=>s.Echo,MemoryType.Stillness=>s.Stillness,_=>s.Rupture }).ToArray();
            if (tokens.Any(t => t is null || t.ExpiresAt <= s.Tick)) continue;
            string? reason = null;
            if ((s.Skill.Capabilities & p.RequiredCapabilities) != p.RequiredCapabilities) reason = "Skill lacks required capabilities";
            var focus = (s.Skill.FocusCost + p.ExtraFocusCost) * 1000;
            if (reason is null && !s.LifePayment && focus > s.Focus) reason = "Not enough Focus for pattern";
            if (reason is null && p.StrainCost * 1000 > s.MaximumStrain - s.Strain) reason = "Not enough Strain capacity";
            var preflight = s.World.For(binding.Implementation);
            if (reason is null && !preflight.Allowed) reason = preflight.Reason;
            if (reason is null && preflight.Destination is { } destination && (!float.IsFinite(destination.X) || !float.IsFinite(destination.Y))) reason = "Invalid movement destination";
            if (reason is not null) { rejected.Add(new(p.Id, reason)); continue; }
            return new(true, p.Id, binding.Implementation, focus, p.StrainCost * 1000, s.Skill.Cooldown,
                tokens.Select(t => t!).ToImmutableArray(), rejected.ToImmutable(), "Ready");
        }
        var baseline = s.World.For(ActionImplementation.Base);
        if (!baseline.Allowed) return Failed(baseline.Reason);
        return new(true, s.Skill.ContentId, ActionImplementation.Base, s.Skill.FocusCost * 1000, 0,
            s.Skill.Cooldown, [], rejected.ToImmutable(), "Ready");

        ActionSelection Failed(string reason) => new(false, s.Skill.ContentId, ActionImplementation.Base, 0, 0, 0, [], rejected.ToImmutable(), reason);
    }
}

/// <summary>Read-only authority-issued proposal. The executor never accepts caller-edited costs or token IDs.</summary>
public sealed class ActionPlan
{
    internal object Owner { get; }
    internal CombatSnapshot Snapshot { get; }
    public ActionIntent Intent { get; }
    public ActionSelection Selection { get; }
    public long SnapshotRevision => Snapshot.Revision;
    public long WorldRevision => Snapshot.World.Revision;
    public Vector2 Origin => Snapshot.World.Origin;
    internal ActionPlan(object owner, ActionIntent intent, CombatSnapshot snapshot)
    { Owner = owner; Intent = intent; Snapshot = snapshot; Selection = ActionResolver.Select(snapshot); }
}
