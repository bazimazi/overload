using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Overload.Domain;

namespace Overload.Content;
public sealed record ExpansionCatalog(string Version, ImmutableArray<SkillDefinition> Skills, ImmutableArray<EnemyDefinition> Enemies, ImmutableArray<RegionalRoom> Rooms);
public static class ExpansionLoader
{
    public static BalanceProfile Load(BalanceProfile basis, string json)
    {
        var options=new JsonSerializerOptions { PropertyNameCaseInsensitive=true,UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow, Converters={new JsonStringEnumConverter(allowIntegerValues:false)} };
        var c=JsonSerializer.Deserialize<ExpansionCatalog>(json,options) ?? throw new InvalidDataException("Missing expansion");
        if(c.Version!="expansion.v1"||c.Skills.IsDefault||c.Enemies.IsDefault||c.Rooms.IsDefault)throw new InvalidDataException("Unsupported expansion");
        var all=basis.Skills.Concat(c.Skills).ToArray();
        if(all.Length!=26||all.Select(s=>s.Id).Distinct().Count()!=26||all.Select(s=>s.ContentId).Distinct().Count()!=26||c.Enemies.Length!=32||c.Rooms.Length!=48
            || c.Enemies.Select(e=>e.ContentId).Distinct().Count()!=32||c.Rooms.Select(r=>r.Id).Distinct().Count()!=48)throw new InvalidDataException("Expansion roster is incomplete or duplicated");
        foreach(var frame in Enum.GetValues<FrameId>())
        {
            var skills=FrameRules.Skills(frame).Select(id=>all.Single(s=>s.Id==id)).ToArray();
            foreach(var s in skills)
                if(s.Family!=ActionFamily.Assault||s.Windup<0||s.Active<1||s.Recovery<0||s.Duration>600||s.FocusCost is <0 or >100||s.Damage<0||!float.IsFinite(s.Reach)||s.Reach is <0 or >320||s.Cooldown is <0 or >3600||!float.IsFinite(s.ProjectileSpeed)||s.ProjectileSpeed is <0 or >1000||!float.IsFinite(s.ProjectileRadius)||s.ProjectileRadius is <0 or >32||s.MaxVictims is <1 or >64||s.BarrierPercent is <0 or >20||s.EffectDelay is <0 or >120||s.EffectPulses is <1 or >3||s.EffectInterval is <0 or >120||!float.IsFinite(s.EffectRadius)||s.EffectRadius is <0 or >96||s.SlowTicks is <0 or >180||s.EffectPulses>1&&s.EffectInterval<1
                    || (s.Capabilities & ~(ActionCapabilities.DirectDamage|ActionCapabilities.AimOrigin|ActionCapabilities.GroundAdvance|ActionCapabilities.PathTraversal))!=0
                    || !float.IsFinite(s.ArcDegrees)||s.ArcDegrees is <0 or >180||s.Stagger<0||!float.IsFinite(s.Push)||s.Push is <0 or >32||string.IsNullOrWhiteSpace(s.ContentId))
                    throw new InvalidDataException("Unsafe Frame skill: "+s.ContentId);
            if(skills[0].FocusCost!=0)throw new InvalidDataException("Frame basics must remain free");
        }
        foreach(var e in c.Enemies)
            if(!Enum.IsDefined(e.Role)||!Enum.IsDefined(e.Style)||e.Life<1||e.Damage<1||e.Speed is <0 or >200||e.Windup<24||e.Recovery<24||e.VolleyCount is <1 or >9||e.StaggerThreshold<1||e.ProjectileSpeed<0||string.IsNullOrWhiteSpace(e.Portrait))throw new InvalidDataException("Unsafe regional enemy");
        foreach(var region in Enum.GetValues<Region>())
        {
            var rooms=c.Rooms.Where(r=>r.Region==region).OrderBy(r=>r.Id).ToArray();
            if(rooms.Length!=12||rooms.Count(r=>r.Boss)!=2||!rooms.Select(r=>r.Id).SequenceEqual(Enumerable.Range((int)region*12,12)))throw new InvalidDataException("Incomplete regional rooms");
            foreach(var r in rooms)
            {
                if(r.Obstacles.IsDefault||r.Obstacles.Any(b=>b.Width<1||b.Height<1||b.X<48||b.X+b.Width>592||b.Y<76||b.Y+b.Height>296)||r.Boss&&!r.Obstacles.IsEmpty)throw new InvalidDataException("Unsafe authored room");
                var check=ExpeditionGenerator.Generate(0,0) with { Obstacles=[..Enumerable.Repeat(r.Obstacles,7)] };
                if(!ExpeditionGenerator.Connected(check))throw new InvalidDataException("Room blocks traversal/return sockets: "+r.Name);
            }
        }
        RegionalContent.Configure(c.Rooms,c.Enemies);
        return basis with { Skills=all };
    }
}
