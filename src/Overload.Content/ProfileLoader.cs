using System.Text.Json;
using System.Text.Json.Serialization;
using Overload.Domain;

namespace Overload.Content;

public static class ProfileLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };
    public static BalanceProfile Load(string json)
    {
        var profile = JsonSerializer.Deserialize<BalanceProfile>(json, Options) ?? throw new InvalidDataException("Empty balance profile");
        var errors = Validate(profile);
        if (errors.Count > 0) throw new InvalidDataException(string.Join(Environment.NewLine, errors));
        return profile;
    }
    public static List<string> Validate(BalanceProfile profile)
    {
        List<string> errors = [];
        if (profile.SchemaVersion != 1 || profile.Id != "balance.arena.v1") errors.Add("profile: unsupported version/id");
        errors.AddRange(ActorBehaviorProfile.Validate(profile.Overload));
        if (profile.Hero is not { } hero || profile.Skills is null || profile.Enemies is null)
        { errors.Add("profile: hero, skills, and enemies are required"); return errors; }
        if (profile.Memories is not { } memories || !float.IsFinite(memories.MomentumDistancePixels) || memories.MomentumDistancePixels is <= 0 or > 3200
            || memories.MomentumWindowTicks is < 1 or > 600 || memories.LifetimeTicks is < 1 or > 3600 || memories.GenerationCooldownTicks is < 1 or > 3600)
            errors.Add("memories: positive distance and bounded tick durations are required");
        if (hero.Life <= 0 || hero.Focus <= 0 || hero.Focus > 10000 || hero.FocusPerSecond < 0 || hero.FocusPerSecond > 1000 || hero.Armor < 0 || hero.Resistance < 0
            || !float.IsFinite(hero.Speed) || hero.Speed <= 0 || hero.Speed > 1000 || hero.FlaskCharges < 1 || hero.FlaskCharges > 9 || hero.FlaskPercent is < 1 or > 100
            || hero.FlaskTicks is < 1 or > 600 || hero.EvadeTicks is < 1 or > 120 || hero.EvadeWindow < 0 || hero.EvadeWindow > hero.EvadeTicks
            || hero.EvadeCooldown < hero.EvadeTicks || hero.EvadeDistance <= 0 || !float.IsFinite(hero.EvadeDistance)
            || hero.CriticalPercent is < 0 or > 40 || hero.CriticalMultiplierPercent is < 100 or > 200) errors.Add("hero: invalid resource, movement or timing range");
        SkillId[] arenaSkills=[SkillId.Cleave,SkillId.ShieldPulse,SkillId.ChainLance,SkillId.Traverse,SkillId.Flask,SkillId.Faultline,SkillId.IronSweep,SkillId.GuardBolt];
        if (!profile.Skills.Select(s=>s.Id).Order().SequenceEqual(arenaSkills.Order())) errors.Add("skills: exactly one definition per arena skill required");
        if (profile.Enemies.Select(e => e.Role).Distinct().Count() != 4 || profile.Enemies.Length != 4) errors.Add("enemies: exactly one definition per M1 role required");
        var ids = profile.Skills.Select(s => s.ContentId).Concat(profile.Enemies.Select(e => e.ContentId)).ToArray();
        if (ids.Any(string.IsNullOrWhiteSpace) || ids.Distinct().Count() != ids.Length) errors.Add("contentId: missing or duplicate ID");
        foreach (var s in profile.Skills)
        {
            const ActionCapabilities all = ActionCapabilities.DirectDamage | ActionCapabilities.AimOrigin | ActionCapabilities.GroundAdvance | ActionCapabilities.PathTraversal;
            if ((s.Capabilities & ~all) != 0) errors.Add($"{s.ContentId}: unknown capabilities");
            if (!Enum.IsDefined(s.Id) || !Enum.IsDefined(s.Family) || s.Windup is < 0 or > 600 || s.Active is < 1 or > 600 || s.Recovery is < 0 or > 600 || s.Duration > 600
                || s.Cooldown < 0 || s.Cooldown > 3600 || s.FocusCost < 0 || s.FocusCost > hero.Focus || s.Damage < 0
                || !float.IsFinite(s.Reach) || s.Reach < 0 || s.Reach > 640 || !float.IsFinite(s.ArcDegrees) || s.ArcDegrees < 0 || s.ArcDegrees > 180
                || s.Stagger < 0 || !float.IsFinite(s.Push) || s.Push < 0 || !float.IsFinite(s.ProjectileSpeed) || s.ProjectileSpeed < 0
                || !float.IsFinite(s.ProjectileRadius) || s.ProjectileRadius < 0 || s.MaxVictims is < 1 or > 64) errors.Add($"{s.ContentId}: invalid action geometry, cost or timing");
            var expected = s.Id == SkillId.Traverse ? ActionFamily.Traverse : s.Id == SkillId.Flask ? ActionFamily.Recover : ActionFamily.Assault;
            if (s.Family != expected) errors.Add($"{s.ContentId}: incorrect action family");
            if (s.Id == SkillId.ChainLance && (s.ProjectileSpeed <= 0 || s.ProjectileRadius <= 0 || s.Reach <= 0)) errors.Add($"{s.ContentId}: lance needs a moving projectile with positive radius and range");
            if (s.Id is SkillId.Cleave or SkillId.ShieldPulse && (s.Reach <= 0 || s.ArcDegrees <= 0)) errors.Add($"{s.ContentId}: melee sector must have positive area");
        }
        foreach (var e in profile.Enemies)
        {
            if (!Enum.IsDefined(e.Role) || e.Life <= 0 || e.Damage <= 0 || !float.IsFinite(e.Speed) || e.Speed < 0 || !float.IsFinite(e.Reach) || e.Reach <= 0
                || !float.IsFinite(e.ArcDegrees) || e.ArcDegrees <= 0 || e.ArcDegrees > 180 || e.Windup < 24 || e.Recovery < 6
                || e.StaggerThreshold < 1 || e.StunTicks < 1 || e.StaggerImmunityTicks < 0 || !float.IsFinite(e.ProjectileSpeed) || e.ProjectileSpeed < 0
                || !float.IsFinite(e.ProjectileRadius) || e.ProjectileRadius < 0) errors.Add($"{e.ContentId}: invalid enemy profile");
            if (e.Role is EnemyRole.Caster or EnemyRole.Bellkeeper && (e.ProjectileSpeed <= 0 || e.ProjectileRadius <= 0)) errors.Add($"{e.ContentId}: volley requires positive projectile speed and radius");
        }
        var traverse = profile.Skills.FirstOrDefault(s => s.Id == SkillId.Traverse);
        if (traverse is null || traverse.Duration != hero.EvadeTicks || traverse.Cooldown != hero.EvadeCooldown) errors.Add("traverse: timeline must match hero evade");
        return errors;
    }
}
