using System.Numerics;

namespace Overload.Domain;

public sealed class EnemyCombat(EnemyDefinition definition, BigInteger? tier = null)
{
    public EnemyDefinition Definition { get; } = definition;
    public BigInteger MaximumLife { get; } = Progression.TierScaled(CombatMath.Points(definition.Life), tier ?? 1);
    public BigInteger Life { get; private set; } = Progression.TierScaled(CombatMath.Points(definition.Life), tier ?? 1);
    public BigInteger Damage { get; } = Progression.TierScaled(CombatMath.Points(definition.Damage), tier ?? 1);
    public int Stagger { get; private set; }
    public long StunnedUntil { get; private set; }
    public long ImmuneUntil { get; private set; }
    public bool Dead => Life.IsZero;
    public bool Enraged => Definition.Role == EnemyRole.Bellkeeper && Life * 2 <= MaximumLife;
    public bool ReceiveHit(BigInteger damage, int stagger, long tick)
    {
        if (damage < 0 || stagger < 0) throw new ArgumentOutOfRangeException();
        if (Dead) return false;
        Life = BigInteger.Max(0, Life - damage);
        if (tick < ImmuneUntil) return false;
        Stagger += stagger;
        if (Stagger < Definition.StaggerThreshold) return false;
        Stagger = 0;
        StunnedUntil = tick + Definition.StunTicks;
        ImmuneUntil = StunnedUntil + Definition.StaggerImmunityTicks;
        return true;
    }
}
