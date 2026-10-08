using System.Collections.Immutable;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Overload.Domain;

public sealed record MapEdge(int A,int B);
public sealed record FractureMapRun(string GeneratorVersion,ZoneDefinition Zone,ImmutableArray<MapEdge> Edges,
    ImmutableHashSet<string> Required,ImmutableHashSet<string> Claims,string Digest,bool Completed=false)
{
    public ImmutableHashSet<int> Fog { get; init; }=[];
}

/// <summary>SplitMix64 consumes the complete 64-bit seed. Geometry and graph are frozen together.</summary>
public static class FractureMapGenerator
{
    public const string Version="maps.v1";
    private struct Random64(ulong seed)
    {
        private ulong state=seed;
        public ulong Next(){unchecked{var z=(state+=0x9E3779B97F4A7C15UL);z=(z^(z>>30))*0xBF58476D1CE4E5B9UL;z=(z^(z>>27))*0x94D049BB133111EBUL;return z^(z>>31);}}
        public int Pick(int count)=>(int)(Next()%(ulong)count);
    }
    public static WorldPoint Center(int node)=>new(node%3*640+320,node/3*448+224);
    public static FractureMapRun Generate(ulong seed,Region region,ActivityFamily activity)
    {
        var rng=new Random64(seed);var candidates=new List<MapEdge>();
        for(var i=0;i<9;i++){if(i%3<2)candidates.Add(new(i,i+1));if(i/3<2)candidates.Add(new(i,i+3));}
        for(var i=candidates.Count-1;i>0;i--){var j=rng.Pick(i+1);(candidates[i],candidates[j])=(candidates[j],candidates[i]);}
        var parents=Enumerable.Range(0,9).ToArray();int Root(int x){while(parents[x]!=x)x=parents[x];return x;}
        var edges=new List<MapEdge>();
        foreach(var e in candidates)if(Root(e.A)!=Root(e.B)){parents[Root(e.A)]=Root(e.B);edges.Add(e);}
        var spare=candidates.Except(edges).ToList();var loops=1+rng.Pick(3);
        edges.AddRange(spare.Take(loops));
        var distance=Enumerable.Repeat(-1,9).ToArray();distance[3]=0;var queue=new Queue<int>();queue.Enqueue(3);
        while(queue.TryDequeue(out var a))foreach(var b in edges.Where(e=>e.A==a||e.B==a).Select(e=>e.A==a?e.B:e.A))if(distance[b]<0){distance[b]=distance[a]+1;queue.Enqueue(b);}
        var order=Enumerable.Range(0,9).Where(i=>i!=3).OrderByDescending(i=>distance[i]).ThenBy(i=>(i+(int)(seed%9))%9).ToArray();
        var boss=order[0];var guardians=order.Skip(1).Take(activity==ActivityFamily.Hunt?1:2).ToArray();
        var floors=new List<RoomBlock>();
        for(var i=0;i<9;i++)floors.Add(new(i%3*640+64,i/3*448+64,512,320));
        foreach(var e in edges)
        {
            var a=Center(e.A);var b=Center(e.B);
            floors.Add(e.B-e.A==1?new((int)a.X,(int)a.Y-64,640,128):new((int)a.X-64,(int)a.Y,128,448));
        }
        // Merge solid grid runs vertically: graph ports are real 128-pixel-wide traversable corridors.
        var blocks=new List<RoomBlock>();
        for(var y=0;y<1344;y+=32)
        {
            int? start=null;
            for(var x=0;x<=1920;x+=32)
            {
                var solid=x<1920&&!floors.Any(b=>x>=b.X&&x+32<=b.X+b.Width&&y>=b.Y&&y+32<=b.Y+b.Height);
                if(solid){start??=x;continue;}if(start is not {} sx)continue;
                var previous=blocks.FindLastIndex(b=>b.X==sx&&b.Width==x-sx&&b.Y+b.Height==y);
                if(previous>=0)blocks[previous]=blocks[previous] with {Height=blocks[previous].Height+32};else blocks.Add(new(sx,y,x-sx,32));start=null;
            }
        }
        var required=ImmutableHashSet.CreateBuilder<string>();required.Add("map.boss");
        var encounters=new List<WorldEncounter>();var sites=new List<WorldSite>();
        var optional=order.Where(i=>i!=boss&&!guardians.Contains(i)).Take(2).ToArray();
        for(var i=0;i<9;i++)
        {
            if(i==3)continue;
            var id=i==boss?"map.boss":$"map.guard.{i}";
            if(guardians.Contains(i))required.Add(activity==ActivityFamily.Hunt?id:$"map.device.{i}");
            encounters.Add(new(id,Center(i),[(i+(int)region)%6,(i+2)%6,(i+3)%6],new(),Boss:i==boss?(int)(seed%2):-1,Optional:!guardians.Contains(i)&&i!=boss));
            if(guardians.Contains(i)&&activity!=ActivityFamily.Hunt)sites.Add(new($"map.device.{i}",activity==ActivityFamily.Breach?"Breach conduit":"Vault guardian seal",new(Center(i).X+80,Center(i).Y),"device","Restore this objective after its defenders fall.",id));
            if(optional.Contains(i))sites.Add(new($"map.cache.{i}","Optional supply cache",new(Center(i).X-90,Center(i).Y+75),"cache","Bank a portion of the expedition budget early. The remaining payout is shown at extraction.",id));
        }
        if(activity==ActivityFamily.Vault){required.Add("map.keystone");sites.Add(new("map.keystone","Vault keystone",new(Center(boss).X+90,Center(boss).Y),"device","Claim the keystone after both guardian seals and the boss.","map.boss"));}
        var zone=new ZoneDefinition("fracture.map",WorldContent.RegionNames[(int)region]+" Fracture",region,"fracture",new(new(0,0,1920,1344),[..blocks]),Center(3),
            [new("extract","Extract to Hearth",new(Center(3).X-100,Center(3).Y),"hearth",new(640,430))],[..encounters],[..sites],activity switch{ActivityFamily.Hunt=>"Hunt the marked elite, then defeat the regional boss.",ActivityFamily.Breach=>"Restore two defended conduits, then defeat the regional boss.",_=>"Open both guardian seals, defeat the boss, and claim its keystone."});
        return new(Version,zone,[..edges],required.ToImmutable(),[],Digest(zone,[..edges],required.ToImmutable()),false);
    }
    public static string Digest(ZoneDefinition z)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(z))));
    public static string Digest(ZoneDefinition z,ImmutableArray<MapEdge> edges,ImmutableHashSet<string> required)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {Zone=z,Edges=edges.OrderBy(e=>e.A).ThenBy(e=>e.B),Required=required.Order()}))));
}

