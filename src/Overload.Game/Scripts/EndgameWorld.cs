using Godot;
using Overload.Domain;
using System.Collections.Immutable;

namespace Overload.Game;
/// <summary>Finite, disclosed hazards share their drawn rectangles with damage checks.</summary>
public partial class EndgameWorld : Node2D
{
    private Arena arena=null!;
    private ImmutableArray<string> rules=[];
    private ActivityFamily activity;
    private long entry;
    private bool keystone;
    private bool cover;
    private int coverHits;
    public bool Assisted { get; private set; }
    public bool ObjectiveReady=>activity!=ActivityFamily.Vault || keystone;
    public string Hint=>activity==ActivityFamily.Vault&&!keystone?"Vault: defeat guardians, then touch the golden keystone at the exit.":activity==ActivityFamily.Breach?"Breach: break the wardens. Hazard pulses leave the central lane safe.":"Hunt: bring down the marked elite and boss.";
    public void Initialize(Arena owner)=>arena=owner;
    public void Configure(ImmutableArray<string> selected,string? mutation,ActivityFamily family,bool? assisted=null)
    { if(assisted is { } value)Assisted=value;rules=selected;activity=family;entry=arena.PlayerState.Tick;keystone=false;cover=rules.Contains("law.glass")||arena.FractureActive&&arena.Character!.State.Fracture!.Region==Region.Glass;coverHits=0;QueueRedraw(); }
    public void Clear() { rules=[];cover=false;keystone=false;QueueRedraw(); }
    public void SetAssisted(bool value) { if(!arena.Playing)Assisted=value; }
    public bool CoverCollision(Vector2 from,Vector2 to,bool friendly,bool heavy=false)
    {
        if(!cover || WorldQueries.SegmentCircle(from,to,new(336,120),18) is null)return false;
        if(friendly) { coverHits++;if(coverHits>=1) {cover=false;arena.CoverBroken();} }
        else if(heavy)cover=false;
        return true;
    }
    public void StrikeCover(Vector2 origin,Vector2 aim,AttackRecipe recipe)
    {
        if(cover&& (recipe.Geometry==AttackGeometry.Lane?WorldQueries.SegmentCircle(origin,origin+aim*recipe.Reach,new(336,120),18) is not null:WorldQueries.SectorOverlaps(origin,aim,recipe.Reach,recipe.ArcDegrees,new(336,120),18)))
        {cover=false;arena.CoverBroken();}
    }
    public void Advance()
    {
        if(!arena.Playing||arena.Paused)return;
        var tick=arena.PlayerState.Tick-entry;
        if(activity==ActivityFamily.Vault&&arena.Enemies.All(e=>e.Enemy!.Dead)&&arena.Player.Position.DistanceTo(new(560,192))<=28)keystone=true;
        foreach(var id in rules)
        {
            var rule=WorldLaws.Rules.Single(r=>r.Id==id);
            if(rule.DamagePercent==0 || tick%rule.Period!=rule.WarningTicks)continue;
            foreach(var area in WorldLaws.Areas(id,tick))if(new Rect2(area.X,area.Y,area.Width,area.Height).Grow(arena.Player.Radius).HasPoint(arena.Player.Position))
                arena.PlayerState.ReceiveHit(Progression.TierScaled(CombatMath.Points(250)*rule.DamagePercent/100,arena.EncounterTier)*(Assisted?50:100)/100,false);
        }
        QueueRedraw();
    }
    public override void _Draw()
    {
        if(arena is null||!arena.Playing)return;
        var tick=arena.PlayerState.Tick-entry;
        foreach(var id in rules)
        {
            var rule=WorldLaws.Rules.Single(r=>r.Id==id);var phase=tick%rule.Period;if(phase>rule.WarningTicks+15)continue;
            foreach(var area in WorldLaws.Areas(id,tick))
            {var rect=new Rect2(area.X,area.Y,area.Width,area.Height);DrawRect(rect,new Color(1,0.55f,0.25f,phase<rule.WarningTicks?0.18f:0.45f));DrawRect(rect,new Color("ffc178"),false,1);}
        }
        if(cover){DrawCircle(new(336,120),18,new Color("70bfd1"));DrawString(ThemeDB.FallbackFont,new(312,92),"COVER",fontSize:9);}
        if(activity==ActivityFamily.Vault&&!keystone){DrawCircle(new(560,192),12,new Color("e4bf7d"));DrawString(ThemeDB.FallbackFont,new(535,170),"KEYSTONE",fontSize:9);}
        foreach(var enemy in arena.Enemies.Where(e=>e.ReturnMark is not null&&!e.Enemy!.Dead))
        {var mark=enemy.ReturnMark!.Value;DrawArc(mark,34,0,Mathf.Tau,32,new Color("c7b3ef"),2);DrawString(ThemeDB.FallbackFont,mark+new Vector2(-32,-39),enemy.Enemy!.Definition.Style is EnemyStyle.Mark or EnemyStyle.Sentinel && arena.ActiveMutation!="mutation.anchor"?"MARKED PULSE":"RETURN MARK",fontSize:9);}
    }
}
