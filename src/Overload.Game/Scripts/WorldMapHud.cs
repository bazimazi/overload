using Godot;
using Overload.Domain;

namespace Overload.Game;

public partial class ArenaHud
{
    private string selectedAtlasZone="hearth";
    public bool WorldAtlasVisible { get; private set; }
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
        if(site.Id=="mara"&&arena.AdventureObjective?.Id=="meet")
        {
            Text("FIRST QUEST · A road worth saving",20,gold);
            Text("Take the west road to Cinderroad. Defeat the first patrol, collect its weapon, and prepare for the Ash enforcer.",18,ink);
            Text("Reward: upgraded weapon, 800 bonus XP, 50 gold. Your first level unlocks a skill and talent point.",16,teal);
            AddButton("Accept quest · clear the first patrol",arena.AcceptFirstQuest,true);
        }
        if(w.Resolved.Contains(Region.Ash)&&site.Id=="mara")Text("The channels run cold. The workers are alive. I thought the Pattern kept us safe; you showed me whom it left out.",19,ink);
        if(w.Resolved.Contains(Region.Hollow)&&site.Id=="sen")Text("The register carries every restored name. Nobody needs permission to have existed.",19,ink);
        if(w.Ending is not null)Text(w.Ending=="preserve"?"Every road keeps more than one possibility. Hearth has room for them all.":"The new Pattern begins with a promise: no life is erased to make the world simpler.",19,gold);
        if(site.Id=="mara"&&w.Adventure?.Milestones.Contains("trained")==true)AddButton("Trailkeeper contracts · explore, fight and earn legendary gear",()=>AdventureJournal());
        AddButton("Return to the road",arena.CloseWorldMenu,site.Id!="mara"||arena.AdventureObjective?.Id!="meet");goBack=arena.CloseWorldMenu;
    }
    public void RegionalMap(bool selectCurrent=false)
    {
        var w=arena.Character!.State.World!;
        if(selectCurrent)selectedAtlasZone=arena.WorldZone.Id;
        ClearMenu("THE PALIMPSEST / REGIONAL WORLD","Roads that remain",$"{arena.WorldZone.Name} · {w.Resolved.Count}/4 regions restored. Activated safe waypoints permit travel.");
        WorldAtlasVisible=true;
        options.AddChild(new WorldAtlasCanvas {Progress=w,SelectedZone=selectedAtlasZone,CurrentZone=arena.WorldZone.Id,AtlasHeight=Math.Clamp(Size.Y-290,240,350),TextPercent=arena.Audio.TextPercent,SelectZone=id=>{selectedAtlasZone=id;RegionalMap();},SizeFlagsHorizontal=SizeFlags.ExpandFill});
        var selected=FrontierWorld.TryDepth(selectedAtlasZone,out var reach)?FrontierWorld.Generate(w.CampaignId,reach):WorldContent.Zone(selectedAtlasZone);
        var detail=Section(options,selected.Name);
        BodyLabel(detail,(selected.Kind=="frontier"?"Endless Frontier":selected.Kind=="hearth"?"Starting zone":Arena.RegionName(selected.Region))+" · "+selected.Objective,14,ink);
        BodyLabel(detail,selected.Id==arena.WorldZone.Id?"YOU ARE HERE · Open the local map to inspect terrain and plan a route.":w.Visited.Contains(selected.Id)?"Visited · Select an activated waypoint below to travel safely.":"Unexplored · Follow the connecting roads to discover this area.",12,muted);
        BodyLabel(detail,$"{selected.Geometry.Bounds.Width/32} × {selected.Geometry.Bounds.Height/32} meters · {selected.Kind}",12,gold);
        AddButton("Local terrain map",()=>{arena.CloseWorldMenu();arena.ToggleLocalMap();});
        Text("Endless Frontier",18,gold);
        Text($"Open from the southern trail in Hearth, from level 1. Vast connected reaches continue east with changing terrain and rising danger. Farthest reach: {w.Frontier.Farthest+1}.",14,ink);
        Text("Safe waypoints",18,gold);
        foreach(var (z,site) in WorldRules.AvailableWaypoints(w).OrderBy(p=>p.Zone.Id))
        {
            var id=site.Id;AddButton($"Travel · {site.Name} / {z.Name}",()=>arena.TravelWaypoint(id));
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
        var z=arena.WorldZone;var rect=LocalMapPanel;var field=LocalMapField;
        Surface(rect,opacity:.98f);LayoutLocalMap();
        DrawKnownTerrain(field,localMapOrigin,localMapScale);DrawMapEnemies(field,localMapOrigin,localMapScale);
        var occupied=new List<Rect2>();
        foreach(var target in arena.KnownWorldTargets().OrderByDescending(t=>t.Id==arena.WorldDestination?.Id))
        {
            var p=MapPointToScreen(target.Position);if(!field.Grow(-8).HasPoint(p))continue;
            var active=target.Id==arena.WorldDestination?.Id;
            DrawMapMarker(p,target.Kind,active?gold:teal,4);
            if(active)DrawArc(p,10,0,Mathf.Tau,24,gold,2);
            var width=Math.Min(185,field.End.X-p.X-12);if(width<50)continue;
            var label=new Rect2(p+new Vector2(9,-12),new(width,18));
            if(occupied.Any(r=>r.Intersects(label)))continue;occupied.Add(label);
            DrawRect(label,new Color("101315",.92f));Write(label.Position+new Vector2(3,13),Fit(target.Name,width-6,10),10,active?gold:ink);
        }
        var player=MapPointToScreen(arena.Player.Position);
        if(field.Grow(-8).HasPoint(player))DrawPlayerArrow(player,arena.Player.Facing,6);
        Write(rect.Position+new Vector2(18,29),Fit(z.Name.ToUpperInvariant()+" / LOCAL MAP",rect.Size.X-480,16),16,gold);
        Write(new(rect.End.X-374,rect.Position.Y+29),$"N ↑   {localMapZoom:0.0}× ZOOM",11,muted);
        var known=arena.WorldFog.Count;var total=(z.Geometry.Bounds.Width+95)/96*((z.Geometry.Bounds.Height+95)/96);
        Write(rect.Position+new Vector2(18,rect.Size.Y-30),Fit($"{known*100/Math.Max(1,total)}% explored · ◆ Waypoint  ∩ Road  ● Landmark  · Wheel: zoom  · Middle drag: pan",rect.Size.X-36,11),11,teal);
        Write(rect.Position+new Vector2(18,rect.Size.Y-12),Fit($"{arena.Controls.Glyph("local_map")} / Esc close · Left click: track / pin · Right click: walk · {arena.Controls.Glyph("track")} cycle landmarks · Time stopped",rect.Size.X-36,11),11,muted);
    }
}
