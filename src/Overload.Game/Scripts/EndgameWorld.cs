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
    private CourtProp coverSprite=null!;
    public bool Assisted { get; private set; }
    public bool ObjectiveReady=>activity!=ActivityFamily.Vault || keystone;
    public IEnumerable<Rect2> WarningAreas
    {
        get
        {
            var tick=arena.PlayerState.Tick-entry;
            foreach(var id in rules)
            {
                var rule=WorldLaws.Rules.Single(r=>r.Id==id);
                if(rule.DamagePercent<=0||tick%rule.Period>rule.WarningTicks+15)continue;
                foreach(var area in WorldLaws.Areas(id,tick))yield return new(Position+new Vector2(area.X,area.Y),new(area.Width,area.Height));
            }
        }
    }
    public string Hint=>activity==ActivityFamily.Vault&&!keystone?"Vault: defeat guardians, then touch the golden keystone at the exit.":activity==ActivityFamily.Breach?"Breach: break the wardens. Hazard pulses leave the central lane safe.":"Hunt: bring down the marked elite and boss.";
    public void Initialize(Arena owner)
    {
        arena=owner;
        coverSprite=new CourtProp { Footprint=new(323,101,26,19),Region=Region.Glass,ZIndex=30,ZAsRelative=false,Visible=false,Modulate=new(.7f,1,.95f,.58f) };
        AddChild(coverSprite);
    }
    public void Configure(ImmutableArray<string> selected,string? mutation,ActivityFamily family,bool? assisted=null)
    { if(assisted is { } value)Assisted=value;rules=selected;activity=family;entry=arena.PlayerState.Tick;keystone=false;cover=rules.Contains("law.glass")||arena.FractureActive&&arena.Character!.State.Fracture!.Region==Region.Glass;coverSprite.Visible=cover;coverHits=0;QueueRedraw(); }
    public void Clear() { Position=Vector2.Zero;rules=[];cover=false;coverSprite.Visible=false;keystone=false;QueueRedraw(); }
    private void BreakCover(bool friendly)
    { cover=false;coverSprite.Visible=false;arena.Effects.CoverShards(Position+new Vector2(336,120));if(friendly)arena.CoverBroken(); }
    public void SetAssisted(bool value) { if(!arena.Playing)Assisted=value; }
    public bool CoverCollision(Vector2 from,Vector2 to,bool friendly,bool heavy=false)
    {
        if(!cover || WorldQueries.SegmentCircle(from,to,Position+new Vector2(336,120),18) is null)return false;
        if(friendly) { coverHits++;if(coverHits>=1) BreakCover(true); }
        else if(heavy)BreakCover(false);
        return true;
    }
    public void StrikeCover(Vector2 origin,Vector2 aim,AttackRecipe recipe)
    {
        if(cover&& (recipe.Geometry==AttackGeometry.Lane?WorldQueries.SegmentCircle(origin,origin+aim*recipe.Reach,Position+new Vector2(336,120),18) is not null:WorldQueries.SectorOverlaps(origin,aim,recipe.Reach,recipe.ArcDegrees,Position+new Vector2(336,120),18)))
        BreakCover(true);
    }
    public void Advance()
    {
        if(!arena.Playing||arena.Paused)return;
        if(arena.AdventureActive&&rules.Length>0&&arena.WorldZone.Encounters.Any(e=>e.Boss>=0)&&!arena.Enemies.Any(e=>!e.Enemy!.Dead&&e.Enemy.Definition.Role==EnemyRole.Bellkeeper))
        {Clear();return;}
        var tick=arena.PlayerState.Tick-entry;
        if(activity==ActivityFamily.Vault&&arena.Enemies.All(e=>e.Enemy!.Dead)&&arena.Player.Position.DistanceTo(new(560,192))<=28)keystone=true;
        foreach(var id in rules)
        {
            var rule=WorldLaws.Rules.Single(r=>r.Id==id);
            if(rule.DamagePercent==0 || tick%rule.Period!=rule.WarningTicks)continue;
            foreach(var area in WorldLaws.Areas(id,tick))if(new Rect2(Position+new Vector2(area.X,area.Y),new(area.Width,area.Height)).Grow(arena.Player.Radius).HasPoint(arena.Player.Position))
            {
                var difficulty=arena.AdventureActive?arena.Character!.State.World!.Adventure!.Difficulty:AdventureDifficulty.Adventurer;
                var multiplier=difficulty==AdventureDifficulty.Story?65:difficulty==AdventureDifficulty.Veteran?125:100;
                arena.Effects.HazardHit(Progression.TierScaled(CombatMath.Points(250)*rule.DamagePercent/100,arena.EncounterTier)*(Assisted?50:100)/100*multiplier/100);
            }
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
            {
                var rect=new Rect2(area.X,area.Y,area.Width,area.Height);
                var warned=phase<rule.WarningTicks;
                DrawRect(rect,new Color(1,0.55f,0.25f,warned?.12f:arena.Audio.ReducedFlash?.16f:.32f));
                DrawRect(rect,new Color("ffc178"),false,1);
                if(warned)DrawRect(new(rect.Position,new(rect.Size.X*phase/Math.Max(1f,rule.WarningTicks),2)),new Color("ffe0ad"));
                for(var x=rect.Position.X+5;x<rect.End.X-5;x+=16)
                    DrawLine(new(x,rect.End.Y-7),new(x+4,rect.End.Y-3),new Color("ffc178",.6f),1);
                if(rect.Size.X>=92)DrawString(ThemeDB.FallbackFont,rect.Position+new Vector2(8,14),"FLOOR / MOVE CLEAR",fontSize:8,modulate:new("ffe0ad"));
            }
        }
        if(cover)DrawString(ThemeDB.FallbackFont,new(306,70),"BREAK WARD",fontSize:8,modulate:new("b7ddcf"));
        if(activity==ActivityFamily.Vault&&!keystone)
        {
            var props=PixelAtlas.Load("props");var column=arena.Character?.State.Fracture?.Region switch {Region.Ash=>1,Region.Glass=>2,Region.Hollow=>3,_=>0};
            props.Draw(this,column,3,new(560,192),Colors.White,.7f);
            DrawArc(new(560,192),19,0,Mathf.Tau,32,new Color("e4bf7d",.4f),1);
            DrawString(ThemeDB.FallbackFont,new(533,155),"KEYSTONE",fontSize:8,modulate:new("e4bf7d"));
        }
        foreach(var enemy in arena.Enemies.Where(e=>e.ReturnMark is not null&&!e.Enemy!.Dead))
        {var mark=enemy.ReturnMark!.Value-Position;DrawArc(mark,34,0,Mathf.Tau,32,new Color("c7b3ef"),2);DrawString(ThemeDB.FallbackFont,mark+new Vector2(-32,-39),enemy.Enemy!.Definition.Style is EnemyStyle.Mark or EnemyStyle.Sentinel && arena.ActiveMutation!="mutation.anchor"?"MARKED PULSE":"RETURN MARK",fontSize:9);}
    }
}
