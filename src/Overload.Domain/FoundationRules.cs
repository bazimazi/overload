using System.Collections.Immutable;
using System.Numerics;

namespace Overload.Domain;
public enum Technique { Wide, Reach }
public static class FoundationRules
{
    public static readonly ImmutableArray<string> TalentIds = [.. new[] { "force", "ward", "flow" }.SelectMany(branch => Enumerable.Range(1, 4).Select(i => $"{branch}.{i}"))];
    public static readonly ImmutableArray<string> InscriptionIds = ["field-notes", "salvagers-mark", "quiet-rest", "long-sip"];
    public static int SkillBudget(CharacterState s) => (int)BigInteger.Min(20, s.ValidatedLevel / 2) + s.SkillMilestones.Count;
    public static int TalentBudget(CharacterState s) => (int)BigInteger.Min(30, s.ValidatedLevel / 2);
    public static int SkillSpent(CharacterState s) => s.SkillRanks.Values.Sum() + s.Techniques.Count;
    public static BigInteger UnspentResonance(CharacterState s) => Progression.Resonance(s.ValidatedLevel) - s.Might - s.Resolve;
    public static CharacterState AddXp(CharacterState s, BigInteger amount)
    {
        if (amount < 0) throw new InvalidOperationException("XP awards cannot be negative");
        var xp = s.TotalXp + amount; var level = Progression.LevelAt(xp); var points = Progression.Resonance(level);
        var might = s.ResonancePolicy switch { ResonancePolicy.Offense => (points * 3 + 3) / 4, ResonancePolicy.Defense => points / 4, _ => (points + 1) / 2 };
        return s with { TotalXp = xp, ValidatedLevel = level, Might = s.AutoResonance ? might : s.Might, Resolve = s.AutoResonance ? points - might : s.Resolve };
    }
    public static CharacterState Rank(CharacterState s, SkillId skill)
    {
        var rank = s.SkillRanks.GetValueOrDefault(skill);
        if (!FrameRules.Skills(s.Frame).Contains(skill) || rank >= 5 || SkillSpent(s) >= SkillBudget(s)) throw new InvalidOperationException("No skill points or maximum rank reached");
        return s with { SkillRanks = s.SkillRanks.SetItem(skill, rank + 1) };
    }
    public static CharacterState ChooseTechnique(CharacterState s, SkillId skill, Technique technique)
    {
        if (!Enum.IsDefined(technique) || s.SkillRanks.GetValueOrDefault(skill) < 3 || !s.Techniques.ContainsKey(skill) && SkillSpent(s) >= SkillBudget(s)) throw new InvalidOperationException("Technique needs rank 3 and one skill point");
        return s with { Techniques = s.Techniques.SetItem(skill, technique) };
    }
    public static CharacterState Talent(CharacterState s, string id)
    {
        if (!FrameRules.Talents(s.Frame).Contains(id) || s.Talents.Contains(id) || s.Talents.Count >= TalentBudget(s)) throw new InvalidOperationException("No available talent point");
        var parts = id.Split('.'); var rank = int.Parse(parts[1]);
        if (rank > 1 && !s.Talents.Contains($"{parts[0]}.{rank - 1}")) throw new InvalidOperationException("Requires the previous branch node");
        if (rank == 4 && s.Talents.Count(t => t.EndsWith(".4", StringComparison.Ordinal)) >= 2) throw new InvalidOperationException("At most two tradeoff talents");
        return s with { Talents = s.Talents.Add(id) };
    }
    public static CharacterState Inscription(CharacterState s, string id)
    {
        if (!InscriptionIds.Contains(id)) throw new InvalidOperationException("Unknown inscription");
        if (s.Inscriptions.Contains(id)) return s with { Inscriptions = s.Inscriptions.Remove(id) };
        if (s.Inscriptions.Count >= 2) throw new InvalidOperationException("Equip at most two inscriptions");
        return s with { Inscriptions = s.Inscriptions.Add(id) };
    }
    public static CharacterState Allocate(CharacterState s, bool might, BigInteger amount)
    {
        if (amount <= 0 || amount > UnspentResonance(s)) throw new InvalidOperationException("Not enough unspent Resonance");
        return s with { AutoResonance = false, Might = s.Might + (might ? amount : 0), Resolve = s.Resolve + (might ? 0 : amount) };
    }
    public static CharacterState Respec(CharacterState s) => s with { SkillRanks = ImmutableDictionary<SkillId, int>.Empty,
        Techniques = ImmutableDictionary<SkillId, Technique>.Empty, Talents = [], Might = 0, Resolve = 0, AutoResonance = false };
    public static void Validate(CharacterState s)
    {
        if (s.SkillRanks is null || s.Techniques is null || s.Talents is null || s.Inscriptions is null || s.SkillMilestones is null
            || s.SkillMilestones.Any(i => i is < 0 or > 3) || s.SkillRanks.Any(p => !FrameRules.Skills(s.Frame).Contains(p.Key) || p.Value is < 0 or > 5)
            || SkillSpent(s) > SkillBudget(s) || s.Techniques.Any(p => !Enum.IsDefined(p.Value) || s.SkillRanks.GetValueOrDefault(p.Key) < 3)
            || s.Talents.Count > TalentBudget(s) || s.Talents.Any(t => !FrameRules.Talents(s.Frame).Contains(t)) || s.Talents.Count(t => t.EndsWith(".4", StringComparison.Ordinal)) > 2
            || s.Inscriptions.Count > 2 || s.Inscriptions.Any(i => !InscriptionIds.Contains(i))) throw new InvalidDataException("Invalid foundation allocation");
        foreach (var talent in s.Talents)
        {
            var parts = talent.Split('.'); var rank = int.Parse(parts[1]);
            if (rank > 1 && !s.Talents.Contains($"{parts[0]}.{rank - 1}")) throw new InvalidDataException("Broken talent prerequisite");
        }
    }
    public static BalanceProfile Build(BalanceProfile basis, CharacterState s)
    {
        Validate(s); EquipmentRules.Validate(s);
        basis = FrameRules.Profile(basis, s.Frame);
        int Has(string id) { var parts=id.Split('.'); return s.Talents.Contains($"{FrameRules.Branch(s.Frame,parts[0])}.{parts[1]}") ? 1 : 0; }
        var damagePercent = 100 + Has("force.1") * 5 + Has("force.4") * 15 - Has("ward.4") * 10;
        var lifePercent = 100 + Has("ward.1") * 5 + Has("ward.4") * 20 - Has("force.4") * 10 - Has("flow.4") * 5;
        var hero = basis.Hero with
        {
            Life = (basis.Hero.Life + EquipmentRules.Stat(s, AffixKind.Life)) * lifePercent / 100 * (AdventureRules.HasPower(s,"guard") ? 112 : 100) / 100,
            Armor = basis.Hero.Armor + EquipmentRules.Stat(s, AffixKind.Armor) + Has("ward.2") * 10,
            Resistance = basis.Hero.Resistance + EquipmentRules.Stat(s, AffixKind.Resistance) + Has("ward.3") * 5,
            FocusPerSecond = basis.Hero.FocusPerSecond + EquipmentRules.Stat(s, AffixKind.FocusRegeneration) + Has("flow.1") * 2 + Has("flow.4") * 3 + (AdventureRules.HasPower(s,"flow") ? 4 : 0),
            Speed = basis.Hero.Speed * (AdventureRules.HasPower(s,"fleet") ? 1.06f : 1f),
            Focus = basis.Hero.Focus + Has("flow.3") * 5,
            CriticalPercent = Math.Min(40, basis.Hero.CriticalPercent + EquipmentRules.Stat(s, AffixKind.CriticalChance)),
            FlaskPercent = basis.Hero.FlaskPercent + (s.Inscriptions.Contains("long-sip") ? 2 : 0)
        };
        var skills = basis.Skills.Select(skill =>
        {
            if (skill.Family != ActionFamily.Assault) return skill;
            var rank = s.SkillRanks.GetValueOrDefault(skill.Id); var attack = EquipmentRules.Stat(s, AffixKind.Attack);
            if(skill.BarrierPercent>0)return skill with { BarrierPercent=Math.Min(20,skill.BarrierPercent+rank+(s.Techniques.TryGetValue(skill.Id,out var t)&&t==Technique.Wide?2:0)),
                Active=skill.Active, Recovery=Math.Max(6,skill.Recovery-Has("flow.2")) };
            if(skill.Damage==0)return skill;
            var coefficient = skill.Id == SkillId.ChainLance ? 80 : skill.Id == SkillId.ShieldPulse ? 25 : 40;
            var result = skill with { Damage = (skill.Damage + attack * coefficient / 100 + rank * (skill.Id == SkillId.ChainLance ? 8 : 4)) * damagePercent / 100,
                Reach = skill.Reach + Has("force.3") * 4, Stagger = skill.Stagger + Has("force.2") * 5, Recovery = Math.Max(6, skill.Recovery - Has("flow.2")) };
            if (s.Techniques.TryGetValue(skill.Id, out var technique)) result = technique == Technique.Reach ? result with { Reach = result.Reach + 16 }
                : result with { ArcDegrees = Math.Min(180, result.ArcDegrees + 20), ProjectileRadius = result.ProjectileRadius + 2,EffectRadius=result.EffectRadius>0?result.EffectRadius+8:0 };
            return result;
        }).ToArray();
        if(AdventureRules.HasPower(s,"tempo"))skills=skills.Select(k=>k.Family==ActionFamily.Assault && k.Id!=FrameRules.Basic(s.Frame) ? k with {Cooldown=Math.Max(1,k.Cooldown*90/100)} : k).ToArray();
        if(AdventureRules.HasPower(s,"reaving"))skills=skills.Select(k=>k.Id==FrameRules.Basic(s.Frame)?k.ProjectileSpeed>0?k with {MaxVictims=k.MaxVictims+2,ProjectileRadius=k.ProjectileRadius+3}:k with {Reach=k.Reach+18,ArcDegrees=Math.Min(180,k.ArcDegrees+60)}:k).ToArray();
        return basis with { Hero = hero, Skills = skills,Overload=basis.Overload with { BindingSlots=EndgameRules.BindingSlots(s),Bindings=s.Bindings } };
    }
}
