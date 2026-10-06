using System.Numerics;

namespace Overload.Domain;

public enum MemoryType { Momentum, Echo, Stillness, Rupture }
public enum LocomotionKind { Walk, StandardEvade, Teleport, Knockback, OverloadedAction }
public enum MemoryClearReason { Checkpoint, Death, EncounterExit, EquipmentChanged, BuildChanged }

public sealed record MemoryDefinition(float MomentumDistancePixels, int MomentumWindowTicks, int LifetimeTicks, int GenerationCooldownTicks);
public sealed record MemoryToken(long Id, MemoryType Type, long GeneratedAt, long ExpiresAt, Vector2 Direction, long? HostileAttackId = null,Vector2 Origin=default,int? TargetId=null);

/// <summary>All hit windows/children of a hostile root share its ID and final possible hit tick (exclusive).</summary>
public readonly record struct HostileAttackEvidence(long RootActionId, long ExpiresAt, Vector2 IncomingDirection, SourceKind Source = SourceKind.EnemyAction);

/// <summary>Tick-driven temporary memories. Geometry evidence comes from the world adapter, never intended input.</summary>
public sealed class CombatMemories(MemoryDefinition definition)
{
    // Float physics positions can end a 96-pixel evade at 95.99991 pixels.
    // This geometric tolerance is 1/32,000 meter; it never credits zero movement.
    private const double DistanceTolerancePixels = 0.001;
    private readonly Queue<(long Tick, double Distance)> movement = new();
    private readonly Dictionary<long, long> echoAwards = [];
    private long nextTokenId;
    private long lastMovementTick = -1;
    private long momentumReadyAt;
    private long echoReadyAt;
    private long stillnessReadyAt;
    private long ruptureReadyAt;
    private Vector2? stillOrigin;
    private long stillSince;
    private readonly Dictionary<int,long> ruptureLocks=[];
    public long Tick { get; private set; }
    public long Revision { get; private set; }
    public bool InCombat { get; private set; }
    public double MomentumDistance { get; private set; }
    public MemoryToken? Momentum { get; private set; }
    public MemoryToken? Echo { get; private set; }
    public MemoryToken? Stillness { get; private set; }
    public MemoryToken? Rupture { get; private set; }
    public MemoryClearReason LastClearReason { get; private set; } = MemoryClearReason.Checkpoint;
    public int TrackedEchoAttackCount => echoAwards.Count;

    public MemoryToken? Get(MemoryType type) => type switch { MemoryType.Momentum=>Momentum,MemoryType.Echo=>Echo,MemoryType.Stillness=>Stillness,MemoryType.Rupture=>Rupture,_=>null };
    public int RemainingTicks(MemoryType type) => Get(type) is { } token ? (int)Math.Max(0, token.ExpiresAt - Tick) : 0;
    public int CooldownTicks(MemoryType type) => (int)Math.Max(0, (type switch { MemoryType.Momentum=>momentumReadyAt,MemoryType.Echo=>echoReadyAt,MemoryType.Stillness=>stillnessReadyAt,_=>ruptureReadyAt }) - Tick);

