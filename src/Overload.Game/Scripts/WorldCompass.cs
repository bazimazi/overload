using Godot;
using Overload.Domain;

namespace Overload.Game;

public partial class ArenaHud
{
    private Vector2 localMapOrigin;
    private float localMapScale;
    private void DrawWorldCompass()
    {
        var rect=new Rect2(Size.X-242,18,224,164);
        Surface(rect,new Color("71634e"),.88f);
        var field=new Rect2(rect.Position+new Vector2(8,26),new(208,102));
        var center=field.GetCenter();const float scale=.18f;
        var origin=center-arena.Player.Position*scale;
        var z=arena.WorldZone;var columns=(z.Geometry.Bounds.Width+95)/96;
        foreach(var cell in arena.WorldFog)
        {
            var r=new Rect2(origin+new Vector2(cell%columns*96,cell/columns*96)*scale,new(96*scale,96*scale));
            if(r.Intersects(field))DrawRect(r.Intersection(field),new Color("344642",.8f));
        }
        foreach(var block in z.Geometry.Blocks)
        {
            var r=new Rect2(origin+new Vector2(block.X,block.Y)*scale,new Vector2(block.Width,block.Height)*scale);
            foreach(var cell in arena.WorldFog)
            {
                var known=new Rect2(origin+new Vector2(cell%columns*96,cell/columns*96)*scale,new(96*scale,96*scale));
                if(r.Intersects(known)&&r.Intersection(known).Intersects(field))DrawRect(r.Intersection(known).Intersection(field),new Color("0a151b"));
            }
        }
        foreach(var target in arena.KnownWorldTargets())
        {
            var p=origin+target.Position*scale;if(!field.Grow(-5).HasPoint(p))continue;
            DrawMapMarker(p,target.Kind,target.Id==arena.WorldDestination?.Id?gold:teal,3);
        }
        foreach(var enemy in arena.Enemies.Where(e=>!e.Enemy!.Dead&&arena.WorldCellKnown(e.Position)))
        {var p=origin+enemy.Position*scale;if(field.Grow(-3).HasPoint(p))DrawCircle(p,2,ember);}
        DrawColoredPolygon([center+new Vector2(0,-5),center+new Vector2(-4,4),center+new Vector2(4,4)],ink);
        if(arena.WorldDestination is {} destination)
        {
            var direction=destination.Position-arena.Player.Position;
            if(arena.GuidancePath.Count>1)
            {var next=arena.GuidancePath[1];direction=new Vector2(next.X,next.Y)-arena.Player.Position;}
            if(direction.LengthSquared()>1)
            {
                var p=center+direction.Normalized()*37;
                var d=direction.Normalized();var n=d.Orthogonal();
                DrawPolyline([p-d*5+n*4,p,p-d*5-n*4],gold,2);
            }
            Write(rect.Position+new Vector2(10,147),Fit(destination.Name,156,10),10,gold);
            Write(rect.Position+new Vector2(170,147),$"{arena.Player.Position.DistanceTo(destination.Position)/32:0}m",10,muted);
        }
        Write(rect.Position+new Vector2(10,18),$"{arena.Controls.Glyph("local_map")} MAP",10,gold);
        Write(rect.Position+new Vector2(119,18),Fit($"{arena.Controls.Glyph("region_map")} WORLD",95,9),9,muted);
        Write(new(Size.X-232,201),Fit($"{arena.Controls.Glyph("track")} / TRACK LANDMARK",214,10),10,muted);
    }
    private void DrawMapMarker(Vector2 p,string kind,Color color,float radius)
    {
        if(kind=="exit")DrawPolyline([p+new Vector2(-radius,radius),p+new Vector2(-radius,-radius),p+new Vector2(radius,-radius),p+new Vector2(radius,radius)],color,2);
        else if(kind is "refuge" or "waypoint")DrawPolyline([p+new Vector2(0,-radius-2),p+new Vector2(radius+2,0),p+new Vector2(0,radius+2),p+new Vector2(-radius-2,0),p+new Vector2(0,-radius-2)],color,1.5f);
        else DrawCircle(p,radius,color);
    }
    private bool HandleWorldMapInput(InputEvent input)
    {
        if(!arena.WorldActive||!arena.LocalMapVisible||MenuVisible)return false;
        if(input is not InputEventMouseButton {Pressed:true,ButtonIndex:MouseButton.Left} mouse)return false;
        var target=arena.KnownWorldTargets().OrderBy(t=>(localMapOrigin+t.Position*localMapScale).DistanceSquaredTo(mouse.Position))
            .FirstOrDefault(t=>(localMapOrigin+t.Position*localMapScale).DistanceTo(mouse.Position)<24);
        if(target is not null)arena.TrackWorldTarget(target.Id);
        GetViewport().SetInputAsHandled();return true;
    }
}
