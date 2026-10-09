using System.Collections.Immutable;

namespace Overload.Domain;

public sealed record WorldReward(int Xp = 0, int Gold = 0, int Alloy = 0, int Item = -1);
public sealed record WorldEncounter(string Id, WorldPoint Position, ImmutableArray<int> Enemies, WorldReward Reward, int Boss = -1, bool Optional = false);
public sealed record WorldSite(string Id, string Name, WorldPoint Position, string Kind, string Text, string Requires = "", WorldReward? Reward = null);
public sealed record TravelLink(string Id, string Name, WorldPoint Position, string Destination, WorldPoint Arrival, string Requires = "");
public sealed record ZoneDefinition(string Id, string Name, Region Region, string Kind, LevelGeometry Geometry, WorldPoint Arrival,
    ImmutableArray<TravelLink> Exits, ImmutableArray<WorldEncounter> Encounters, ImmutableArray<WorldSite> Sites, string Objective);

/// <summary>Authored, versioned geography. Each saved active zone owns its materialized definition.</summary>
public static class WorldContent
{
    public const string Version = "world.v1";
    public static readonly string[] RegionNames = ["Ash Foundry", "Glass Marsh", "Hollow Archive", "Crown Scar"];
    public static readonly string[][] Places = [["Cinderroad", "Bellhouse", "Cooling Works", "Kilnheart"],
        ["Reed Causeway", "Drowned Observatory", "Mirror Galleries", "Widow Basin"],
        ["Scribe Approach", "Index Hall", "Unwritten Wing", "Abbot Vault"],
        ["Procession Road", "Marshal Bastion", "Crown Bridge", "Pattern Chamber"]];
    public static readonly string[] Refuges = ["Sootwake Refuge", "Reedwatch Camp", "House of Names", "The Last Assembly"];
    public static readonly string[] Problems = ["Restore the cooling network without erasing its workers.",
        "Free the causeway from the remembered flood.", "Restore the names removed from the Archive.", "Reach the First Pattern and decide what stability should preserve."];
    public static string Id(Region region, int place) => $"{region.ToString().ToLowerInvariant()}.{place}";
    public static ImmutableDictionary<string, ZoneDefinition> Zones { get; } = Build();
    public static ZoneDefinition Zone(string id) => Zones.TryGetValue(id, out var zone) ? zone : throw new InvalidOperationException("Unknown destination");
    private static ImmutableDictionary<string, ZoneDefinition> Build()
    {
        var zones = new List<ZoneDefinition>();
        var entrances = new WorldPoint[] { new(120,384), new(640,650), new(1160,384), new(640,110) };
        zones.Add(new("hearth", "Hearth Junction", Region.Ash, "hearth", new(new(0,0,1280,768), [new(420,190,140,120),new(800,450,160,110)]), new(640,384),
            [.. Enumerable.Range(0,4).Select(r => new TravelLink($"road.{r}", RegionNames[r], entrances[r], Id((Region)r,0),new(140,560), r==0?"":r==3?"crown.open":"ash.resolved"))], [],
            [new("hearth.waypoint","Hearth Waypoint",new(640,430),"waypoint","The surviving junction. Rest, travel, and return to the forge."),
             new("mara","Mara · Engineer",new(340,360),"npc","The Pattern keeps the kilns running by deleting workers who cannot obey. Follow the west pipe. Restore the flow; bring the workers home."),
             new("iven","Iven · Courier",new(920,350),"npc","The old gantry and service culvert meet at the cooling junction. The lower worker court can become a refuge. Every road has a way back."),
             new("sen","Sen · Archivist",new(730,220),"npc","A name is a promise that someone existed. After Ash, the flooded mirrors and erased Archive can be restored in either order.")], "Follow the west conduit to Ash Foundry."));
        for (var n=0;n<4;n++)
        {
            var r=(Region)n; var id=Id(r,0); var gate=n==0?"":n==3?"crown.open":"ash.resolved";
            var blocks = n switch
            {
                0 => new RoomBlock[] { new(680,400,460,300),new(360,120,200,140),new(1290,780,240,140) },
                1 => [new(580,350,540,390),new(240,760,380,190),new(1260,80,360,150)],
                2 => [new(650,350,500,370),new(320,80,100,260),new(1370,780,100,230),new(1490,100,250,90)],
                _ => [new(640,390,540,280),new(270,790,350,170),new(1360,80,160,170)]
            };
            var wild = new List<WorldEncounter>();
            var positions=new WorldPoint[]{new(400,560),new(610,290),new(1260,290),new(1570,560),new(620,850),new(1210,920),new(980,940)};
            if(n is 1 or 3)positions[4]=new(700,850);
            for(var e=0;e<7;e++) wild.Add(new($"{id}.pack.{e}",positions[e],e==6?[0,3,2]:[e%6,(e+1)%6,(e+2)%6],
                e<4?new(300+n*200,25+n*25,2,e==3?n*4:-1):e==6?new(300,60,4,16+n):new(150),Optional:e>=4));
            var links=new List<TravelLink>{new("home","Hearth Junction",new(120,560),"hearth",entrances[n],gate),
                new("enforcer",Places[n][1],new(1530,300),Id(r,1),new(140,384)),
                new("dungeon",Places[n][2],new(1790,560),Id(r,2),new(140,520))};
            if(n==0) { links.Add(new("aqueduct","Glass Marsh · Cooled aqueduct",new(1670,960),Id(Region.Glass,0),new(140,560),"ash.resolved"));
                links.Add(new("service","Hollow Archive · Service road",new(1790,850),Id(Region.Hollow,0),new(140,560),"ash.resolved")); }
            if(n is 1 or 2) links.Add(new("crown","Crown Scar · Procession road",new(1790,960),Id(Region.Crown,0),new(140,560),"crown.open"));
            zones.Add(new(id,Places[n][0],r,"wild",new(new(0,0,1920,1152),[..blocks]),new(140,560),[..links],[..wild],
                [new($"{id}.refuge",Refuges[n],new(940,1020),"refuge","Reclaim this shelter. Survivors return, a waypoint lights, and a return shortcut opens.",$"{id}.pack.6"),
                 new($"{id}.shortcut","Return service gate",new(200,1010),"shortcut","The refuge's workers reopen the direct passage to Hearth.",$"{id}.refuge"),
                 new($"{id}.landmark",n switch{0=>"The Great Kiln",1=>"The Leaning Mirror",2=>"The Suspended Page Vault",_=>"The Fractured Crown"},new(980,310),"lore",Problems[n]),
                 new($"{id}.route",n==0?"Gantry / service culvert":"The divided road",new(520,560),"lore","The upper route has long sightlines. The lower route has cover and a refuge. Both reconnect at the eastern junction."),
                 new(n==0?$"{id}.mara":n==1?$"{id}.iven":$"{id}.sen",n==0?"Mara · Engineer":n==1?"Iven · Courier":"Sen · Archivist",new(1060,1030),"npc",
                     n switch {0=>"The workers are here because you made room for them. Cold water, warm lights: a refuge the Pattern never planned.",1=>"That causeway leads through the present, not the old flood. We can carry people across it again.",2=>"Every recovered name belongs to someone beyond these walls. The Archive's new wing stays open.",_=>"Stability can begin by preserving people. The First Pattern waits beyond the bridge."},$"{id}.refuge")],Problems[n]));
            zones.Add(Boss(r,n,0)); zones.Add(Boss(r,n,1));
            var dungeonId=Id(r,2);
            var db=n switch {0=>new RoomBlock[]{new(420,220,140,510),new(870,540,160,340)},
                1=>[new(440,360,310,300),new(930,120,160,420)],
                2=>[new(380,180,110,580),new(900,380,110,560),new(600,820,180,100)],
                _=>[new(460,380,210,320),new(890,100,130,420)]};
            zones.Add(new(dungeonId,Places[n][2],r,"dungeon",new(new(0,0,1536,1056),[..db]),new(140,520),
                [new("return",Places[n][0],new(110,520),id,new(1710,560)),new("ruler",Places[n][3],new(1410,520),Id(r,3),new(140,384),$"{dungeonId}.ready")],
                [new($"{dungeonId}.guard.0",new(660,260),[3,2,4],new()),new($"{dungeonId}.guard.1",new(1170,790),[4,5,3],new()),
                 new($"{dungeonId}.vault",new(730,970),[3,3,2],new(300,60,4),Optional:true)],
                [new($"{dungeonId}.device.0",n switch{0=>"Pressure release",1=>"Present-day mirror",2=>"Ledger of names",_=>"Procession anchor"},new(660,160),"device","The machinery changes beside you. Restore the first conduit.",$"{dungeonId}.guard.0",new(800+n*400,75+n*50,4)),
                 new($"{dungeonId}.device.1",n switch{0=>"Cooling valve",1=>"Flood sluice",2=>"Unwritten register",_=>"Bridge stabilizer"},new(1250,850),"device","Restore the second conduit. The ruler's gate also requires the regional enforcer's defeat.",$"{dungeonId}.guard.1",new(800+n*400,75+n*50,4,n*4+2)),
                 new($"{dungeonId}.waypoint","Ruler approach",new(1320,520),"waypoint","A safe approach. Rest before crossing the marked boss gate.")],"Restore both conduits and defeat the regional enforcer to open the ruler's gate."));
        }
        // Outdoor geography now spans 86 camera screens. Combat interiors retain their authored scale.
        var expanded=zones.Select(z=>z.Kind=="wild"?ExpandLandscape(z):z).ToArray();
        WorldPoint Arrival(string destination,WorldPoint p)=>destination.EndsWith(".0",StringComparison.Ordinal)?new(p.X*3,p.Y*3):p;
        return expanded.Select(z=>z with {Exits=[..z.Exits.Select(e=>e with {Arrival=Arrival(e.Destination,e.Arrival)})]})
            .Select(z=>z.Id=="hearth"?z with {Exits=z.Exits.Add(new("frontier","Endless Frontier",new(640,700),FrontierWorld.Id(0),new(180,1920)))}:z)
            .ToImmutableDictionary(z=>z.Id);
    }
    private static ZoneDefinition ExpandLandscape(ZoneDefinition z)
    {
        WorldPoint P(WorldPoint p)=>new(p.X*3,p.Y*3);
        RoomBlock B(RoomBlock b)=>new(b.X*3,b.Y*3,b.Width*3,b.Height*3);
        var packs=z.Encounters.Select(e=>e with {Position=P(e.Position)}).ToList();
        // Place additional optional defenders along both connecting trails, keeping campaign rewards intact.
        var points=new WorldPoint[]{new(280,460),new(420,410),new(780,290),new(1020,290),new(1430,420),new(1680,450),
            new(210,700),new(710,1000),new(1100,1000),new(1350,1000),new(1600,870),new(1760,720)};
        foreach(var p in points)
            packs.Add(new($"{z.Id}.roam.{packs.Count}",P(p),[packs.Count%6,(packs.Count+2)%6],new(90,8,1),Optional:true));
        return z with {Geometry=new(B(z.Geometry.Bounds),[..z.Geometry.Blocks.Select(B)]),Arrival=P(z.Arrival),
            Exits=[..z.Exits.Select(e=>e with {Position=P(e.Position)})],Encounters=[..packs],Sites=[..z.Sites.Select(s=>s with {Position=P(s.Position)})]};
    }
    private static ZoneDefinition Boss(Region r,int n,int boss)
    {
        var place=boss==0?1:3;var id=Id(r,place);var parent=Id(r,boss==0?0:2);
        return new(id,Places[n][place],r,"boss",new(new(0,0,960,768),[new(350,120,110,130),new(350,530,110,130)]),new(140,384),
            [new("return",Places[n][boss==0?0:2],new(100,384),parent,boss==0?new(1450,300):new(1320,520))],
            [new($"{id}.boss",new(650,384),[],new(boss==0?1400+n*800:1800+n*800,boss==0?125+n*100:175+n*100,8,n*4+(boss==0?1:3)),Boss:boss)],
            [new($"{id}.waypoint","Safe threshold",new(160,290),"waypoint","A sealed combat circle lies ahead. The return gate opens when its ruler falls.")],boss==0?"Defeat the enforcer. Its decree seals the regional ruler's gate.":n==3?"Defeat the First Pattern. Preserve contradictions, or bind a gentler Pattern.":Problems[n]);
    }
    public static void Validate(ZoneDefinition z)
    {
        ValidateStructure(z);
        if(string.IsNullOrWhiteSpace(z.Id)||z.Exits.IsDefault||z.Encounters.IsDefault||z.Sites.IsDefault||z.Encounters.Length>24
            ||z.Exits.Select(e=>e.Id).Distinct().Count()!=z.Exits.Length||z.Encounters.Select(e=>e.Id).Distinct().Count()!=z.Encounters.Length)
            throw new InvalidDataException("Invalid world content identity");
        var nav=z.Geometry.Navigation();
        foreach(var p in z.Exits.Select(e=>e.Position).Concat(z.Encounters.Select(e=>e.Position)).Concat(z.Sites.Select(e=>e.Position)).Prepend(z.Arrival))
            if(!nav.Clear(p.Vector,p.Vector,20) || nav.FindPath(z.Arrival.Vector,p.Vector,20).Length==0 && p!=z.Arrival)
                throw new InvalidDataException($"Unreachable anchor in {z.Id}: {p}");
    }
    public static void ValidateStructure(ZoneDefinition z)
    {
        if(z is null||z.Geometry is null||string.IsNullOrWhiteSpace(z.Id)||z.Id.Length>64||string.IsNullOrWhiteSpace(z.Name)||z.Name.Length>128
            ||z.Objective is null||z.Objective.Length>600||!Enum.IsDefined(z.Region)||z.Kind is not ("hearth" or "wild" or "dungeon" or "boss" or "fracture" or "frontier")
            ||z.Encounters.IsDefault||z.Encounters.Length>24||z.Sites.IsDefault||z.Sites.Length>24||z.Exits.IsDefault||z.Exits.Length>8
            ||z.Encounters.Any(e=>e is null||string.IsNullOrWhiteSpace(e.Id)||e.Id.Length>64||e.Enemies.IsDefault||e.Enemies.Length>3||e.Enemies.Any(i=>i is <0 or >5)||e.Boss is <-1 or >1||e.Reward is null||e.Reward.Xp<0||e.Reward.Gold<0||e.Reward.Alloy<0||e.Reward.Item is <-1 or >19)
            ||z.Sites.Any(p=>p is null||string.IsNullOrWhiteSpace(p.Id)||p.Name is null||p.Text is null||p.Requires is null||p.Kind is not ("npc" or "lore" or "refuge" or "shortcut" or "device" or "waypoint" or "cache"))
            ||z.Exits.Any(e=>e is null||string.IsNullOrWhiteSpace(e.Id)||e.Name is null||e.Destination is null||e.Requires is null)
            ||z.Encounters.Select(e=>e.Id).Distinct().Count()!=z.Encounters.Length||z.Sites.Select(p=>p.Id).Distinct().Count()!=z.Sites.Length||z.Exits.Select(e=>e.Id).Distinct().Count()!=z.Exits.Length)
            throw new InvalidDataException("Invalid materialized zone references or entity budgets");
        z.Geometry.Validate();
    }
}
