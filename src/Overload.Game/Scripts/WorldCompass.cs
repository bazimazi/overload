using Godot;
using Overload.Domain;

namespace Overload.Game;

public partial class ArenaHud
{
    private Vector2 localMapOrigin,localMapPan;
    private float localMapScale,localMapZoom=1;
    private bool draggingMap;
    private HBoxContainer mapButtons=null!;
    private Button localMapButton=null!;
    public float LocalMapZoom=>localMapZoom;
    public Vector2 MapPointToScreen(Vector2 point){LayoutLocalMap();return localMapOrigin+point*localMapScale;}
    public void ResetLocalMapView(){localMapZoom=1;localMapPan=Vector2.Zero;draggingMap=false;}
    private void InitializeMapButtons()
    {
        mapButtons=new HBoxContainer();AddChild(mapButtons);
        var local=localMapButton=PanelButton(mapButtons,"Local map",arena.ToggleLocalMap);
        local.TooltipText="Explore your map, set pins and walk to discovered ground";
        var atlas=PanelButton(mapButtons,"World map",arena.OpenRegionalMap);
        atlas.TooltipText="Connected campaign, Endless Frontier and activated waypoints";
        foreach(var b in new[]{local,atlas}){b.AddThemeFontSizeOverride("font_size",11);b.CustomMinimumSize=new(102,28);}
    }
    private void UpdateMapButtons()
    {
        mapButtons.Visible=arena.WorldActive&&!MenuVisible&&!arena.PlayerState.Dead;
        localMapButton.Text=arena.LocalMapVisible?"Close map":"Local map";
        mapButtons.Position=new(Size.X-242,arena.LocalMapVisible?LocalMapPanel.Position.Y+6:183);mapButtons.Size=new(224,30);
    }
    private void DrawWorldCompass()
    {
        var rect=new Rect2(Size.X-242,18,224,164);
        Surface(rect,new Color("71634e"),.88f);
        var field=new Rect2(rect.Position+new Vector2(8,27),new(208,99));
        var center=field.GetCenter();const float scale=.18f;
        var origin=center-arena.Player.Position*scale;
        DrawKnownTerrain(field,origin,scale);
        foreach(var target in arena.KnownWorldTargets())
        {
            var p=origin+target.Position*scale;if(!field.Grow(-5).HasPoint(p))continue;
            DrawMapMarker(p,target.Kind,target.Id==arena.WorldDestination?.Id?gold:teal,3);
        }
        DrawMapEnemies(field,origin,scale);
        DrawPlayerArrow(center,arena.Player.Facing,5);
        Write(rect.Position+new Vector2(10,18),"MINIMAP",10,gold);
        Write(rect.Position+new Vector2(188,18),"N ↑",9,muted);
        if(arena.WorldDestination is {} destination)
        {
            var direction=destination.Position-arena.Player.Position;
            if(arena.GuidancePath.Count>1)
            {var next=arena.GuidancePath[1];direction=new Vector2(next.X,next.Y)-arena.Player.Position;}
            if(direction.LengthSquared()>1)
            {
                var d=direction.Normalized();var n=d.Orthogonal();var p=center+d*37;
                DrawPolyline([p-d*5+n*4,p,p-d*5-n*4],gold,2);
            }
            Write(rect.Position+new Vector2(10,147),Fit(destination.Name,156,10),10,gold);
            Write(rect.Position+new Vector2(170,147),$"{arena.Player.Position.DistanceTo(destination.Position)/32:0}m",10,muted);
        }
    }
    private void DrawPlayerArrow(Vector2 p,Vector2 facing,float radius)
    {
        var d=facing.LengthSquared()>.01f?facing.Normalized():Vector2.Up;var side=d.Orthogonal();
        DrawCircle(p,radius+2,new Color("111619"));
        DrawColoredPolygon([p+d*(radius+2),p-d*radius+side*radius,p-d*radius-side*radius],Colors.White);
    }
    private void DrawMapMarker(Vector2 p,string kind,Color color,float radius)
    {
        if(kind=="exit")DrawPolyline([p+new Vector2(-radius,radius),p+new Vector2(-radius,-radius),p+new Vector2(radius,-radius),p+new Vector2(radius,radius)],color,2);
        else if(kind is "refuge" or "waypoint")DrawPolyline([p+new Vector2(0,-radius-2),p+new Vector2(radius+2,0),p+new Vector2(0,radius+2),p+new Vector2(-radius-2,0),p+new Vector2(0,-radius-2)],color,1.5f);
        else if(kind=="pin"){DrawLine(p+new Vector2(0,7),p-new Vector2(0,4),color,2);DrawCircle(p-new Vector2(0,5),radius,color);}
        else DrawCircle(p,radius,color);
    }
    private void DrawKnownTerrain(Rect2 field,Vector2 origin,float scale)
    {
        DrawRect(field,new Color("101719"));
        var z=arena.WorldZone;var columns=(z.Geometry.Bounds.Width+95)/96;
        var visible=new List<Rect2>();
        foreach(var cell in arena.WorldFog)
        {
            var r=new Rect2(origin+new Vector2(cell%columns*96,cell/columns*96)*scale,new(96*scale,96*scale));
            if(!r.Intersects(field))continue;var clipped=r.Intersection(field);visible.Add(clipped);
            DrawRect(clipped,new Color("364744"));
        }
        foreach(var block in z.Geometry.Blocks)
        {
            var r=new Rect2(origin+new Vector2(block.X,block.Y)*scale,new Vector2(block.Width,block.Height)*scale);
            if(!r.Intersects(field))continue;
            foreach(var known in visible)if(r.Intersects(known))DrawRect(r.Intersection(known),new Color("0a1114"));
        }
        foreach(var road in arena.WorldRoads)
            for(var i=1;i<road.Length;i++)DrawDiscoveredLine(field,origin,scale,road[i-1],road[i],new Color("aa9570",.55f),Math.Max(1,22*scale));
        for(var i=1;i<arena.GuidancePath.Count;i++)
        {
            var a=arena.GuidancePath[i-1];var b=arena.GuidancePath[i];
            DrawDiscoveredLine(field,origin,scale,new(a.X,a.Y),new(b.X,b.Y),gold,1.5f);
        }
        DrawRect(field,new Color("5f6559"),false,1);
    }
    private void DrawDiscoveredLine(Rect2 field,Vector2 origin,float scale,Vector2 a,Vector2 b,Color color,float width)
    {
        var start=origin+a*scale;var end=origin+b*scale;
        if(!new Rect2(start.Min(end)-Vector2.One*width,(start-end).Abs()+Vector2.One*width*2).Intersects(field))return;
        var steps=Math.Max(1,(int)Math.Ceiling(a.DistanceTo(b)/48));
        for(var n=1;n<=steps;n++)
        {
            var from=a.Lerp(b,(n-1)/(float)steps);var to=a.Lerp(b,n/(float)steps);
            if(!arena.WorldCellKnown(from)||!arena.WorldCellKnown(to))continue;
            var p=origin+from*scale;var q=origin+to*scale;
            var direction=q-p;var low=0f;var high=1f;
            bool Clip(float value,float delta,float min,float max)
            {
                if(Math.Abs(delta)<.0001f)return value>=min&&value<=max;
                var x=(min-value)/delta;var y=(max-value)/delta;
                low=Math.Max(low,Math.Min(x,y));high=Math.Min(high,Math.Max(x,y));return low<=high;
            }
            var safe=field.Grow(-width/2);
            if(Clip(p.X,direction.X,safe.Position.X,safe.End.X)&&Clip(p.Y,direction.Y,safe.Position.Y,safe.End.Y))
                DrawLine(p+direction*low,p+direction*high,color,width);
        }
    }
    private void DrawMapEnemies(Rect2 field,Vector2 origin,float scale)
    {
        foreach(var enemy in arena.Enemies.Where(e=>!e.Enemy!.Dead&&arena.WorldCellKnown(e.Position)))
        {var p=origin+enemy.Position*scale;if(field.Grow(-3).HasPoint(p))DrawCircle(p,enemy.Enemy!.Definition.Role==EnemyRole.Bellkeeper?4:2,ember);}
    }
    private Rect2 LocalMapPanel=>new(new Vector2(22,108),new Vector2(Math.Max(180,Size.X-44),Math.Max(180,Size.Y-254)));
    private Rect2 LocalMapField=>new(LocalMapPanel.Position+new Vector2(14,44),LocalMapPanel.Size-new Vector2(28,96));
    private void LayoutLocalMap()
    {
        var b=arena.WorldZone.Geometry.Bounds;var field=LocalMapField;
        localMapScale=Math.Min(field.Size.X/b.Width,field.Size.Y/b.Height)*localMapZoom;
        var span=new Vector2(b.Width,b.Height)*localMapScale;
        var limit=((span+field.Size)/2-new Vector2(40,40)).Max(Vector2.Zero);
        localMapPan=localMapPan.Clamp(-limit,limit);
        localMapOrigin=field.GetCenter()-new Vector2(b.Width,b.Height)*localMapScale/2+localMapPan;
    }
    private bool HandleWorldMapInput(InputEvent input)
    {
        if(!arena.WorldActive||!arena.LocalMapVisible||MenuVisible)return false;
        LayoutLocalMap();
        if(input is InputEventMouseMotion motion&&draggingMap)
        {localMapPan+=motion.Relative;GetViewport().SetInputAsHandled();return true;}
        if(input is not InputEventMouseButton mouse)return false;
        if(mouse.ButtonIndex==MouseButton.Middle)
        {draggingMap=mouse.Pressed&&LocalMapField.HasPoint(mouse.Position);GetViewport().SetInputAsHandled();return true;}
        if(!mouse.Pressed||!LocalMapField.HasPoint(mouse.Position))return false;
        if(mouse.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
        {
            var world=(mouse.Position-localMapOrigin)/localMapScale;
            localMapZoom=Math.Clamp(localMapZoom*(mouse.ButtonIndex==MouseButton.WheelUp?1.25f:.8f),1,8);
            LayoutLocalMap();localMapPan+=mouse.Position-(localMapOrigin+world*localMapScale);
            GetViewport().SetInputAsHandled();return true;
        }
        if(mouse.ButtonIndex is not (MouseButton.Left or MouseButton.Right))return false;
        var point=(mouse.Position-localMapOrigin)/localMapScale;
        if(mouse.ButtonIndex==MouseButton.Left)
        {
            var target=arena.KnownWorldTargets().OrderBy(t=>MapPointToScreen(t.Position).DistanceSquaredTo(mouse.Position))
                .FirstOrDefault(t=>MapPointToScreen(t.Position).DistanceTo(mouse.Position)<14);
            if(target is not null)arena.TrackWorldTarget(target.Id);else arena.PinWorldLocation(point);
        }
        else if(arena.PinWorldLocation(point)){arena.ToggleLocalMap();arena.RequestWorldWalk(point);}
        GetViewport().SetInputAsHandled();return true;
    }
}
