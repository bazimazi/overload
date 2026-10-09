using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

namespace Overload.Domain;

public sealed record WorldProgress
{
    public string Version { get; init; } = WorldContent.Version;
    public int GeographyVersion { get; init; } = 1;
    public Guid CampaignId { get; init; } = Guid.NewGuid();
    public ZoneDefinition ActiveZone { get; init; } = WorldContent.Zone("hearth");
    public WorldPoint Arrival { get; init; } = new(640,384);
    public string SafeZone { get; init; } = "hearth";
    public ZoneDefinition SafeSnapshot { get; init; } = WorldContent.Zone("hearth");
    public WorldPoint SafePosition { get; init; } = new(640,430);
    public ImmutableHashSet<string> Discovered { get; init; } = ["hearth", "ash.0"];
    public ImmutableHashSet<string> Visited { get; init; } = ["hearth"];
    public ImmutableHashSet<string> Waypoints { get; init; } = ["hearth.waypoint"];
    public ImmutableHashSet<string> Claims { get; init; } = [];
    public ImmutableHashSet<Region> Resolved { get; init; } = [];
    public ImmutableDictionary<string, ImmutableHashSet<int>> Fog { get; init; } = ImmutableDictionary<string, ImmutableHashSet<int>>.Empty;
    public bool LegacyRewardsSuppressed { get; init; }
    public string? Ending { get; init; }
    public FrontierProgress Frontier { get; init; } = new();
    public AdventureProgress? Adventure { get; init; }
}