public static class FractureMapRules
{
    public const string Version="fracture.v4";
    public static CharacterState Begin(CharacterState s,BigInteger tier,Guid id,Region region,ActivityFamily activity,string? mutation=null,bool anomaly=false,bool assisted=false)
    {
        var begun=EndgameRules.Begin(s,tier,id,region,activity,mutation,anomaly,assisted,false);var run=begun.Fracture!;
        var seed=EndlessRules.Seed(tier,id,$"{run.RouteId}|{region}|{activity}|{(anomaly?run.Mutation:null)}",Version);
        return begun with {Fracture=run with {ContentVersion=Version,Seed=seed,Rooms=[],Layout=null,Map=FractureMapGenerator.Generate(seed,region,activity)}};
    }
    public static BigInteger Xp(Expedition run,string claim)
    {
        var m=run.Map!;var main=run.XpBudget*8/10;var cache=run.XpBudget-main;
        if(claim.StartsWith("map.cache.",StringComparison.Ordinal))
        {var caches=m.Zone.Sites.Where(s=>s.Kind=="cache").OrderBy(s=>s.Id).ToArray();return claim==caches[^1].Id?cache-cache/2:cache/2;}
        if(!m.Required.Contains(claim))return 0;
        var ids=m.Required.Order().ToArray();var part=main/ids.Length;return claim==ids[^1]?main-part*(ids.Length-1):part;
    }
    public static CharacterState Claim(CharacterState s,Guid id,string claim)
    {
        var run=s.Fracture??throw new InvalidOperationException("Missing expedition");var map=run.Map??throw new InvalidOperationException("Legacy expedition");
        if(run.Id!=id||!map.Zone.Encounters.Any(e=>e.Id==claim)&&!map.Zone.Sites.Any(p=>p.Id==claim))throw new InvalidOperationException("Stale map receipt");
        if(map.Claims.Contains(claim))return s;if(run.Completed)throw new InvalidOperationException("Expedition already extracted");
        if(map.Zone.Sites.SingleOrDefault(p=>p.Id==claim) is {} site&&!map.Claims.Contains(site.Requires))throw new InvalidOperationException("Defeat the objective's defenders first");
        if(claim=="map.keystone"&&!map.Required.Where(id=>id!="map.keystone").All(map.Claims.Contains))throw new InvalidOperationException("Open both guardian seals first");
        var updated=FoundationRules.AddXp(s,Xp(run,claim));map=map with {Claims=map.Claims.Add(claim)};
        updated=updated with {Fracture=run with {Map=map}};
        if(!map.Required.All(map.Claims.Contains))return updated;
        updated=updated with {Fracture=updated.Fracture! with {Map=map with {Completed=true}}};
        return EndlessRules.Finish(updated,updated.Fracture!);
    }
    public static void Validate(CharacterState s,Expedition run)
    {
        var m=run.Map;
        if(m is null)throw new InvalidDataException("Missing physical map snapshot");
        WorldContent.ValidateStructure(m.Zone);
        if(!s.FractureUnlocked||run.Id==Guid.Empty||run.Sequence!=s.ExpeditionSequence||run.Sequence<1||run.Tier<1||run.Tier>s.HighestUnlockedTier
            ||!Enum.IsDefined(run.Region)||!Enum.IsDefined(run.Activity)||!EndlessRules.Routes.Any(r=>r.Id==run.RouteId)
            ||run.Seed!=EndlessRules.Seed(run.Tier,run.Id,$"{run.RouteId}|{run.Region}|{run.Activity}|{(run.Anomaly?run.Mutation:null)}",Version)
            ||run.XpBudget!=EndlessRules.ExpeditionXp(run.Tier)||run.GoldBudget!=200*run.Tier||run.AlloyBudget!=100*run.Tier||run.ElapsedTicks<0
            ||!run.Rooms.IsEmpty||run.Layout is not null||run.NextGroup!=0||run.Claims.Count!=0||m is null||m.GeneratorVersion!=FractureMapGenerator.Version
            ||m.Edges.IsDefault||m.Edges.Length is <9 or >11||m.Claims is null||m.Required is null||m.Fog is null||m.Fog.Any(c=>c<0||c>=280)||!m.Required.Contains("map.boss")||m.Zone.Region!=run.Region||m.Zone.Kind!="fracture"||m.Zone.Id!="fracture.map"
            ||m.Claims.Concat(m.Required).Any(id=>!m.Zone.Encounters.Any(e=>e.Id==id)&&!m.Zone.Sites.Any(p=>p.Id==id))||m.Completed!=m.Required.All(m.Claims.Contains)
            ||m.Digest!=FractureMapGenerator.Digest(m.Zone,m.Edges,m.Required)||m.Required.Count!=(run.Activity==ActivityFamily.Hunt?2:run.Activity==ActivityFamily.Breach?3:4)
            ||run.WorldRules.IsDefault||!run.WorldRules.SequenceEqual(WorldLaws.ForRoute(run.RouteId,EndlessRules.ChapterAt(run.Tier)))
            ||run.Mutation is null||!WorldLaws.Mutations.Contains(run.Mutation)||run.Completed&&s.HighestClearedTier<run.Tier
            ||run.ChainId is null&&run.ChainLeg!=0||run.ChainId is not null&&(run.ChainLeg is <1 or >3||s.Chain is not {} c||c.Id!=run.ChainId||c.Tier!=run.Tier||c.Regions[run.ChainLeg-1]!=run.Region||c.CompletedLegs!=(run.Completed?run.ChainLeg:run.ChainLeg-1)||!run.Completed&&c.Closed))
            throw new InvalidDataException("Invalid frozen Fracture map or objective checkpoint");
        WorldContent.ValidateStructure(m.Zone);
        var nav=m.Zone.Geometry.Navigation();
        if(m.Edges.Distinct().Count()!=m.Edges.Length||m.Edges.Any(e=>e.A is <0 or >8||e.B is <0 or >8||e.B-e.A!=3&&(e.B-e.A!=1||e.A/3!=e.B/3)
            ||!nav.Clear(FractureMapGenerator.Center(e.A).Vector,FractureMapGenerator.Center(e.B).Vector,20)))throw new InvalidDataException("Graph links disagree with physical ports");
    }
}
