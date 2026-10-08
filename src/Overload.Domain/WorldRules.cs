using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

namespace Overload.Domain;

public sealed record WorldProgress
{
    public string Version { get; init; } = WorldContent.Version;
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
}

public static class WorldRules
{
    public const int FogStep = 96;
    public static CharacterState Enroll(CharacterState s)
    {
        if(s.World is not null)return s;
        if(s.RunId!=Guid.Empty&&s.CheckpointRoom<JourneyRules.Length(s))throw new InvalidOperationException("Finish the saved legacy journey before entering the connected campaign");
        return s with { World=new() { LegacyRewardsSuppressed=s.RunId!=Guid.Empty&&s.CheckpointRoom==JourneyRules.Length(s) } };
    }
    public static bool Satisfied(WorldProgress w,string gate) => gate switch
    {
        ""=>true, "ash.resolved"=>w.Resolved.Contains(Region.Ash),
        "crown.open"=>w.Resolved.Contains(Region.Glass)&&w.Resolved.Contains(Region.Hollow),
        _ when gate.EndsWith(".ready",StringComparison.Ordinal)=>w.Claims.Contains(gate[..^6]+".device.0")&&w.Claims.Contains(gate[..^6]+".device.1")&&w.Claims.Contains(gate.Split('.')[0]+".1.boss"),
        _=>w.Claims.Contains(gate)
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
        var w=Require(s);var zone=WorldContent.Zone(zoneId);
        if(!zone.Geometry.Navigation().Clear(arrival.Vector,arrival.Vector,20))throw new InvalidOperationException("Unsafe zone arrival");
        return s with { World=w with { ActiveZone=zone,Arrival=arrival,Discovered=w.Discovered.Add(zoneId),Visited=w.Visited.Add(zoneId) } };
    }
    public static CharacterState Resume(CharacterState s)
    {var w=Require(s);return s with {World=w with {ActiveZone=w.SafeSnapshot,Arrival=w.SafePosition,Discovered=w.Discovered.Add(w.SafeZone),Visited=w.Visited.Add(w.SafeZone)}};}
    public static CharacterState Reveal(CharacterState s,WorldPoint position)
    {
        var w=Require(s);if(!w.ActiveZone.Geometry.Contains(position))return s;
        var cells=w.Fog.GetValueOrDefault(w.ActiveZone.Id,[]);var before=cells.Count;
        var columns=(w.ActiveZone.Geometry.Bounds.Width+FogStep-1)/FogStep;var rows=(w.ActiveZone.Geometry.Bounds.Height+FogStep-1)/FogStep;
        var x=(int)position.X/FogStep;var y=(int)position.Y/FogStep;
        for(var dy=-2;dy<=2;dy++)for(var dx=-2;dx<=2;dx++)if(x+dx>=0&&x+dx<columns&&y+dy>=0&&y+dy<rows)cells=cells.Add((y+dy)*columns+x+dx);
        return before==cells.Count?s:s with { World=w with { Fog=w.Fog.SetItem(w.ActiveZone.Id,cells) } };
    }
    public static CharacterState ClaimEncounter(CharacterState s,Guid campaignId,string zoneId,string encounterId)
    {
        var w=Require(s);if(w.CampaignId!=campaignId||w.ActiveZone.Id!=zoneId)throw new InvalidOperationException("Stale world encounter receipt");
        if(w.Claims.Contains(encounterId))return s;
        var e=w.ActiveZone.Encounters.SingleOrDefault(e=>e.Id==encounterId)??throw new InvalidOperationException("Unknown encounter");
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
        if(site.Kind=="shortcut")return Enter(s,"hearth",new(640,430));
        if(w.Claims.Contains(siteId)&&site.Kind=="device")return s;
        var result=site.Reward is {} reward?Reward(s,reward,siteId):s;w=result.World! with { Claims=w.Claims.Add(siteId) };
        if(site.Kind is "waypoint" or "refuge")w=w with { Waypoints=w.Waypoints.Add(siteId),SafeZone=w.ActiveZone.Id,SafeSnapshot=w.ActiveZone,SafePosition=site.Position };
        return result with { World=w,Journal=[..result.Journal.TakeLast(31),$"{site.Name}: {(site.Kind=="refuge"?"survivors return; waypoint and shortcut restored":site.Kind=="device"?"conduit restored":"checkpoint saved")}."] };
    }
    public static CharacterState FastTravel(CharacterState s,string waypoint)
    {
        var w=Require(s);if(!w.Waypoints.Contains(waypoint))throw new InvalidOperationException("Activate that safe waypoint first");
        var zone=WorldContent.Zones.Values.Single(z=>z.Sites.Any(p=>p.Id==waypoint));var p=zone.Sites.Single(p=>p.Id==waypoint);
        return s with { World=w with { ActiveZone=zone,Arrival=p.Position,SafeZone=zone.Id,SafeSnapshot=zone,SafePosition=p.Position,Visited=w.Visited.Add(zone.Id),Discovered=w.Discovered.Add(zone.Id) } };
    }
    public static CharacterState End(CharacterState s,string choice)
    {
        var w=Require(s);if(!w.Resolved.Contains(Region.Crown)||choice is not ("preserve" or "bind"))throw new InvalidOperationException("The First Pattern still rules");
        if(w.Ending is not null)return s;
        return s with { World=w with { Ending=choice },Journal=[..s.Journal.TakeLast(31),choice=="preserve"?"The Manyborn preserves contradictions. Mara rebuilds, Iven carries survivors, Sen remembers every name.":"The Manyborn binds a gentler Pattern. Its first law preserves every name; its roads remain open."] };
    }
    private static CharacterState Reward(CharacterState s,WorldReward r,string receipt)
    {
        if(s.World!.LegacyRewardsSuppressed)return s;
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
        if(w.Version!=WorldContent.Version||w.CampaignId==Guid.Empty||w.ActiveZone is null||w.SafeSnapshot is null||w.SafeSnapshot.Id!=w.SafeZone||!WorldContent.Zones.ContainsKey(w.ActiveZone.Id)||!WorldContent.Zones.ContainsKey(w.SafeZone)
            ||w.Discovered is null||w.Visited is null||w.Waypoints is null||w.Claims is null||w.Resolved is null||w.Fog is null
            ||w.Claims.Count>256||w.Waypoints.Count>32||w.Fog.Count>17||w.Resolved.Any(r=>!Enum.IsDefined(r))||w.Ending is not (null or "preserve" or "bind")
            ||w.Ending is not null&&!w.Resolved.Contains(Region.Crown)||!w.Visited.IsSubsetOf(w.Discovered)
            ||w.Discovered.Any(id=>!WorldContent.Zones.ContainsKey(id))||!w.ActiveZone.Geometry.Contains(w.Arrival,20)
            ||!w.SafeSnapshot.Geometry.Navigation().Clear(w.SafePosition.Vector,w.SafePosition.Vector,20)
            ||w.Fog.Any(pair=>!WorldContent.Zones.ContainsKey(pair.Key)||pair.Value is null||pair.Value.Count>2048||pair.Value.Any(c=>c<0||c>=2048))
            ||w.Waypoints.Any(id=>!WorldContent.Zones.Values.Any(z=>z.Sites.Any(p=>p.Id==id&&(p.Kind is "waypoint" or "refuge")))))
            throw new InvalidDataException("Invalid persistent world state");
    }
}
