using Godot;
using Overload.Domain;

namespace Overload.Game;

public sealed record WorldTarget(string Id, string Name, Vector2 Position, string Kind);

public partial class Arena
{
    private readonly HashSet<int> engagedWorldActors=[];
    private string? trackedWorldId;
    private System.Numerics.Vector2[] guidancePath=[];
    private long nextGuidanceTick;
    private WorldTarget? worldPin;
    public WorldTarget? WorldDestination { get; private set; }
    public IReadOnlyList<System.Numerics.Vector2> GuidancePath=>guidancePath;
    public bool WorldCellKnown(Vector2 p)
    {
        var b=WorldZone.Geometry.Bounds;
        var columns=(b.Width+WorldRules.FogStep-1)/WorldRules.FogStep;
        return p.X>=0&&p.Y>=0&&p.X<b.Width&&p.Y<b.Height
            &&WorldFog.Contains((int)p.Y/WorldRules.FogStep*columns+(int)p.X/WorldRules.FogStep);
    }
    public IEnumerable<WorldTarget> KnownWorldTargets()
    {
        if(!WorldActive)yield break;
        foreach(var exit in WorldZone.Exits.Where(e=>WorldCellKnown(V(e.Position))&&(GraphFracture||WorldRules.ExitOpen(Character!.State.World!,e))))
            yield return new("exit:"+exit.Id,exit.Name,V(exit.Position),"exit");
        foreach(var site in WorldZone.Sites.Where(s=>WorldCellKnown(V(s.Position))&&s.Kind is "refuge" or "waypoint" or "device" or "cache" or "lore" or "npc"))
            if((GraphFracture||WorldRules.Satisfied(Character!.State.World!,site.Requires)||site.Kind=="refuge")&&(!WorldClaims.Contains(site.Id)||site.Kind is "waypoint" or "refuge"))
                yield return new(site.Id,site.Name,V(site.Position),site.Kind);
        if(worldPin is not null)yield return worldPin;
        foreach(var loot in NearbyLoot.Where(d=>WorldCellKnown(V(d.Position))))yield return new("loot:"+loot.Item.Id,loot.Item.Name,V(loot.Position),"cache");
    }
    public void TrackWorldTarget(string id)
    {
        if(!KnownWorldTargets().Any(t=>t.Id==id))return;
        trackedWorldId=id;nextGuidanceTick=0;AdvanceWorldNavigation();
    }
    public bool PinWorldLocation(Vector2 point)
    {
        if(!WorldActive||!WorldCellKnown(point)||!WorldNavigation.Clear(new(point.X,point.Y),new(point.X,point.Y),Player.Radius))return false;
        worldPin=new("map.pin","Your map pin",point,"pin");TrackWorldTarget(worldPin.Id);return true;
    }
    public void CycleWorldTarget()
    {
        var targets=KnownWorldTargets().OrderBy(t=>t.Id).ToArray();
        if(targets.Length==0)return;
        var index=Array.FindIndex(targets,t=>t.Id==trackedWorldId);
        TrackWorldTarget(targets[(index+1)%targets.Length].Id);
        WorldNotice("Tracking "+WorldDestination!.Name);
    }
    private WorldTarget? MainWorldTarget()
    {
        var z=WorldZone;
        var quest=GuidedAdventureTarget();if(quest is not null)return quest;
        if(FrontierActive)
        {
            var camp=z.Sites.Single(s=>s.Kind=="waypoint");
            return !WorldClaims.Contains(camp.Id)?new(camp.Id,camp.Name,V(camp.Position),"waypoint")
                :new("exit:onward","Uncharted wilderness",V(z.Exits.Single(e=>e.Id=="onward").Position),"exit");
        }
        if(GraphFracture)
        {
            var map=Character!.State.Fracture!.Map!;
            var site=z.Sites.FirstOrDefault(s=>map.Required.Contains(s.Id)&&!map.Claims.Contains(s.Id)&&(s.Requires.Length==0||map.Claims.Contains(s.Requires)));
            if(site is not null)return new(site.Id,site.Name,V(site.Position),site.Kind);
            var encounter=z.Encounters.FirstOrDefault(e=>map.Required.Contains(e.Id)&&!map.Claims.Contains(e.Id));
            if(encounter is not null)return new(encounter.Id,encounter.Boss>=0?"Fracture ruler":"Marked defenders",V(encounter.Position),"encounter");
            return null;
        }
        var w=Character!.State.World!;
        if(z.Kind=="boss")
        {
            var boss=z.Encounters.FirstOrDefault(e=>!w.Claims.Contains(e.Id));
            if(boss is not null)return new(boss.Id,z.Name+" ruler",V(boss.Position),"encounter");
        }
        if(z.Kind=="dungeon")
        {
            var site=z.Sites.FirstOrDefault(s=>s.Kind=="device"&&!w.Claims.Contains(s.Id));
            if(site is not null)return new(site.Id,site.Name,V(site.Position),"device");
        }
        var exit=z.Kind switch
        {
            "hearth"=>z.Exits.FirstOrDefault(e=>!FrontierWorld.TryDepth(e.Destination,out _)&&WorldRules.ExitOpen(w,e)&&!w.Resolved.Contains(WorldContent.Zone(e.Destination).Region))??z.Exits.FirstOrDefault(e=>e.Id=="frontier"),
            "wild"=>z.Exits.FirstOrDefault(e=>e.Id==(!w.Claims.Contains(WorldContent.Id(z.Region,1)+".boss")?"enforcer":w.Resolved.Contains(z.Region)?"home":"dungeon")),
            "dungeon"=>z.Exits.FirstOrDefault(e=>e.Id==(WorldRules.Satisfied(w,WorldContent.Id(z.Region,2)+".ready")?"ruler":"return")),
            _=>z.Exits.FirstOrDefault()
        };
        return exit is null?null:new("exit:"+exit.Id,exit.Name,V(exit.Position),"exit");
    }
    private void AdvanceWorldNavigation()
    {
        if(!WorldActive)return;
        if(PlayerState.Tick<nextGuidanceTick)return;
        nextGuidanceTick=PlayerState.Tick+45;
        WorldDestination=KnownWorldTargets().FirstOrDefault(t=>t.Id==trackedWorldId)??MainWorldTarget();
        var from=new System.Numerics.Vector2(Player.Position.X,Player.Position.Y);
        var waypoints=WorldDestination is null?[]:WorldNavigation.FindPath(from,new(WorldDestination.Position.X,WorldDestination.Position.Y),Player.Radius);
        guidancePath=waypoints.Length==0?[]:[from,..waypoints];
    }
    private void ResetWorldNavigation()
    {trackedWorldId=null;worldPin=null;WorldDestination=null;guidancePath=[];nextGuidanceTick=0;engagedWorldActors.Clear();}
    private bool EngagedEnemy(ActorBody actor)=>!WorldActive||actor.Enemy!.Dead||engagedWorldActors.Contains(actor.ActorId);
    private bool HasEngagedEnemies=>Enemies.Any(e=>!e.Enemy!.Dead&&EngagedEnemy(e));
    private void AdvanceWorldEngagement()
    {
        if(!WorldActive)return;
        foreach(var pack in worldPacks.Values)
            if(pack.Any(a=>engagedWorldActors.Contains(a.ActorId)||a.Enemy!.Life<a.Enemy.MaximumLife
                ||a.Position.DistanceTo(Player.Position)<210&&WorldQueries.ClearRay(a,a.Position,Player.Position)))
                foreach(var actor in pack)engagedWorldActors.Add(actor.ActorId);
        foreach(var actor in Enemies.Where(a=>!worldPacks.Values.Any(p=>p.Contains(a))))engagedWorldActors.Add(actor.ActorId);
    }
}
