using Godot;
using Overload.Domain;
using BigInteger=System.Numerics.BigInteger;
namespace Overload.Game;
public partial class CombatEffects
{
    private sealed class GroundCast(Vector2 origin, Vector2 aim, BigInteger damage, SkillId skill, long due, EffectRoot root)
    { public Vector2 Origin=origin,Aim=aim;public BigInteger Damage=damage;public SkillId Skill=skill;public long Due=due;public int Pulse;public float Radius=48;public int Pulses=1,Interval=24,Stagger;public EffectRoot Root=root; }
    private readonly List<GroundCast> groundCasts=[];
    private GroundCast? commandedEcho;
    public int GroundCastCount=>groundCasts.Count;
    public int CommandedEchoCount=>commandedEcho is null?0:1;
    private bool FrameAction(ActionExecution action, Vector2 aim, BigInteger budget)
    {
        var id=action.Definition.Id;
        if(action.Implementation!=ActionImplementation.Base)return false;
        if(id==SkillId.BoneVolley)
        {
            var victims=new HashSet<int>();var root=new EffectRoot(action.RootActionId,action.Source);
            for(var i=-1;i<=1;i++)if(root.TryChild(root.Primary,out var child))Launch(arena.Player.Position,aim.Rotated(i*.12f),action.Definition.ProjectileSpeed,action.Definition.ProjectileRadius,action.Definition.Reach,budget,true,3,action.Definition.Stagger,action.RootActionId,provenance:child,skill:id,sharedVictims:victims);
            return true;
        }
        if(id is SkillId.Bulwark or SkillId.RecallThread or SkillId.Veil)
        { arena.PlayerState.GrantBarrier(action.Definition.BarrierPercent,arena.Character!.State.Techniques.GetValueOrDefault(id)==Technique.Reach?240:180);Record(id+": temporary barrier");return true; }
        if(id is not (SkillId.EmberWell or SkillId.StormLoom or SkillId.EchoOrder))return false;
        var point=WorldQueries.ClipProjectile(this,arena.Player.Position,arena.Player.Position+aim*action.Definition.Reach,12,out _);
        var cast=new GroundCast(point,aim,budget,id,arena.PlayerState.Tick+action.Definition.EffectDelay,new(action.RootActionId,action.Source)) { Radius=action.Definition.EffectRadius,Pulses=action.Definition.EffectPulses,Interval=action.Definition.EffectInterval,Stagger=action.Definition.Stagger };
        if(id==SkillId.EchoOrder)commandedEcho=cast;
        else if(groundCasts.Count<4)groundCasts.Add(cast);
        else Record("Ground cast limit reached");
        Record(id+": fixed cast along aim");return true;
    }
    private void AdvanceFrames()
    {
        if(arena.PlayerState.Dead){groundCasts.Clear();commandedEcho=null;return;}
        var tick=arena.PlayerState.Tick;
        foreach(var cast in groundCasts.ToArray())
        {
            if(tick<cast.Due)continue;
            var primary=cast.Root.Primary;
            var provenance=primary;
            if(cast.Pulse>0) { if(!cast.Root.TryChild(primary,out provenance)){groundCasts.Remove(cast);continue;}provenance=provenance with { Source=SourceKind.SecondaryEffect }; }
            var pulses=cast.Pulses;
            RunePulse(cast.Origin, cast.Radius, cast.Skill == SkillId.EmberWell ? new Color("edb073") : new Color("baa6ed"));
            var amount=cast.Damage*(cast.Pulse+1)/pulses-cast.Damage*cast.Pulse/pulses;
            foreach(var target in arena.Enemies.Where(e=>!e.Enemy!.Dead&&e.Position.DistanceTo(cast.Origin)<=cast.Radius+e.Radius&&WorldQueries.ClearRay(this,cast.Origin,e.Position)).OrderBy(e=>e.ActorId))
                HitEnemy(target,amount,cast.Stagger,provenance,cast.Origin,cast.Aim);
            cast.Pulse++;cast.Due+=cast.Interval;
            if(cast.Pulse>=pulses)groundCasts.Remove(cast);
        }
        if(commandedEcho is { } echo && tick>=echo.Due)
        {
            var target=arena.Enemies.Where(e=>!e.Enemy!.Dead&&WorldQueries.ClearRay(this,echo.Origin,e.Position)).OrderBy(e=>e.Position.DistanceSquaredTo(echo.Origin)).ThenBy(e=>e.ActorId).FirstOrDefault();
            if(target is not null&&echo.Root.TryChild(echo.Root.Primary,out var child))
                Launch(echo.Origin,(target.Position-echo.Origin).Normalized(),360,5,240,echo.Damage*(echo.Pulse+1)/echo.Pulses-echo.Damage*echo.Pulse/echo.Pulses,true,1,echo.Stagger,echo.Root.Primary.RootActionId,provenance:child with { Source=SourceKind.SecondaryEffect });
            echo.Pulse++;echo.Due+=echo.Interval;if(echo.Pulse>=echo.Pulses)commandedEcho=null;
        }
    }
    private void DrawFrames()
    {
        foreach(var cast in groundCasts)
        {
            var color=cast.Skill==SkillId.EmberWell?new Color("f1ac70"):new Color("a49ce2");
            DrawCircle(cast.Origin,cast.Radius,new Color(color,.07f));
            DrawArc(cast.Origin,cast.Radius,0,Mathf.Tau,48,new Color(color,.7f),1);
            DrawArc(cast.Origin,cast.Radius*.82f,0,Mathf.Tau,48,new Color(color,.3f),1);
            for(var i=0;i<8;i++)
            {
                var a=juiceTime*.6f+i*Mathf.Tau/8;var direction=Vector2.FromAngle(a);
                var point=cast.Origin+direction*cast.Radius*.9f;
                DrawLine(point-direction*3,point+direction*3,color,1);
                if(cast.Skill==SkillId.EmberWell)
                { var spark=cast.Origin+Vector2.FromAngle(a+juiceTime)*cast.Radius*(.3f+(i%3)*.15f);DrawRect(new(spark.Round()-new Vector2(0,(juiceTime*12+i*7)%12),new(1,2)),new Color(color,.65f)); }
                else
                { var p=cast.Origin+direction*cast.Radius*.55f;DrawPolyline([cast.Origin,p+direction.Orthogonal()*MathF.Sin(juiceTime*12+i)*6,point],new Color(color,.25f),1); }
            }
        }
        if(commandedEcho is { } echo)
        {
            DrawArc(echo.Origin,14,0,Mathf.Tau,24,new Color("a8d2c8",.5f),1);
            var facing=((int)MathF.Round(echo.Aim.Angle()/(Mathf.Tau/8))+8)%8;
            var atlas=PixelAtlas.Load("revenant");atlas.Draw(this,facing,echo.Due-arena.PlayerState.Tick<8?atlas.Windup:atlas.Idle,echo.Origin,new Color(.7f,1,.9f,.48f));
        }
    }
}
