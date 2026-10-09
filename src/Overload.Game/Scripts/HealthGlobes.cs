using Godot;
using Overload.Domain;

namespace Overload.Game;

public partial class CombatEffects
{
    private sealed class HealthGlobe(Vector2 point){public Vector2 Point=point;public float Age;}
    private readonly List<HealthGlobe> healthGlobes=[];
    private int ordinaryKills;
    public int GlobesCollected {get;private set;}
    public int HealthGlobeCount=>healthGlobes.Count;
    private void DropHealthGlobe(ActorBody body)
    {
        if(!arena.AdventureActive||body.Enemy!.Definition.Role==EnemyRole.Bellkeeper)return;
        ordinaryKills++;
        if(ordinaryKills%3==0&&healthGlobes.Count<24)healthGlobes.Add(new(body.Position));
    }
    private void AdvanceHealthGlobes()
    {
        if(arena.PlayerState.Dead)return;
        foreach(var globe in healthGlobes.ToArray())
        {
            globe.Age+=1f/60;
            var delta=arena.Player.Position-globe.Point;
            if(globe.Age<.35f||arena.PlayerState.Life>=arena.PlayerState.MaximumLife||delta.Length()>84
                ||!WorldQueries.ClearRay(this,globe.Point,arena.Player.Position))continue;
            globe.Point=globe.Point.MoveToward(arena.Player.Position,220f/60);
            if(globe.Point.DistanceTo(arena.Player.Position)>16)continue;
            var restored=arena.PlayerState.RestoreLife(12);healthGlobes.Remove(globe);GlobesCollected++;
            if(restored>0)
            {numbers.Add(new(arena.Player.Position+new Vector2(-14,-45),"+"+CounterText.Short(restored/1000)+" LIFE",new Color("f4a1a1"),12));RunePulse(arena.Player.Position,22,new Color("d35a67"));arena.Audio.Play("heal","Player");}
        }
    }
    private void DrawHealthGlobes()
    {
        foreach(var globe in healthGlobes)
        {
            var p=globe.Point-new Vector2(0,7+MathF.Sin(globe.Age*3)*2);
            DrawCircle(p,9,new Color("e75564",.1f));DrawCircle(p,6,new Color("631d28"));DrawCircle(p-new Vector2(1,1),4,new Color("b43b49"));DrawCircle(p-new Vector2(2,2),1.5f,new Color("ffd2b0"));
            DrawArc(p,6,0,Mathf.Tau,20,new Color("ef8890",.7f),1);
            if(globe.Point.DistanceTo(arena.Player.Position)<100)WriteEffect(p-new Vector2(24,15),"LIFE +12%",9,new Color("efadb1"));
        }
    }
}
