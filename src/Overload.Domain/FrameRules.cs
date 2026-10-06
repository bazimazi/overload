using System.Collections.Immutable;

namespace Overload.Domain;

public static class FrameRules
{
    public static ImmutableArray<SkillId> Skills(FrameId frame) => frame switch
    {
        FrameId.Warden => [SkillId.Cleave, SkillId.ShieldPulse, SkillId.ChainLance, SkillId.Faultline, SkillId.IronSweep, SkillId.GuardBolt, SkillId.SunderingBlow, SkillId.Bulwark],
        FrameId.Threadseer => [SkillId.Needle, SkillId.EmberWell, SkillId.Tether, SkillId.StormLoom, SkillId.FrostFan, SkillId.ThreadCut, SkillId.CinderOrb, SkillId.RecallThread],
        FrameId.Revenant => [SkillId.ShardShot, SkillId.Reap, SkillId.EchoOrder, SkillId.Veil, SkillId.BoneVolley, SkillId.GraveLine, SkillId.SoulLance, SkillId.DuskRing],
        _ => throw new InvalidDataException("Unknown Frame")
    };
    public static SkillId Basic(FrameId frame) => Skills(frame)[0];
    public static CharacterState Create(FrameId frame, bool standard = true) => (standard ? EndgameRules.Standard() : new CharacterState()) with
    { Frame = frame, RegionalCampaign = true, EquippedSkills = [.. Skills(frame).Skip(1).Take(3)] };
    public static BalanceProfile Profile(BalanceProfile basis, FrameId frame)
    {
        var ids = Skills(frame);
        var hero = frame switch
        {
            FrameId.Threadseer => basis.Hero with { Life = 180, Armor = 25, Resistance = 35 },
            FrameId.Revenant => basis.Hero with { Life = 190, Armor = 30, Speed = 140 },
            _ => basis.Hero
        };
        return basis with { Hero = hero, Skills = [.. basis.Skills.Where(s => ids.Contains(s.Id) || s.Id is SkillId.Traverse or SkillId.Flask)] };
    }
    public static string Branch(FrameId frame, string branch) => frame == FrameId.Warden ? branch : $"{frame.ToString().ToLowerInvariant()}-{branch}";
    public static ImmutableArray<string> Talents(FrameId frame) => [.. new[] { "force", "ward", "flow" }.SelectMany(b => Enumerable.Range(1,4).Select(i => $"{Branch(frame,b)}.{i}"))];
    // These are complete, equal-budget fixtures, usable as training loadouts without item expansion.
    public static CharacterState BuildFixture(FrameId frame, int build)
    {
        var ids = Skills(frame);
        var slots = build switch { 0 => new[]{1,2,3}, 1 => new[]{4,5,6}, 2 => new[]{1,6,7}, _ => throw new ArgumentOutOfRangeException(nameof(build)) };
        var s = Create(frame, false) with { TotalXp = Progression.TotalXp(40), ValidatedLevel = 40 };
        var active = slots.Select(i => ids[i]).ToImmutableArray();
        return s with { EquippedSkills = active, SkillRanks = active.Append(ids[0]).ToImmutableDictionary(i => i, _ => 4),
            Techniques = active.ToImmutableDictionary(i => i, _ => build == 1 ? Technique.Reach : Technique.Wide),
            Talents = [.. new[]{"force","ward","flow"}.Where((_,i) => i != build).SelectMany(b => Enumerable.Range(1,4).Select(i => $"{Branch(frame,b)}.{i}"))] };
    }
    public static string SkillDescription(SkillId id) => id switch
    {
        SkillId.EmberWell => "Place a well along aim; erupts after 0.6s. Fixed ground, one hit per victim.",
        SkillId.StormLoom => "Place a loom; three pulses over 2s. Secondary pulses cannot create memories.",
        SkillId.Tether => "Piercing tether slows ordinary foes for 2s; bosses instead gain extra stagger.",
        SkillId.BoneVolley => "Three spread bolts share a victim ledger; each enemy can take this base action only once.",
        SkillId.EchoOrder => "Command one echo at a clear point along aim; fires three bolts. Replaces the previous command.",
        SkillId.Veil or SkillId.Bulwark or SkillId.RecallThread => "Raise a temporary barrier; no damage, healing, or evasion. Direct Assault patterns cannot apply.",
        _ => "Direct Assault; compatible shared signatures replace its geometry."
    };
}
