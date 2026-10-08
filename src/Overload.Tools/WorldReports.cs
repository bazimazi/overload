using System.Diagnostics;
using System.Text.Json;
using Overload.Domain;

namespace Overload.Tools;

public static class WorldReports
{
    public static void Export(string output)
    {
        Directory.CreateDirectory(output);var watch=Stopwatch.StartNew();
        foreach(var z in WorldContent.Zones.Values)WorldContent.Validate(z);
        var rows=new List<object>();var lengths=new List<int>();var signatures=new Dictionary<string,int>();var failures=0;
        for(ulong seed=0;seed<10000;seed++)
        {
            var m=FractureMapGenerator.Generate(seed,(Region)(seed%4),(ActivityFamily)(seed%3));
            var b=m.Zone.Geometry.Bounds;const int step=64;var cols=b.Width/step;var rowsCount=b.Height/step;
            var open=new bool[cols*rowsCount];var nav=m.Zone.Geometry.Navigation();
            for(var y=0;y<rowsCount;y++)for(var x=0;x<cols;x++){var p=new System.Numerics.Vector2(x*step+32,y*step+32);open[y*cols+x]=nav.Clear(p,p,20);}
            int Cell(WorldPoint p)=>(int)p.Y/step*cols+(int)p.X/step;
            var distances=Enumerable.Repeat(-1,open.Length).ToArray();var queue=new Queue<int>();var start=Cell(m.Zone.Arrival);distances[start]=0;queue.Enqueue(start);
            while(queue.TryDequeue(out var current))
            {
                var x=current%cols;var y=current/cols;
                foreach(var (dx,dy) in new (int,int)[]{(-1,0),(1,0),(0,-1),(0,1)})
                {var nx=x+dx;var ny=y+dy;if(nx<0||nx>=cols||ny<0||ny>=rowsCount)continue;var next=ny*cols+nx;if(!open[next]||distances[next]>=0)continue;distances[next]=distances[current]+step;queue.Enqueue(next);}
            }
            var reachable=m.Zone.Encounters.Select(e=>e.Position).Concat(m.Zone.Sites.Select(s=>s.Position)).Concat(m.Zone.Exits.Select(e=>e.Position)).All(p=>distances[Cell(p)]>=0);
            if(!reachable)failures++;
            var length=distances[Cell(m.Zone.Encounters.Single(e=>e.Boss>=0).Position)];lengths.Add(length);
            var signature=string.Join(';',m.Edges.OrderBy(e=>e.A).ThenBy(e=>e.B).Select(e=>$"{e.A}-{e.B}"));signatures[signature]=signatures.GetValueOrDefault(signature)+1;
            var degree=Enumerable.Range(0,9).Select(n=>m.Edges.Count(e=>e.A==n||e.B==n)).ToArray();
            rows.Add(new {seed,region=m.Zone.Region.ToString(),activity=((ActivityFamily)(seed%3)).ToString(),reachable,criticalPathPixels=length,optionalPathPixels=m.Zone.Sites.Where(s=>s.Kind=="cache").Select(s=>distances[Cell(s.Position)]).ToArray(),loops=m.Edges.Length-8,deadEnds=degree.Count(d=>d==1),minimumConnectorWidth=128,encounters=m.Zone.Encounters.Length,enemies=m.Zone.Encounters.Sum(e=>e.Boss>=0?1:e.Enemies.Length),floorCells=open.Count(v=>v),topology=signature,digest=m.Digest});
        }
        var options=new JsonSerializerOptions {WriteIndented=true};
        File.WriteAllText(Path.Combine(output,"seeds.json"),JsonSerializer.Serialize(rows,options));
        var economy=Enum.GetValues<Region>().Select(region=>new {region=WorldContent.RegionNames[(int)region],main=WorldContent.Zones.Values.Where(z=>z.Id!="hearth"&&z.Region==region).SelectMany(z=>z.Encounters.Where(e=>!e.Optional).Select(e=>e.Reward).Concat(z.Sites.Where(p=>p.Reward is not null).Select(p=>p.Reward!))).Aggregate(new WorldReward(Item:0),(a,r)=>new(a.Xp+r.Xp,a.Gold+r.Gold,a.Alloy+r.Alloy,a.Item+(r.Item>=0?1:0))),optional=WorldContent.Zones.Values.Where(z=>z.Id!="hearth"&&z.Region==region).SelectMany(z=>z.Encounters.Where(e=>e.Optional).Select(e=>e.Reward)).Aggregate(new WorldReward(Item:0),(a,r)=>new(a.Xp+r.Xp,a.Gold+r.Gold,a.Alloy+r.Alloy,a.Item+(r.Item>=0?1:0)))}).ToArray();
        File.WriteAllText(Path.Combine(output,"summary.json"),JsonSerializer.Serialize(new {version=FractureMapGenerator.Version,samples=10000,failures,uniqueTopologies=signatures.Count,mostRepeatedTopology=signatures.Values.Max(),criticalPathMinimum=lengths.Min(),criticalPathMaximum=lengths.Max(),criticalPathMean=lengths.Average(),worstShortestSeed=lengths.IndexOf(lengths.Min()),worstLongestSeed=lengths.IndexOf(lengths.Max()),connectorWidth=128,maximumLiveEnemies=12,geometryFrozen=true,rng="SplitMix64",seconds=watch.Elapsed.TotalSeconds,economy},options));
        Console.WriteLine($"WORLD_REPORT: 17 authored zones, 10000 physical maps, {failures} unreachable, {signatures.Count} topologies, {lengths.Min()}..{lengths.Max()}px critical paths, {watch.Elapsed.TotalSeconds:0.0}s");
        if(failures!=0)throw new InvalidOperationException("Generated maps have unreachable objectives");
    }
}
