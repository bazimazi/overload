using Godot;
using Overload.Domain;

namespace Overload.Game;

public partial class ArenaHud
{
    public void MapFractureStatus()
    {
        var run=arena.Character!.State.Fracture!;var map=run.Map!;
        ClearMenu($"FRACTURE {CounterText.Short(run.Tier)} / {run.Activity}",map.Zone.Name,map.Zone.Objective);
        foreach(var id in map.Required.Order())Text($"{(map.Claims.Contains(id)?"COMPLETE":"OPEN")} · {id.Replace("map.","").Replace('.', ' ')}",18,gold);
        Text($"Fixed maximum: {CounterText.Short(run.XpBudget)} XP · {CounterText.Short(run.GoldBudget)} gold · {CounterText.Short(run.AlloyBudget)} Alloy. Optional caches hold 20% of XP; unclaimed cache XP is forfeited on completion.",16,muted);
        AddButton("Return to expedition",arena.CloseWorldMenu,true);AddButton("Hearth · retain objective checkpoint",arena.ReturnToTitle);goBack=arena.CloseWorldMenu;
    }
    public void MapFractureComplete()
    {
        var run=arena.Character!.State.Fracture!;
        ClearMenu("OBJECTIVES COMPLETE / REWARDS BANKED",run.Map!.Zone.Name,arena.Character.State.Journal.Last());
        Text("The physical routes are complete. The next tier is available at the Atlas. Optional caches left behind supplied no XP.",16,gold);
        AddButton("Return to Hearth",arena.ReturnToTitle,true);goBack=arena.ReturnToTitle;
    }
    public void WorldDialogue(WorldSite site)
    {
        var w=arena.Character!.State.World!;
        ClearMenu(arena.WorldZone.Name.ToUpperInvariant(),site.Name,site.Text);
        if(w.Resolved.Contains(Region.Ash)&&site.Id=="mara")Text("The channels run cold. The workers are alive. I thought the Pattern kept us safe; you showed me whom it left out.",19,ink);
        if(w.Resolved.Contains(Region.Hollow)&&site.Id=="sen")Text("The register carries every restored name. Nobody needs permission to have existed.",19,ink);
        if(w.Ending is not null)Text(w.Ending=="preserve"?"Every road keeps more than one possibility. Hearth has room for them all.":"The new Pattern begins with a promise: no life is erased to make the world simpler.",19,gold);
        AddButton("Return to the road",arena.CloseWorldMenu,true);goBack=arena.CloseWorldMenu;
    }
    public void RegionalMap()
    {
        var w=arena.Character!.State.World!;
        ClearMenu("THE PALIMPSEST / REGIONAL WORLD","Roads that remain",$"{arena.WorldZone.Name} · {w.Resolved.Count}/4 regions restored. Activated safe waypoints permit travel.");
        options.AddChild(new RegionalWorldCanvas {Progress=w,TextScale=arena.Audio.TextPercent/100f,CustomMinimumSize=new(0,250),SizeFlagsHorizontal=SizeFlags.ExpandFill});
        foreach(var r in Enum.GetValues<Region>())
        {
            var known=w.Discovered.Contains(WorldContent.Id(r,0));var gate=r==Region.Ash?"":r==Region.Crown?"crown.open":"ash.resolved";
            Text($"{WorldContent.RegionNames[(int)r]} · {(w.Resolved.Contains(r)?"RESTORED":w.Visited.Contains(WorldContent.Id(r,0))?"VISITED":known?"DISCOVERED":"RUMORED")}",18,gold);
            Text(WorldRules.Satisfied(w,gate)?WorldContent.Problems[(int)r]:WorldRules.GateReason(gate),14,muted);
            if(known)foreach(var z in WorldContent.Zones.Values.Where(z=>z.Id!="hearth"&&z.Region==r).OrderBy(z=>z.Id))
                Text($"{z.Name} · {(w.Visited.Contains(z.Id)?"visited":w.Discovered.Contains(z.Id)?"discovered":"unexplored")}",14,ink);
        }
        Text("Safe waypoints",18,gold);
        foreach(var id in w.Waypoints.Order())
        {
            var z=WorldContent.Zones.Values.Single(z=>z.Sites.Any(p=>p.Id==id));var site=z.Sites.Single(p=>p.Id==id);
            AddButton($"Travel · {site.Name} / {z.Name}",()=>arena.TravelWaypoint(id));
        }
        AddButton("Return to exploration",arena.CloseWorldMenu,true);goBack=arena.CloseWorldMenu;
    }
    public void WorldEnding()
    {
        ClearMenu("THE FIRST PATTERN FALLS","A world that can change","The Pattern held reality together by erasing incompatible lives. The roads are restored. Decide what the Manyborn leaves behind.");
        AddButton("Preserve contradictions · every life may remain",()=>arena.ChooseEnding("preserve"),true);
        AddButton("Bind a gentler Pattern · make preservation its first law",()=>arena.ChooseEnding("bind"));
        Text("The campaign is complete. Both choices leave all regions and uncapped Fractures available at Hearth.",16,gold);goBack=null;
    }
    private void DrawLocalWorldMap()
    {
        var z=arena.WorldZone;var bounds=z.Geometry.Bounds;
        var rect=new Rect2(new Vector2(50,110),new Vector2(Size.X-100,Size.Y-250));
        if(rect.Size.Y<100)return;
        Surface(rect,opacity:.94f);
        var scale=Math.Min((rect.Size.X-48)/bounds.Width,(rect.Size.Y-70)/bounds.Height);
        var origin=rect.GetCenter()-new Vector2(bounds.Width,bounds.Height)*scale/2+new Vector2(0,12);
        localMapOrigin=origin;localMapScale=scale;
        Rect2 Project(RoomBlock b)=>new(origin+new Vector2(b.X,b.Y)*scale,new Vector2(b.Width,b.Height)*scale);
        var cells=arena.WorldFog;var columns=(bounds.Width+WorldRules.FogStep-1)/WorldRules.FogStep;
        bool Known(WorldPoint p)=>cells.Contains((int)p.Y/WorldRules.FogStep*columns+(int)p.X/WorldRules.FogStep);
        foreach(var cell in cells)
        {
            var x=cell%columns*WorldRules.FogStep;var y=cell/columns*WorldRules.FogStep;
            DrawRect(Project(new(x,y,Math.Min(WorldRules.FogStep,bounds.Width-x),Math.Min(WorldRules.FogStep,bounds.Height-y))),new Color("394c4b"));
        }
        // Walls and icons are clipped to discovery, so this overlay never exposes hidden caches.
        foreach(var block in z.Geometry.Blocks)
            foreach(var cell in cells)
            {
                var x=cell%columns*96;var y=cell/columns*96;
                var left=Math.Max(x,block.X);var top=Math.Max(y,block.Y);var right=Math.Min(x+96,block.X+block.Width);var bottom=Math.Min(y+96,block.Y+block.Height);
                if(right>left&&bottom>top)DrawRect(new(origin+new Vector2(left,top)*scale,new Vector2(right-left,bottom-top)*scale),new Color("0d191e"));
            }
        foreach(var exit in z.Exits.Where(e=>Known(e.Position)))
        {var p=origin+new Vector2(exit.Position.X,exit.Position.Y)*scale;DrawRect(new(p-new Vector2(4,4),new(8,8)),gold);Write(p+new Vector2(8,-4),exit.Name,11,ink);}
        foreach(var site in z.Sites.Where(s=>Known(s.Position)&&s.Kind is "waypoint" or "refuge" or "device" or "cache"))
        {var p=origin+new Vector2(site.Position.X,site.Position.Y)*scale;DrawCircle(p,4,new("8ee4bd"));Write(p+new Vector2(8,4),site.Name,11,ink);}
        var player=origin+arena.Player.Position*scale;DrawCircle(player,5,Colors.White);DrawLine(player,player+arena.Player.Facing*13,Colors.White,2);
        for(var i=1;i<arena.GuidancePath.Count;i++)
        {
            var a=arena.GuidancePath[i-1];var b=arena.GuidancePath[i];
            var steps=Math.Max(1,(int)Math.Ceiling(System.Numerics.Vector2.Distance(a,b)/24));
            for(var n=1;n<=steps;n++)
            {
                var from=System.Numerics.Vector2.Lerp(a,b,(n-1)/(float)steps);var to=System.Numerics.Vector2.Lerp(a,b,n/(float)steps);
                if(Known(new(from.X,from.Y))&&Known(new(to.X,to.Y)))DrawLine(origin+new Vector2(from.X,from.Y)*scale,origin+new Vector2(to.X,to.Y)*scale,new Color("e4bf7d",.65f),2);
            }
        }
        foreach(var target in arena.KnownWorldTargets())
            if(target.Id==arena.WorldDestination?.Id)DrawArc(origin+target.Position*scale,10,0,Mathf.Tau,24,gold,2);
        Write(rect.Position+new Vector2(20,28),z.Name.ToUpperInvariant()+" / EXPLORED GEOMETRY",16,gold);
        Write(rect.Position+new Vector2(20,rect.Size.Y-14),Fit($"{arena.Controls.Glyph("local_map")} close · Click a discovered landmark to track · {arena.Controls.Glyph("track")} cycle · Time is stopped",rect.Size.X-40,12),12,muted);
    }
}

public partial class RegionalWorldCanvas : Control
{
    public WorldProgress Progress { get; init; }=new();
    public float TextScale { get; init; }=1;
    public override void _Draw()
    {
        var center=Size/2;var span=Math.Clamp((Size.X-150)/2,70,210);var positions=new Vector2[]{center+new Vector2(-span,0),center+new Vector2(0,85),center+new Vector2(span,0),center-new Vector2(0,85)};
        foreach(var p in positions)DrawLine(center,p,new("586969"),2);
        DrawCircle(center,9,new("e4bf7d"));DrawString(ThemeDB.FallbackFont,center+new Vector2(-26,27),"HEARTH",fontSize:(int)(12*TextScale));
        for(var i=0;i<4;i++)
        {var r=(Region)i;var known=Progress.Discovered.Contains(WorldContent.Id(r,0));DrawCircle(positions[i],8,Progress.Resolved.Contains(r)?new("8ee4bd"):known?new("e4bf7d"):new("718080"));
            DrawString(ThemeDB.FallbackFont,positions[i]+new Vector2(-65,-18),WorldContent.RegionNames[i],fontSize:(int)(14*TextScale),modulate:known?Colors.White:new("8eabb6"));}
    }
}
