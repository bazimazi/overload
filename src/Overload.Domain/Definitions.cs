namespace Overload.Domain;

public enum SkillId { Cleave, ShieldPulse, ChainLance, Traverse, Flask, Faultline, IronSweep, GuardBolt, SunderingBlow, Bulwark,
    Needle, EmberWell, Tether, StormLoom, FrostFan, ThreadCut, CinderOrb, RecallThread,
    ShardShot, Reap, EchoOrder, Veil, BoneVolley, GraveLine, SoulLance, DuskRing }
public enum FrameId { Warden, Threadseer, Revenant }
public enum EnemyStyle { Standard, Skirmisher, Fan, Sentinel, Mark, Procession, Mirror, FirstPattern }
public enum ActionFamily { Assault, Traverse, Recover }
public enum ActionPhase { Windup, Active, Recovery }
public enum SourceKind { BasePlayerAction, OverloadedPlayerAction, EnemyAction, Environment, SecondaryEffect }
public enum EnemyRole { Pursuer, Caster, Brute, Bellkeeper }
[Flags]
public enum ActionCapabilities { None = 0, DirectDamage = 1, AimOrigin = 2, GroundAdvance = 4, PathTraversal = 8 }

public sealed record SkillDefinition(
    SkillId Id, string ContentId, ActionFamily Family, int Windup, int Active, int Recovery,
    int Cooldown, int FocusCost, int Damage, float Reach, float ArcDegrees, int Stagger, float Push,
    float ProjectileSpeed = 0, float ProjectileRadius = 0, int MaxVictims = 1, ActionCapabilities Capabilities = ActionCapabilities.None,
    int BarrierPercent = 0, int EffectDelay = 0, int EffectPulses = 1, int EffectInterval = 0, float EffectRadius = 0, int SlowTicks = 0)
{
    public int Duration => Windup + Active + Recovery;
}

public sealed record HeroDefinition(int Life, int Focus, int FocusPerSecond, int Armor, int Resistance,
    float Speed, int FlaskCharges, int FlaskPercent, int FlaskTicks, int EvadeTicks, int EvadeWindow,
    int EvadeCooldown, float EvadeDistance, int CriticalPercent, int CriticalMultiplierPercent);

public sealed record EnemyDefinition(EnemyRole Role, string ContentId, int Life, int Damage, float Speed,
    float Reach, float ArcDegrees, int Windup, int Recovery, int StaggerThreshold, int StunTicks,
    int StaggerImmunityTicks, float ProjectileSpeed, float ProjectileRadius, EnemyStyle Style = EnemyStyle.Standard,
    int VolleyCount = 1, string? Name = null, string? Portrait = null);

public sealed record BalanceProfile(int SchemaVersion, string Id, HeroDefinition Hero, SkillDefinition[] Skills, EnemyDefinition[] Enemies, MemoryDefinition Memories, OverloadDefinition Overload);

public readonly record struct CombatEvent(long RootActionId, long EffectId, SourceKind Source, int TargetId, string Kind, long? ParentEffectId = null);