public static class WorldRules
{
    public const int FogStep = 96;
    public static CharacterState Enroll(CharacterState s)
    {
        if(s.World is not null)return s;
        if(s.RunId!=Guid.Empty&&s.CheckpointRoom<JourneyRules.Length(s))throw new InvalidOperationException("Finish the saved legacy journey before entering the connected campaign");
        return s with { World=new() { GeographyVersion=2,LegacyRewardsSuppressed=s.RunId!=Guid.Empty&&s.CheckpointRoom==JourneyRules.Length(s) } };
    }
    public static CharacterState UpgradeGeography(CharacterState s)
    {
        var w=Require(s);if(w.GeographyVersion>=2)return s;
        ZoneDefinition Latest(ZoneDefinition z)=>WorldContent.Zones.GetValueOrDefault(z.Id,z);
        WorldPoint Position(ZoneDefinition old,WorldPoint p)
        {var latest=Latest(old);return new(p.X*latest.Geometry.Bounds.Width/old.Geometry.Bounds.Width,p.Y*latest.Geometry.Bounds.Height/old.Geometry.Bounds.Height);}
        var fog=w.Fog;
        foreach(var (id,cells) in w.Fog.Where(p=>p.Key.EndsWith(".0",StringComparison.Ordinal)))
        {
            var expanded=ImmutableHashSet<int>.Empty;
            foreach(var cell in cells)
                for(var dy=0;dy<3;dy++)for(var dx=0;dx<3;dx++)expanded=expanded.Add((cell/20*3+dy)*60+cell%20*3+dx);
            fog=fog.SetItem(id,expanded);
        }
        return s with {World=w with {GeographyVersion=2,ActiveZone=Latest(w.ActiveZone),Arrival=Position(w.ActiveZone,w.Arrival),
            SafeSnapshot=Latest(w.SafeSnapshot),SafePosition=Position(w.SafeSnapshot,w.SafePosition),Fog=fog}};
    }
    public static bool Satisfied(WorldProgress w,string gate) => gate switch
    {
        ""=>true, "ash.resolved"=>w.Resolved.Contains(Region.Ash),
        "crown.open"=>w.Resolved.Contains(Region.Glass)&&w.Resolved.Contains(Region.Hollow),
        _ when gate.EndsWith(".ready",StringComparison.Ordinal)=>w.Claims.Contains(gate[..^6]+".device.0")&&w.Claims.Contains(gate[..^6]+".device.1")&&w.Claims.Contains(gate.Split('.')[0]+".1.boss"),
        _=>FrontierWorld.Claims(w).Contains(gate)
    };
    public static string GateReason(string gate) => gate switch
    {
        "ash.resolved"=>"Restore Ash Foundry first.", "crown.open"=>"Resolve Glass Marsh and Hollow Archive first.",
        _ when gate.EndsWith(".ready",StringComparison.Ordinal)=>"Restore both conduits and defeat the regional enforcer.", _=>"Clear the nearby defenders first."
    };
    public static bool ExitOpen(WorldProgress w,TravelLink link) => Satisfied(w,link.Requires)&& (w.ActiveZone.Kind!="boss"||w.ActiveZone.Encounters.All(e=>w.Claims.Contains(e.Id)));
    public static CharacterState Travel(CharacterState s,string exitId)
    {
        var w=Require(s);var exit=w.ActiveZone.Exits.SingleOrDefault(e=>e.Id==exitId)??throw new InvalidOperationException("Stale exit");
        if(!ExitOpen(w,exit))throw new InvalidOperationException(GateReason(exit.Requires));
        return Enter(s,exit.Destination,exit.Arrival);
    }
    public static CharacterState Enter(CharacterState s,string zoneId,WorldPoint arrival)
    {
        var w=Require(s);
        if(FrontierWorld.TryDepth(zoneId,out var depth))
        {
            var frontier=FrontierWorld.Visit(w.Frontier,depth);var wilderness=FrontierWorld.Generate(w.CampaignId,depth);
            if(!wilderness.Geometry.Navigation().Clear(arrival.Vector,arrival.Vector,20))throw new InvalidOperationException("Unsafe frontier arrival");
            return s with {World=w with {Frontier=frontier,ActiveZone=wilderness,Arrival=arrival}};
        }
        var zone=WorldContent.Zone(zoneId);
        if(!zone.Geometry.Navigation().Clear(arrival.Vector,arrival.Vector,20))throw new InvalidOperationException("Unsafe zone arrival");
        return s with { World=w with { ActiveZone=zone,Arrival=arrival,Discovered=w.Discovered.Add(zoneId),Visited=w.Visited.Add(zoneId) } };
    }
    public static CharacterState Resume(CharacterState s)
    {var w=Require(s);return s with {World=w with {ActiveZone=w.SafeSnapshot,Arrival=w.SafePosition}};}
    public static CharacterState Park(CharacterState s,WorldPoint position)
    {
        var w=Require(s);
        return w.ActiveZone.Geometry.Navigation().Clear(position.Vector,position.Vector,8)?s with {World=w with {Arrival=position}}:s;
    }
    public static ImmutableHashSet<int> ZoneFog(WorldProgress w)
        =>FrontierWorld.TryDepth(w.ActiveZone.Id,out var depth)?w.Frontier.Surveys.GetValueOrDefault(depth,new()).Fog:w.Fog.GetValueOrDefault(w.ActiveZone.Id,[]);
    public static CharacterState SaveFog(CharacterState s,ImmutableHashSet<int> cells)
    {
        var w=Require(s);
        if(!FrontierWorld.TryDepth(w.ActiveZone.Id,out var depth))return s with {World=w with {Fog=w.Fog.SetItem(w.ActiveZone.Id,cells)}};
        if(depth<=w.Frontier.RetiredThrough)return s;
        var survey=w.Frontier.Surveys.GetValueOrDefault(depth,new());
        return s with {World=w with {Frontier=w.Frontier with {Surveys=w.Frontier.Surveys.SetItem(depth,survey with {Fog=cells})}}};
    }
    public static CharacterState Reveal(CharacterState s,WorldPoint position)
    {
        var w=Require(s);if(!w.ActiveZone.Geometry.Contains(position))return s;
        var cells=ZoneFog(w);var before=cells.Count;
        var columns=(w.ActiveZone.Geometry.Bounds.Width+FogStep-1)/FogStep;var rows=(w.ActiveZone.Geometry.Bounds.Height+FogStep-1)/FogStep;
        var x=(int)position.X/FogStep;var y=(int)position.Y/FogStep;
        for(var dy=-2;dy<=2;dy++)for(var dx=-2;dx<=2;dx++)if(x+dx>=0&&x+dx<columns&&y+dy>=0&&y+dy<rows)cells=cells.Add((y+dy)*columns+x+dx);
        return before==cells.Count?s:SaveFog(s,cells);
    }
    public static CharacterState ClaimEncounter(CharacterState s,Guid campaignId,string zoneId,string encounterId)
    {
        var w=Require(s);if(w.CampaignId!=campaignId||w.ActiveZone.Id!=zoneId)throw new InvalidOperationException("Stale world encounter receipt");
        if(FrontierWorld.Claims(w).Contains(encounterId))return s;
        var e=w.ActiveZone.Encounters.SingleOrDefault(e=>e.Id==encounterId)??throw new InvalidOperationException("Unknown encounter");
        if(FrontierWorld.TryDepth(zoneId,out _))return ClaimFrontier(s,e.Id,e.Reward);
        var result=Reward(s,e.Reward,e.Id);w=result.World! with { Claims=w.Claims.Add(e.Id) };
        if(e.Boss==1)
        {
            w=w with { Resolved=w.Resolved.Add(w.ActiveZone.Region),Discovered=w.Discovered.Union(w.ActiveZone.Region==Region.Ash?["glass.0","hollow.0"]:Array.Empty<string>()) };
            if(w.Resolved.Contains(Region.Glass)&&w.Resolved.Contains(Region.Hollow))w=w with { Discovered=w.Discovered.Add("crown.0") };
            result=result with { SkillMilestones=result.SkillMilestones.Add(Math.Min(3,(int)w.ActiveZone.Region)),FractureUnlocked=result.FractureUnlocked||w.ActiveZone.Region==Region.Crown };
        }
        return result with { World=w,Journal=[..result.Journal.TakeLast(31),e.Boss==1?$"{WorldContent.RegionNames[(int)w.ActiveZone.Region]} restored. Its landscape and travel routes change.":$"{w.ActiveZone.Name}: defenders cleared; rewards banked."] };
    }
    public static CharacterState Interact(CharacterState s,string siteId)
    {
        var w=Require(s);var site=w.ActiveZone.Sites.SingleOrDefault(p=>p.Id==siteId)??throw new InvalidOperationException("Stale interaction");
        if(!Satisfied(w,site.Requires))throw new InvalidOperationException(GateReason(site.Requires));
        if(site.Kind is "npc" or "lore")return s;
        if(FrontierWorld.TryDepth(w.ActiveZone.Id,out var depth))
        {
            var frontierResult=ClaimFrontier(s,site.Id,site.Reward??new());var next=frontierResult.World!;
            if(site.Kind is "waypoint" or "refuge")next=next with {SafeZone=next.ActiveZone.Id,SafeSnapshot=next.ActiveZone,SafePosition=site.Position,Frontier=next.Frontier with {LastCamp=depth}};
            return AdventureRules.ObserveContract(frontierResult with {World=next});
        }
        if(site.Kind=="shortcut")return Enter(s,"hearth",new(640,430));
        if(w.Claims.Contains(siteId)&&site.Reward is not null)return s;
        var result=site.Reward is {} reward?Reward(s,reward,siteId):s;w=result.World! with { Claims=w.Claims.Add(siteId) };
        if(site.Kind is "waypoint" or "refuge")w=w with { Waypoints=w.Waypoints.Add(siteId),SafeZone=w.ActiveZone.Id,SafeSnapshot=w.ActiveZone,SafePosition=site.Position };
        return result with { World=w,Journal=[..result.Journal.TakeLast(31),$"{site.Name}: {(site.Kind=="refuge"?"survivors return; waypoint and shortcut restored":site.Kind=="device"?"conduit restored":"checkpoint saved")}."] };
    }
    public static CharacterState FastTravel(CharacterState s,string waypoint)
    {
        var w=Require(s);var target=AvailableWaypoints(w).FirstOrDefault(p=>p.Site.Id==waypoint);
        if(target.Site is null)throw new InvalidOperationException("Activate that safe waypoint first");
        var entered=Enter(s,target.Zone.Id,target.Site.Position);w=entered.World!;
        return entered with { World=w with {SafeZone=target.Zone.Id,SafeSnapshot=target.Zone,SafePosition=target.Site.Position} };
    }
    public static IEnumerable<(ZoneDefinition Zone,WorldSite Site)> AvailableWaypoints(WorldProgress w)
    {
        foreach(var zone in WorldContent.Zones.Values)
            foreach(var site in zone.Sites.Where(p=>w.Waypoints.Contains(p.Id)))yield return (zone,site);
        if(w.Frontier.LastCamp>=0)
        {var zone=FrontierWorld.Generate(w.CampaignId,w.Frontier.LastCamp);yield return (zone,zone.Sites.Single(s=>s.Kind=="waypoint"));}
    }
    private static CharacterState ClaimFrontier(CharacterState s,string id,WorldReward reward)
    {
        var w=Require(s);FrontierWorld.TryDepth(w.ActiveZone.Id,out var depth);
        if(FrontierWorld.Claims(w).Contains(id))return s;
        var result=Reward(s,reward,id);w=result.World!;var survey=w.Frontier.Surveys[depth];
        return AdventureRules.ObserveContract(result with {World=w with {Frontier=w.Frontier with {Surveys=w.Frontier.Surveys.SetItem(depth,survey with {Claims=survey.Claims.Add(id)})}},
            Journal=[..result.Journal.TakeLast(31),$"{w.ActiveZone.Name}: {id.Split('.').Last()} secured; rewards banked."]});
    }
    public static CharacterState End(CharacterState s,string choice)
    {
        var w=Require(s);if(!w.Resolved.Contains(Region.Crown)||choice is not ("preserve" or "bind"))throw new InvalidOperationException("The First Pattern still rules");
        if(w.Ending is not null)return s;
        return s with { World=w with { Ending=choice },Journal=[..s.Journal.TakeLast(31),choice=="preserve"?"The Manyborn preserves contradictions. Mara rebuilds, Iven carries survivors, Sen remembers every name.":"The Manyborn binds a gentler Pattern. Its first law preserves every name; its roads remain open."] };
    }
    private static CharacterState Reward(CharacterState s,WorldReward r,string receipt)
    {
        if(s.World!.LegacyRewardsSuppressed&&s.World.ActiveZone.Kind!="frontier")return s;
        if(s.World.Adventure is not null)return AdventureRules.Reward(s,r,receipt);
        var gold=r.Gold;if(s.Inscriptions.Contains("field-notes"))gold=gold*110/100;
        var alloy=r.Alloy+(r.Item>=0&&s.Inscriptions.Contains("salvagers-mark")?1:0);var inventory=s.Inventory;
        if(r.Item>=0)
        {
            var template=EquipmentRules.StarterItems.Single(i=>i.Slot==(GearSlot)(r.Item%6));
            var item=template with { Id=new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"{s.World.CampaignId:N}:{receipt}"))[..16]),Name="Restored "+template.Slot,Band=2,BaseValue=template.BaseValue+8+r.Item*2 };
            if(inventory.Length>=EquipmentRules.Capacity){gold+=25;alloy+=5;}else inventory=inventory.Add(item);
        }
        return FoundationRules.AddXp(s with {Gold=s.Gold+gold,Alloy=s.Alloy+alloy,Inventory=inventory},r.Xp);
    }
    private static WorldProgress Require(CharacterState s)=>s.World??throw new InvalidOperationException("This character uses the legacy journey");
    public static void Validate(WorldProgress w)
    {
        WorldContent.ValidateStructure(w.ActiveZone);
        WorldContent.ValidateStructure(w.SafeSnapshot);
        FrontierWorld.Validate(w);
        if(w.Version!=WorldContent.Version||w.GeographyVersion is <1 or >2||w.CampaignId==Guid.Empty||w.ActiveZone is null||w.SafeSnapshot is null||w.SafeSnapshot.Id!=w.SafeZone
            ||!WorldContent.Zones.ContainsKey(w.ActiveZone.Id)&&!FrontierWorld.TryDepth(w.ActiveZone.Id,out _)
            ||!WorldContent.Zones.ContainsKey(w.SafeZone)&&!FrontierWorld.TryDepth(w.SafeZone,out _)
            ||w.Discovered is null||w.Visited is null||w.Waypoints is null||w.Claims is null||w.Resolved is null||w.Fog is null
            ||w.Claims.Count>256||w.Waypoints.Count>32||w.Fog.Count>17||w.Resolved.Any(r=>!Enum.IsDefined(r))||w.Ending is not (null or "preserve" or "bind")
            ||w.Ending is not null&&!w.Resolved.Contains(Region.Crown)||!w.Visited.IsSubsetOf(w.Discovered)
            ||w.Discovered.Any(id=>!WorldContent.Zones.ContainsKey(id))||!w.ActiveZone.Geometry.Contains(w.Arrival,7)
            ||!w.SafeSnapshot.Geometry.Navigation().Clear(w.SafePosition.Vector,w.SafePosition.Vector,20)
            ||w.Fog.Any(pair=>!WorldContent.Zones.ContainsKey(pair.Key)||pair.Value is null||pair.Value.Count>8192||pair.Value.Any(c=>c<0||c>=8192))
            ||w.Waypoints.Any(id=>!WorldContent.Zones.Values.Any(z=>z.Sites.Any(p=>p.Id==id&&(p.Kind is "waypoint" or "refuge")))))
            throw new InvalidDataException("Invalid persistent world state");
    }
}
