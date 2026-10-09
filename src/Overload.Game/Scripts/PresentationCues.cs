using Godot;
using Overload.Domain;

namespace Overload.Game;

public partial class Arena
{
    public ActorBody? HighlightedEnemy
    {
        get
        {
            if(!Playing||Paused||LocalMapVisible||Hud.MenuVisible)return null;
            return Controls.Controller
                ? Enemies.Where(e=>!e.Enemy!.Dead&&Player.Position.DistanceTo(e.Position)<180&&Player.Facing.Dot(Player.Position.DirectionTo(e.Position))>.9f).OrderBy(e=>Player.Position.DistanceSquaredTo(e.Position)).FirstOrDefault()
                : Enemies.Where(e=>!e.Enemy!.Dead&&new Rect2(e.VisualPosition-new Vector2(e.Radius+12,e.Radius>10?70:48),new Vector2((e.Radius+12)*2,e.Radius>10?85:60)).HasPoint(CursorWorldPosition)).OrderBy(e=>e.Position.DistanceSquaredTo(CursorWorldPosition)).FirstOrDefault();
        }
    }
}

public partial class CombatEffects
{
    private sealed record DefeatSeal(Vector2 Position, Color Color) {public float Life=7;}
    private readonly List<DefeatSeal> defeatSeals=[];
    private void MarkDefeat(ActorBody body)
    {
        if(defeatSeals.Count>=32)defeatSeals.RemoveAt(0);
        defeatSeals.Add(new(body.Position,new("78624c")));
        RunePulse(body.Position,body.Radius*2,new("d4b07b"));
    }
    private void DrawPresentationCues()
    {
        foreach(var seal in defeatSeals)
        {
            var alpha=Math.Min(.32f,seal.Life/3);
            DrawSetTransform(seal.Position,0,new(1,.45f));
            DrawCircle(Vector2.Zero,14,new Color("161413",alpha));
            DrawArc(Vector2.Zero,11,0,Mathf.Tau,24,new Color(seal.Color,alpha),1);
            DrawSetTransform(Vector2.Zero);
        }
        if(arena.PointerDestination is {} destination)
        {
            DrawSetTransform(destination,0,new(1,.45f));
            var radius=10+MathF.Sin(juiceTime*5);
            DrawArc(Vector2.Zero,radius,0,Mathf.Tau,32,new Color("e3c78c",.65f),1);
            for(var i=0;i<4;i++){var direction=Vector2.FromAngle(Mathf.Pi/4+i*Mathf.Pi/2);DrawLine(direction*14,direction*18,new Color("e3c78c",.8f));}
            DrawSetTransform(Vector2.Zero);
        }
        if(arena.HighlightedEnemy is {} enemy)
        {
            DrawSetTransform(enemy.VisualPosition,0,new(1,.4f));
            DrawArc(Vector2.Zero,enemy.Radius+8,0,Mathf.Tau,48,new Color("e8b98a",.8f),1.5f);
            DrawSetTransform(Vector2.Zero);
        }
        if(!arena.WorldActive)return;
        var region=arena.WorldZone.Region;var origin=arena.CameraOrigin;
        var color=region==Region.Ash?new Color("c9a084"):region==Region.Glass?new("8bbfba"):region==Region.Hollow?new("b1a0ca"):new("b89095");
        // Bounded, slow-moving dust and mist; they never participate in visibility or hit queries.
        for(var i=0;i<6;i++)
        {
            var p=origin+new Vector2(Mathf.PosMod(i*157+juiceTime*(2+i%2),720)-40,40+i*57);
            DrawSetTransform(p,0,new(1,.16f));
            for(var layer=3;layer>0;layer--)DrawCircle(Vector2.Zero,36+layer*17,new Color(color,.007f));
            DrawSetTransform(Vector2.Zero);
        }
        for(var i=0;i<24;i++)
        {
            var p=origin+new Vector2(Mathf.PosMod(i*89+juiceTime*(5+i%3),640),Mathf.PosMod(i*131-juiceTime*6,360));
            DrawRect(new(p.Round(),Vector2.One),new Color(color,.2f+.1f*MathF.Sin(juiceTime+i)));
        }
    }
}

public partial class ArenaHud
{
    private void DrawTargetPlate()
    {
        if(arena.HighlightedEnemy is not {} body||body.Enemy!.Definition.Role==EnemyRole.Bellkeeper)return;
        var enemy=body.Enemy!;var width=Math.Min(320,Size.X*.27f);var x=(Size.X-width)/2;
        CenterWrite(new(Size.X/2,33),Fit(enemy.Definition.Name??enemy.Definition.Role.ToString(),width,13),13,gold);
        Surface(new(x,42,width,12),new("8b6849"));
        Bar(new(x+3,45,width-6,6),CombatMath.BarBasisPoints(enemy.Life,enemy.MaximumLife)/10000f,new("a7493c"));
        CenterWrite(new(Size.X/2,72),body.Stunned?"STAGGERED":body.Recovering?"EXPOSED":enemy.Definition.Role.ToString().ToUpperInvariant(),10,body.Stunned||body.Recovering?teal:muted);
    }
    private void DrawWorldVignette()
    {
        for(var i=0;i<12;i++)
        {
            var alpha=(1-i/12f)*.045f;var inset=i*6;
            DrawRect(new(inset,0,6,Size.Y),new Color(0,0,0,alpha));
            DrawRect(new(Size.X-inset-6,0,6,Size.Y),new Color(0,0,0,alpha));
            DrawRect(new(0,inset,Size.X,6),new Color(0,0,0,alpha));
        }
    }
}