    public void AdvanceTo(long tick)
    {
        if (tick < Tick) throw new ArgumentOutOfRangeException(nameof(tick), "Use Reset for a checkpoint clock reset.");
        if (tick != Tick) Revision++;
        Tick = tick;
        if (Momentum?.ExpiresAt <= Tick) Momentum = null;
        if (Echo?.ExpiresAt <= Tick) Echo = null;
        if (Stillness?.ExpiresAt <= Tick) Stillness = null;
        if (Rupture?.ExpiresAt <= Tick) Rupture = null;
        foreach(var id in ruptureLocks.Where(p=>p.Value<=Tick).Select(p=>p.Key).ToArray())ruptureLocks.Remove(id);
        while (movement.TryPeek(out var sample) && sample.Tick <= Tick - definition.MomentumWindowTicks)
        {
            movement.Dequeue(); MomentumDistance = Math.Max(0, MomentumDistance - sample.Distance);
        }
        // Retire receipts only after their root can no longer hit. Old evidence is rejected below.
        foreach (var id in echoAwards.Where(pair => pair.Value <= Tick).Select(pair => pair.Key).ToArray()) echoAwards.Remove(id);
    }
    public void SetCombatActive(bool active)
    {
        if (InCombat != active) Revision++;
        if (InCombat && !active) Clear(MemoryClearReason.EncounterExit);
        InCombat = active;
    }
    public bool ObserveLocomotion(Vector2 displacement, LocomotionKind kind, SourceKind rootSource = SourceKind.BasePlayerAction)
    {
        if (!float.IsFinite(displacement.X) || !float.IsFinite(displacement.Y)) throw new ArgumentOutOfRangeException(nameof(displacement));
        if (!InCombat || rootSource != SourceKind.BasePlayerAction || kind is not (LocomotionKind.Walk or LocomotionKind.StandardEvade)) return false;
        if (lastMovementTick == Tick) return false;
        Revision++;
        lastMovementTick = Tick;
        var distance = displacement.Length();
        if (!float.IsFinite(distance)) throw new ArgumentOutOfRangeException(nameof(displacement));
        if (distance <= 0) return false;
        movement.Enqueue((Tick, distance)); MomentumDistance += distance;
        if (MomentumDistance + DistanceTolerancePixels < definition.MomentumDistancePixels || Tick < momentumReadyAt) return false;
        Momentum = new(++nextTokenId, MemoryType.Momentum, Tick, Tick + definition.LifetimeTicks, Vector2.Normalize(displacement));
        momentumReadyAt = Tick + definition.GenerationCooldownTicks;
        movement.Clear(); MomentumDistance = 0;
        return true;
    }
    /// <summary>Call only for collision evidence rejected specifically by a base-action evasion window.</summary>
    public bool ObserveEvadedHit(HostileAttackEvidence hit, SourceKind traversalSource)
    {
        if (!InCombat || traversalSource != SourceKind.BasePlayerAction || hit.Source != SourceKind.EnemyAction
            || hit.RootActionId <= 0 || hit.ExpiresAt <= Tick || Tick < echoReadyAt) return false;
        if (!float.IsFinite(hit.IncomingDirection.X) || !float.IsFinite(hit.IncomingDirection.Y) || !float.IsFinite(hit.IncomingDirection.LengthSquared())) return false;
        if (echoAwards.TryGetValue(hit.RootActionId, out var expiry))
        {
            Revision++;
            echoAwards[hit.RootActionId] = Math.Max(expiry, hit.ExpiresAt);
            return false;
        }
        var direction = hit.IncomingDirection.LengthSquared() > 0 ? Vector2.Normalize(hit.IncomingDirection) : Vector2.Zero;
        Revision++;
        Echo = new(++nextTokenId, MemoryType.Echo, Tick, Tick + definition.LifetimeTicks, direction, hit.RootActionId);
        echoReadyAt = Tick + definition.GenerationCooldownTicks;
        echoAwards.Add(hit.RootActionId, hit.ExpiresAt);
        return true;
    }
    public void Clear(MemoryClearReason reason)
    {
        Revision++;
        Momentum = null; Echo = null; movement.Clear(); MomentumDistance = 0; echoAwards.Clear();
        momentumReadyAt = 0; echoReadyAt = 0; lastMovementTick = -1; LastClearReason = reason;
        Stillness=null;Rupture=null;stillOrigin=null;stillSince=Tick;stillnessReadyAt=0;ruptureReadyAt=0;ruptureLocks.Clear();
    }
    public void ObservePosition(Vector2 position)
    {
        if(!float.IsFinite(position.X)||!float.IsFinite(position.Y))throw new ArgumentOutOfRangeException(nameof(position));
        if(!InCombat){stillOrigin=null;return;}
        if(stillOrigin is null || Vector2.DistanceSquared(stillOrigin.Value,position)>64) { stillOrigin=position;stillSince=Tick; }
    }
    public bool ObserveBaseAssaultHit(Vector2 position,Vector2 aim,int target,bool staggerBreak,SourceKind source,Vector2? hitOrigin=null,Vector2? targetPosition=null)
    {
        if(!InCombat || source!=SourceKind.BasePlayerAction || target<0 || !float.IsFinite(aim.X)||!float.IsFinite(aim.Y))return false;
        ObservePosition(position);bool generated=false;
        if(Tick-stillSince>=48 && Tick>=stillnessReadyAt)
        {
            Stillness=new(++nextTokenId,MemoryType.Stillness,Tick,Tick+definition.LifetimeTicks,aim,Origin:hitOrigin??position);
            stillnessReadyAt=Tick+definition.GenerationCooldownTicks;generated=true;
        }
        if(staggerBreak && Tick>=ruptureReadyAt && !ruptureLocks.ContainsKey(target))
        {
            Rupture=new(++nextTokenId,MemoryType.Rupture,Tick,Tick+definition.LifetimeTicks,aim,Origin:targetPosition??position,TargetId:target);
            ruptureReadyAt=Tick+definition.GenerationCooldownTicks;ruptureLocks[target]=Tick+360;generated=true;
        }
        if(generated)Revision++;return generated;
    }
    // Validate every receipt first, then remove exactly the selected tokens. Generation locks and root receipts survive spending.
    internal bool TryConsume(System.Collections.Immutable.ImmutableArray<MemoryToken> tokens)
    {
        if (tokens.Any(t => Get(t.Type) != t || t.ExpiresAt <= Tick) || tokens.Select(t => t.Type).Distinct().Count() != tokens.Length) return false;
        foreach (var token in tokens)
        {
            switch(token.Type) { case MemoryType.Momentum:Momentum=null;break;case MemoryType.Echo:Echo=null;break;case MemoryType.Stillness:Stillness=null;break;case MemoryType.Rupture:Rupture=null;break; }
        }
        if (tokens.Length > 0) Revision++;
        return true;
    }
    public void Reset()
    {
        Clear(MemoryClearReason.Checkpoint); Tick = 0; InCombat = false;
    }
}
