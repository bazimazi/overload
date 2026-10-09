using Godot;
using Overload.Domain;

namespace Overload.Game;

public enum EliteKind {None,Volatile,Stormbound}
public partial class Arena
{
    public IEnumerable<ActorBody> WarningElites=>Enemies.Where(e=>!e.Enemy!.Dead&&e.EliteWarningUntil>0);
    public IEnumerable<Rect2> DangerAreas=>laws.WarningAreas.Concat(WarningElites.Select(e=>new Rect2(e.EliteWarningPoint-new Vector2(64,64),new(128,128))));
    private void AdvanceElites()
    {
        if(!AdventureActive)return;
        foreach(var actor in Enemies.Where(e=>e.Elite!=EliteKind.None&&!e.Enemy!.Dead&&EngagedEnemy(e)))
        {
            if(actor.EliteWarningUntil>0)
            {
                if(PlayerState.Tick<actor.EliteWarningUntil)continue;
                var id=((long)actor.ActorId<<32)|0x80000000u|(uint)(PlayerState.Tick/240);
                Effects.EnemySweep(actor.EliteWarningPoint,Vector2.Right,64,360,actor.Enemy!.Damage,id,PlayerState.Tick+1);
                actor.EliteWarningUntil=0;actor.EliteReadyAt=PlayerState.Tick+210;continue;
            }
            if(actor.EliteReadyAt==0){actor.EliteReadyAt=PlayerState.Tick+120;continue;}
            if(PlayerState.Tick<actor.EliteReadyAt)continue;
            actor.EliteWarningPoint=actor.Elite==EliteKind.Stormbound?Player.Position:actor.Position;
            actor.EliteWarningUntil=PlayerState.Tick+60;
            Audio.Play("warning","Enemy");
        }
    }
}
public partial class CombatEffects
{
    private void DrawEliteWarnings()
    {
        if(arena.PrimaryTarget is { } selected)
        {
            var point=selected.VisualPosition;var color=new Color("f4e2b7");
            DrawArc(point,selected.Radius+9,0,Mathf.Tau,24,new Color(color,.85f),1.5f);
            DrawColoredPolygon([point+new Vector2(-4,-65),point+new Vector2(4,-65),point+new Vector2(0,-59)],color);
        }
        foreach(var actor in arena.Enemies.Where(e=>!e.Enemy!.Dead&&e.Elite!=EliteKind.None))
        {
            var color=actor.Elite==EliteKind.Volatile?new Color("ffc16b"):new Color("90ccff");
            DrawArc(actor.VisualPosition,actor.Radius+5,0,Mathf.Tau,24,new Color(color,.8f),2);
            if(actor.Position.DistanceTo(arena.Player.Position)<240)
                DrawString(ThemeDB.FallbackFont,actor.VisualPosition-new Vector2(25,56),actor.Elite.ToString().ToUpperInvariant(),fontSize:8,modulate:color);
            if(actor.EliteWarningUntil==0)continue;
            color=new Color("ffc16b");
            var point=actor.EliteWarningPoint;var progress=1-(actor.EliteWarningUntil-arena.PlayerState.Tick)/60f;
            DrawCircle(point,64,new Color(color,.12f));DrawArc(point,64,0,Mathf.Tau,48,color,2);
            DrawArc(point,60,-Mathf.Pi/2,-Mathf.Pi/2+Math.Max(.01f,progress*Mathf.Tau),48,new Color(color,.6f),3);
            DrawString(ThemeDB.FallbackFont,point-new Vector2(42,78),"ELITE PULSE · MOVE OUT",fontSize:8,modulate:color);
        }
    }
}
