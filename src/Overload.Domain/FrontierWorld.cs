using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;

namespace Overload.Domain;

public sealed record FrontierSurvey
{
    public ImmutableHashSet<string> Claims { get; init; } = [];
    public ImmutableHashSet<int> Fog { get; init; } = [];
}

public sealed record FrontierProgress
{
    public string Version { get; init; } = "frontier.v1";
    public long Farthest { get; init; } = -1;
    public long RetiredThrough { get; init; } = -1;
    public long LastCamp { get; init; } = -1;
    public ImmutableDictionary<long, FrontierSurvey> Surveys { get; init; } = ImmutableDictionary<long, FrontierSurvey>.Empty;
}

/// <summary>Deterministic, physically connected wilderness beyond the campaign. No campaign or oath gate.</summary>
public static class FrontierWorld
{
    public const int RetainedSurveys = 32;
    public const int Width = 6144, Height = 3840;
    public static string Id(long depth) => "frontier."+depth.ToString(CultureInfo.InvariantCulture);
    public static bool TryDepth(string id,out long depth)
    {
        depth=-1;
        return id.StartsWith("frontier.",StringComparison.Ordinal)
            && long.TryParse(id.AsSpan(9),NumberStyles.None,CultureInfo.InvariantCulture,out depth) && depth>=0 && depth<long.MaxValue
            && id==Id(depth);
    }
    public static ZoneDefinition Generate(Guid campaign,long depth)
    {
        if(depth<0||depth>=long.MaxValue)throw new ArgumentOutOfRangeException(nameof(depth));
        var bytes=SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"frontier.v1:{campaign:N}:{depth}"));
        ulong seed=System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(bytes);
        int Next(int range)
        {seed+=0x9e3779b97f4a7c15UL;var z=seed;z=(z^(z>>30))*0xbf58476d1ce4e5b9UL;z=(z^(z>>27))*0x94d049bb133111ebUL;return (int)((z^(z>>31))%(ulong)range);}
        var region=(Region)(depth%4);var id=Id(depth);
        string[] names=["Ember Expanse","Shattered Wetlands","Forgotten Marches","Crown Wastes"];
        var blocks=new List<RoomBlock>();
        // Broad east/west and north/south corridors remain clear in every seed.
        for(var row=0;row<4;row++)for(var col=0;col<6;col++)
        {
            if(Next(4)==0)continue;
            var x=680+col*850+Next(160);var y=280+row*850+Next(120);
            blocks.Add(new(x,y,160+Next(160),130+Next(150)));
        }
        var geometry=new LevelGeometry(new(0,0,Width,Height),[..blocks]);
        var encounters=new List<WorldEncounter>();
        var growth=(int)Math.Min(depth,2000);
        for(var row=0;row<3;row++)for(var col=0;col<7;col++)
        {
            var p=new WorldPoint(500+col*800,row==0?850:row==1?1920:3500);
            encounters.Add(new($"{id}.pack.{row*7+col}",p,[Next(6),Next(6),Next(6)],new(100+growth*50,12+growth*4,1),Optional:true));
        }
        encounters.Add(new($"{id}.guardian",new(5520,1920),[],new(700+growth*200,80+growth*10,6,(int)(depth%20)),Boss:0,Optional:true));
        return new(id,$"{names[(int)region]} · Reach {depth+1}",region,"frontier",geometry,new(180,1920),
            [new("back",depth==0?"Hearth Junction":"Previous reach",new(100,1920),depth==0?"hearth":Id(depth-1),depth==0?new(640,700):new(Width-230,1920)),
             new("onward","Uncharted wilderness",new(Width-110,1920),Id(depth+1),new(180,1920)),
             new("home","Hearth return conduit",new(3040,2040),"hearth",new(640,700))],
            [..encounters],
            [new($"{id}.camp","Trailkeeper Camp",new(3000,1920),"waypoint","Rest here to save a frontier checkpoint and unlock its world-map return."),
             new($"{id}.cache.north","Ruined caravan",new(2900,850),"cache","Salvage what the caravan left behind.",$"{id}.pack.3",new(250+growth*60,40,3)),
             new($"{id}.cache.south","Buried reliquary",new(3700,3500),"cache","The lower trail holds a guarded relic.",$"{id}.pack.18",new(250+growth*60,40,3)),
             new($"{id}.landmark","The road beyond the Pattern",new(3050,1100),"lore","Every reach has its own terrain, defenders and camps. Continue east to uncover another vast wilderness, or follow the trail west to retrace your journey.")],
            "Explore the side trails, activate Trailkeeper Camp, and follow the east road into another reach.");
    }
    public static FrontierProgress Visit(FrontierProgress f,long depth)
    {
        if(depth>f.Farthest+1)throw new InvalidOperationException("Reach this wilderness through its connecting road first");
        var farthest=Math.Max(f.Farthest,depth);var retired=Math.Max(f.RetiredThrough,farthest-RetainedSurveys);
        var surveys=f.Surveys.RemoveRange(f.Surveys.Keys.Where(k=>k<=retired));
        if(depth>retired&&!surveys.ContainsKey(depth))surveys=surveys.Add(depth,new());
        return f with {Farthest=farthest,RetiredThrough=retired,Surveys=surveys};
    }
    public static ImmutableHashSet<string> Claims(WorldProgress w)
    {
        if(!TryDepth(w.ActiveZone.Id,out var depth))return w.Claims;
        return depth<=w.Frontier.RetiredThrough
            ? [..w.ActiveZone.Encounters.Select(e=>e.Id),..w.ActiveZone.Sites.Where(s=>s.Kind is not ("lore" or "npc")).Select(s=>s.Id)]
            : w.Frontier.Surveys.GetValueOrDefault(depth,new()).Claims;
    }
    public static void Validate(WorldProgress w)
    {
        var f=w.Frontier;
        if(f is null||f.Version!="frontier.v1"||f.Farthest< -1||f.Farthest>=long.MaxValue||f.RetiredThrough!=Math.Max(-1,f.Farthest-RetainedSurveys)
            ||f.LastCamp< -1||f.LastCamp>f.Farthest||f.Surveys is null||f.Surveys.Count>RetainedSurveys
            ||f.Surveys.Any(p=>p.Key<=f.RetiredThrough||p.Key>f.Farthest||p.Value is null||p.Value.Claims is null||p.Value.Claims.Count>26
                ||p.Value.Claims.Any(c=>!c.StartsWith(Id(p.Key)+".",StringComparison.Ordinal))
                ||p.Value.Fog is null||p.Value.Fog.Any(c=>c<0||c>=Width/WorldRules.FogStep*(Height/WorldRules.FogStep))))
            throw new InvalidDataException("Invalid frontier survey");
        foreach(var zone in new[]{w.ActiveZone,w.SafeSnapshot})
            if(TryDepth(zone.Id,out var depth)&&(depth>f.Farthest||zone.Kind!="frontier"||zone.Geometry.Bounds!=new RoomBlock(0,0,Width,Height)))
                throw new InvalidDataException("Invalid frontier snapshot");
    }
}
