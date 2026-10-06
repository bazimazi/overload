using System.Collections.Immutable;
using System.Numerics;

namespace Overload.Domain;

public enum AttackGeometry { Sector, Projectile, Lane }
public sealed record AttackRecipe(AttackGeometry Geometry, float Reach, float ArcDegrees, float HalfWidth, int MaxVictims, int Stagger, float Push);

/// <summary>Authored v1 adapters. Distances are world pixels (32 pixels per meter).</summary>
public static class PatternExecution
{
    public const float PursuitDistance = 64;
    public const float CrossingDistance = 160;
    public const int AfterstrikeDelay = 21;
    public static bool Supports(SkillDefinition skill, ActionImplementation implementation) => implementation switch
    {
        ActionImplementation.Pursuit or ActionImplementation.Afterstrike or ActionImplementation.Convergence or ActionImplementation.Focused or ActionImplementation.Shatter or ActionImplementation.Cascade => skill.Family == ActionFamily.Assault && skill.Capabilities.HasFlag(ActionCapabilities.DirectDamage),
        ActionImplementation.Crossing => skill.Id == SkillId.Traverse,
        ActionImplementation.Shelter => skill.Id == SkillId.Traverse,
        ActionImplementation.Reprieve => skill.Id == SkillId.Flask,
        ActionImplementation.Base => true,
        _ => false
    };
    public static AttackRecipe Recipe(SkillDefinition skill, ActionImplementation implementation)
    {
        if (skill.Family != ActionFamily.Assault || !Supports(skill, implementation)) throw new ArgumentException("No authored attack adapter");
        var geometry = implementation == ActionImplementation.Pursuit ? AttackGeometry.Lane
            : implementation == ActionImplementation.Convergence ? AttackGeometry.Sector
            : skill.ProjectileSpeed > 0 ? AttackGeometry.Projectile : skill.Id is SkillId.Faultline or SkillId.ThreadCut or SkillId.GraveLine ? AttackGeometry.Lane : AttackGeometry.Sector;
        var afterstrike = implementation == ActionImplementation.Afterstrike;
        if(implementation==ActionImplementation.Focused)return new(AttackGeometry.Lane,skill.Reach,0,6,1,skill.Stagger,0);
        if(implementation==ActionImplementation.Shatter)return new(AttackGeometry.Sector,80,140,0,64,skill.Stagger*2,16);
        if(implementation==ActionImplementation.Cascade)return new(AttackGeometry.Sector,96,160,0,64,skill.Stagger,0);
        return new(geometry, implementation == ActionImplementation.Convergence ? 96 : skill.Reach,
            implementation == ActionImplementation.Convergence ? 110 : skill.ArcDegrees, skill.Id is SkillId.Faultline or SkillId.ThreadCut or SkillId.GraveLine ? 8 + skill.ProjectileRadius : 8, skill.ProjectileSpeed > 0 && implementation != ActionImplementation.Convergence ? skill.MaxVictims : 64,
            afterstrike ? skill.Stagger * 60 / 100 : skill.Stagger,
            implementation is ActionImplementation.Pursuit or ActionImplementation.Convergence ? 0 : afterstrike ? skill.Push * 0.6f : skill.Push);
    }
    public static (BigInteger First, BigInteger Repeat) Damage(BigInteger rolledBudget, ActionImplementation implementation)
    {
        if (rolledBudget < 0) throw new ArgumentOutOfRangeException(nameof(rolledBudget));
        return implementation switch
        {
            ActionImplementation.Base => (rolledBudget, 0),
            ActionImplementation.Pursuit => (rolledBudget * 110 / 100, 0),
            ActionImplementation.Convergence => (rolledBudget * 135 / 100, 0),
            ActionImplementation.Focused => (rolledBudget * 125 / 100,0),
            ActionImplementation.Shatter => (rolledBudget * 75 / 100,0),
            ActionImplementation.Cascade => (rolledBudget * 160 / 100,0),
            // Preserve the entire 120% budget even when subunits do not divide evenly.
            ActionImplementation.Afterstrike => (rolledBudget * 60 / 100, rolledBudget * 120 / 100 - rolledBudget * 60 / 100),
            _ => throw new ArgumentException("No authored damage adapter", nameof(implementation))
        };
    }
}

public readonly record struct EffectProvenance(long RootActionId, long EffectId, long? ParentEffectId, SourceKind Source, int Depth);

/// <summary>A live root owns its bounded identity ledger; it is released with its final effect.</summary>
public sealed class EffectRoot(long actionId, SourceKind source)
{
    public const int MaximumChildren = 64;
    public const int MaximumDepth = 4;
    private readonly Dictionary<long, EffectProvenance> children = [];
    public EffectProvenance Primary { get; } = new(actionId, 0, null, source, 0);
    public bool TryChild(EffectProvenance parent, out EffectProvenance child)
    {
        child = default;
        if (children.Count >= MaximumChildren || parent.Depth >= MaximumDepth
            || (parent.EffectId == 0 ? parent != Primary : !children.TryGetValue(parent.EffectId, out var known) || parent != known)) return false;
        child = new(actionId, children.Count + 1, parent.EffectId, source, parent.Depth + 1);
        children.Add(child.EffectId, child);
        return true;
    }
}

/// <summary>Stable, bounded delayed delivery. Newly enqueued work is never resolved recursively by TakeDue.</summary>
public sealed class EffectTimeline<T>(int capacity = 256)
{
    private readonly PriorityQueue<T, (long Tick, long Sequence)> pending = new();
    private long nextSequence;
    public int Count => pending.Count;
    public IEnumerable<T> Values => pending.UnorderedItems.Select(item => item.Element);
    public bool TryEnqueue(long tick, T value)
    {
        if (pending.Count >= capacity) return false;
        pending.Enqueue(value, (tick, nextSequence++)); return true;
    }
    public ImmutableArray<T> TakeDue(long tick)
    {
        var result = ImmutableArray.CreateBuilder<T>();
        while (pending.TryPeek(out _, out var priority) && priority.Tick <= tick) result.Add(pending.Dequeue());
        return result.ToImmutable();
    }
    public void Clear() { pending.Clear(); nextSequence = 0; }
}
