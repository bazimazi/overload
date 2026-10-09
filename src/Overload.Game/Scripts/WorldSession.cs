using Godot;
using Overload.Domain;

namespace Overload.Game;

public partial class Arena
{
    public bool WorldActive { get; private set; }
    public bool LocalMapVisible { get; private set; }
    public ZoneDefinition WorldZone => world.Exploration!;
    public TacticalNavigation WorldNavigation=>world.Navigation;
    public bool GraphFracture=>WorldActive&&FractureActive&&Character?.State.Fracture?.Map is not null;
    public System.Collections.Immutable.ImmutableHashSet<string> WorldClaims=>GraphFracture?Character!.State.Fracture!.Map!.Claims:Character!.State.World!.Claims;
    public System.Collections.Immutable.ImmutableHashSet<int> WorldFog=>liveWorldFog;
    private System.Collections.Immutable.ImmutableHashSet<int> liveWorldFog=[];
    private (int X,int Y) lastFogCell=(-1,-1);
    public string WorldObjective
    {
        get
        {
            if(GraphFracture)return WorldZone.Name+" / "+WorldZone.Objective;
            var w=Character!.State.World!;var z=WorldZone;
            if(z.Kind=="hearth")return "Hearth / "+(!w.Resolved.Contains(Region.Ash)?"Follow the west conduit to Ash Foundry.":!w.Resolved.Contains(Region.Glass)||!w.Resolved.Contains(Region.Hollow)?"Restore Glass Marsh and Hollow Archive, in either order.":!w.Resolved.Contains(Region.Crown)?"Take the north procession road to Crown Scar.":"All roads restored. Fractures continue at the Atlas.");
            if(w.Resolved.Contains(z.Region))return z.Name+" / Region restored · explore remaining shelters or return to Hearth.";
            if(z.Kind=="dungeon")
            {
                var next=z.Sites.FirstOrDefault(s=>s.Kind=="device"&&!w.Claims.Contains(s.Id));
                return z.Name+" / "+(next is not null?$"Restore {next.Name}.":!w.Claims.Contains(WorldContent.Id(z.Region,1)+".boss")?$"Defeat the enforcer in {WorldContent.Places[(int)z.Region][1]}.":$"Enter {WorldContent.Places[(int)z.Region][3]}.");
            }
            if(z.Kind=="wild")return z.Name+" / "+(!w.Claims.Contains(WorldContent.Id(z.Region,1)+".boss")?$"Reach {WorldContent.Places[(int)z.Region][1]}; an optional refuge lies off the lower road.":$"Enter {WorldContent.Places[(int)z.Region][2]} and restore its conduits.");
            return z.Name+" / "+z.Objective;
        }
    }
    private readonly Dictionary<string,List<ActorBody>> worldPacks=[];
    private long lastFogTick;
    private long bankedMapTick;
    private string worldMessage="";
    private long worldMessageUntil;
    public string WorldPrompt
    {
        get
        {
            if(!WorldActive)return "";
            if(worldMessageUntil>PlayerState.Tick)return worldMessage;
            var p=Player.Position;var site=WorldZone.Sites.OrderBy(s=>p.DistanceSquaredTo(V(s.Position))).FirstOrDefault();
            if(site is not null&&p.DistanceTo(V(site.Position))<64)return $"{Controls.Glyph("interact")} · {site.Name}";
            var exit=WorldZone.Exits.OrderBy(e=>p.DistanceSquaredTo(V(e.Position))).FirstOrDefault();
            if(exit is not null&&p.DistanceTo(V(exit.Position))<64)return $"{Controls.Glyph("interact")} · {exit.Name}";
            return $"{Controls.Glyph("local_map")} local map · {Controls.Glyph("region_map")} regional map · {Controls.Glyph("interact")} interact";
        }
    }
    private static Vector2 V(WorldPoint p)=>new(p.X,p.Y);
    public void StartWorld()
    {
        if(Character is null)return;
        if(Character.State.World is null&&!UpdateCharacter(WorldRules.Enroll)){Hud.Title();return;}
        if(!WorldTransaction($"world.resume:{Guid.NewGuid():N}",WorldRules.Resume))return;
        EnterWorldZone(true);
    }
    private void EnterWorldZone(bool rest)
    {
        var w=Character!.State.World!;
        if(rest)ApplyCharacterBuild(true);
        StartEncounter(0,rest);ClearEncounterEnemies();WorldActive=true;worldPacks.Clear();lastFogTick=-120;
        Effects.ZIndex=1000;laws.ZIndex=4;
        world.Configure(w.ActiveZone);world.Progress=w;world.MapClaimed=null;world.MapRequired=null;
        liveWorldFog=w.Fog.GetValueOrDefault(w.ActiveZone.Id,[]);lastFogCell=(-1,-1);
        Player.Position=V(w.Arrival);Player.TeleportVisual();world.Viewer=Player.Position;Audio.SetRegion(w.ActiveZone.Region,w.Resolved.Contains(w.ActiveZone.Region));
        Hud.HideMenu();Effects.Record(w.ActiveZone.Name);AdvanceWorld();AdvanceWorldNavigation();
        if(w.ActiveZone.Kind=="hearth"&&w.Resolved.Contains(Region.Crown)&&w.Ending is null){Paused=true;Audio.SetPaused(true);Hud.WorldEnding();}
    }
    private void EnterFractureMap()
    {
        var run=Character!.State.Fracture!;
        StartEncounter(0);ClearEncounterEnemies();WorldActive=true;FractureActive=true;EncounterTier=run.Tier;ApplyCharacterBuild(true);
        Effects.ZIndex=1000;laws.ZIndex=4;
        activeExpeditionId=run.Id;activeExpeditionSequence=run.Sequence;worldPacks.Clear();lastFogTick=-120;bankedMapTick=0;
        world.Configure(run.Map!.Zone);world.Progress=null;world.MapClaimed=id=>Character.State.Fracture!.Map!.Claims.Contains(id);world.MapRequired=id=>Character.State.Fracture!.Map!.Required.Contains(id);
        liveWorldFog=run.Map.Fog;lastFogCell=(-1,-1);
        Player.Position=V(run.Map.Zone.Arrival);Player.TeleportVisual();world.Viewer=Player.Position;Audio.SetRegion(run.Region,false);Hud.HideMenu();AdvanceWorld();AdvanceWorldNavigation();
    }
    private bool WorldTransaction(string receipt,Func<CharacterState,CharacterState> change)
    {
        try {Character!.Transact(Character.State.Revision,receipt,change);world.Progress=Character.State.World;SaveProblem="";return true;}
        catch(Exception e)when(e is IOException or InvalidOperationException or UnauthorizedAccessException)
        {SaveProblem=e.Message;WorldNotice(e.Message);return false;}
    }
    private void WorldNotice(string text){worldMessage=text;worldMessageUntil=PlayerState.Tick+300;Effects.Record(text);}
    public void FlushWorldFog()
    {
        if(!WorldActive)return;
        RevealWorldFog();
        if(GraphFracture)
        {
            var run=Character!.State.Fracture!;var cells=liveWorldFog;
            if(cells.Count!=run.Map!.Fog.Count)WorldTransaction($"map.fog:{Guid.NewGuid():N}",s=>s with {Fracture=s.Fracture! with {Map=s.Fracture.Map! with {Fog=cells}}});
            return;
        }
        if(Character?.State.World is null)return;
        if(Character.State.World.Fog.GetValueOrDefault(WorldZone.Id,[]).Count!=liveWorldFog.Count)WorldTransaction($"fog:{Guid.NewGuid():N}",s=>s with {World=s.World! with {Fog=s.World.Fog.SetItem(WorldZone.Id,liveWorldFog)}});
    }
    private void RevealWorldFog()
    {
        var b=WorldZone.Geometry.Bounds;var x=(int)Player.Position.X/96;var y=(int)Player.Position.Y/96;
        if(lastFogCell==(x,y))return;lastFogCell=(x,y);
        var columns=(b.Width+95)/96;var rows=(b.Height+95)/96;
        for(var dy=-2;dy<=2;dy++)for(var dx=-2;dx<=2;dx++)if(x+dx>=0&&x+dx<columns&&y+dy>=0&&y+dy<rows)liveWorldFog=liveWorldFog.Add((y+dy)*columns+x+dx);
    }
    private void AdvanceWorld()
    {
        RevealWorldFog();
        if(PlayerState.Tick-lastFogTick>=120){lastFogTick=PlayerState.Tick;FlushWorldFog();}
        var w=Character!.State.World;
        foreach(var e in WorldZone.Encounters)
        {
            if(WorldClaims.Contains(e.Id))continue;
            if(worldPacks.TryGetValue(e.Id,out var pack))
            {
                if(pack.All(a=>a.Enemy!.Dead))
                {
                    if(GraphFracture?ClaimMap(e.Id):WorldTransaction($"world:{w!.CampaignId:N}:{e.Id}",s=>WorldRules.ClaimEncounter(s,w.CampaignId,WorldZone.Id,e.Id)))
                    {
                        Audio.Play("reward","UI");WorldNotice(e.Boss==1?"Region restored. Routes and refuges change.":$"Defenders cleared · +{e.Reward.Xp} XP · +{e.Reward.Gold} gold");
                        nextGuidanceTick=0;
                        w=Character.State.World!;
                        if(GraphFracture&&Character.State.Fracture!.Completed)return;
                        if(!GraphFracture&&e.Boss==1&&WorldZone.Region==Region.Crown&&w!.Ending is null){Paused=true;Audio.SetPaused(true);Hud.WorldEnding();}
                    }
                }
                else if(Player.Position.DistanceTo(V(e.Position))>660)
                {
                    foreach(var body in pack){body.CollisionLayer=0;body.CollisionMask=0;Enemies.Remove(body);engagedWorldActors.Remove(body.ActorId);director.Retire(body.ActorId);body.QueueFree();}worldPacks.Remove(e.Id);
                }
                continue;
            }
            if(Player.Position.DistanceTo(V(e.Position))>340||Enemies.Count(a=>!a.Enemy!.Dead)>9)continue;
            pack=[];
            if(e.Boss>=0)pack.Add(Spawn(RegionalContent.Boss(WorldZone.Region,e.Boss),V(e.Position)));
            else for(var i=0;i<e.Enemies.Length;i++)
            {
                var position=V(e.Position)+Vector2.FromAngle(i*Mathf.Tau/3)*42;
                if(!world.Navigation.Clear(new(position.X,position.Y),new(position.X,position.Y),20))position=V(e.Position);
                pack.Add(Spawn(RegionalContent.Enemy(WorldZone.Region,e.Enemies[i]),position));
            }
            worldPacks[e.Id]=pack;
            if(GraphFracture&&e.Boss>=0){laws.Position=V(e.Position)-new Vector2(320,180);laws.Configure(Character.State.Fracture!.WorldRules,null,ActivityFamily.Hunt);}
            else if(!GraphFracture&&e.Boss>=0){laws.Position=V(e.Position)-new Vector2(320,180);laws.Configure([WorldZone.Region switch {Region.Ash=>"law.vents",Region.Glass=>"law.glass",Region.Hollow=>"law.watch",_=>"law.crown"}],null,ActivityFamily.Hunt);}
        }
        // Retired bodies never accumulate across a long exploration session.
        foreach(var body in Enemies.Where(a=>a.Enemy!.Dead).ToArray())
                if(worldPacks.All(pair=>!pair.Value.Contains(body)||WorldClaims.Contains(pair.Key))) {Enemies.Remove(body);engagedWorldActors.Remove(body.ActorId);director.Retire(body.ActorId);body.QueueFree();}
        AdvanceWorldEngagement();
    }
    private bool ClaimMap(string id)
    {
        var run=Character!.State.Fracture!;var elapsed=PlayerState.Tick-bankedMapTick;
        if(!WorldTransaction($"map:{run.Id:N}:{id}",s=>FractureMapRules.Claim(s with {Fracture=s.Fracture! with {ElapsedTicks=checked(s.Fracture.ElapsedTicks+elapsed),MasteryEarned=s.Fracture.MasteryEarned||masteryEarned}},run.Id,id)))return false;
        bankedMapTick=PlayerState.Tick;
        if(Character.State.Fracture!.Completed){Paused=true;Audio.SetPaused(true);Hud.MapFractureComplete();}
        return true;
    }
    public void InteractWorld()
    {
        if(!WorldActive||Paused)return;
        var p=Player.Position;var w=Character!.State.World;
        var site=WorldZone.Sites.Where(s=>p.DistanceTo(V(s.Position))<64).OrderBy(s=>p.DistanceSquaredTo(V(s.Position))).FirstOrDefault();
        if(site is not null)
        {
            if(GraphFracture){if(ClaimMap(site.Id))WorldNotice(site.Name+" claimed");return;}
            if(!WorldRules.Satisfied(w!,site.Requires)){WorldNotice(WorldRules.GateReason(site.Requires));return;}
            if(site.Kind is "npc" or "lore"){Paused=true;Controls.ClearBuffer();Audio.SetPaused(true);Hud.WorldDialogue(site);return;}
            if(site.Kind is "refuge" or "waypoint"&&Enemies.Any(a=>!a.Enemy!.Dead&&a.Position.DistanceTo(p)<420)){WorldNotice("Clear the nearby danger before resting.");return;}
            FlushWorldFog();
            if(WorldTransaction($"site:{Guid.NewGuid():N}",s=>WorldRules.Interact(s,site.Id)))
            {
                nextGuidanceTick=0;
                if(site.Kind=="shortcut")EnterWorldZone(true);
                else if(site.Kind is "refuge" or "waypoint"){ApplyCharacterBuild(true);PlayerState.Reset();motion.Reset();Effects.Reset();WorldNotice("Safe checkpoint saved · Life, Focus and flasks restored");}
                else WorldNotice(site.Name+" restored");
            }
            return;
        }
        var exit=WorldZone.Exits.Where(e=>p.DistanceTo(V(e.Position))<64).OrderBy(e=>p.DistanceSquaredTo(V(e.Position))).FirstOrDefault();
        if(exit is null)return;
        if(GraphFracture){ReturnToTitle();return;}
        if(!WorldRules.ExitOpen(w!,exit)){WorldNotice(WorldZone.Kind=="boss"?"Defeat the ruler to reopen this gate.":WorldRules.GateReason(exit.Requires));return;}
        FlushWorldFog();
        if(WorldTransaction($"travel:{Guid.NewGuid():N}",s=>WorldRules.Travel(s,exit.Id)))EnterWorldZone(exit.Destination=="hearth");
    }
    public void RetryWorld()
    { if(GraphFracture){EnterFractureMap();return;}if(WorldTransaction($"resume:{Guid.NewGuid():N}",WorldRules.Resume))EnterWorldZone(true); }
    public void OpenRegionalMap()
    {if(!WorldActive)return;LocalMapVisible=false;FlushWorldFog();Paused=true;Controls.ClearBuffer();Audio.SetPaused(true);if(GraphFracture)Hud.MapFractureStatus();else Hud.RegionalMap();}
    public void CloseWorldMenu(){Paused=false;Audio.SetPaused(false);Controls.ClearBuffer();Hud.HideMenu();}
    public void TravelWaypoint(string id)
    {
        if(Enemies.Any(e=>!e.Enemy!.Dead&&e.Position.DistanceTo(Player.Position)<420)){WorldNotice("Travel requires a safe place away from hostiles.");CloseWorldMenu();return;}
        if(WorldTransaction($"waypoint:{Guid.NewGuid():N}",s=>WorldRules.FastTravel(s,id)))EnterWorldZone(true);
    }
    public void ChooseEnding(string choice)
    {if(WorldTransaction($"ending:{Character!.State.World!.CampaignId:N}",s=>WorldRules.End(s,choice))){CloseWorldMenu();WorldNotice(Character.State.Journal.Last());}}
}
