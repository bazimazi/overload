using System.Collections.Immutable;

namespace Overload.Domain;

public sealed record RoomBlock(int X,int Y,int Width,int Height);
public sealed record GeneratedLayout(string Version, ulong Seed, ImmutableArray<int> Rooms, ImmutableArray<ImmutableArray<RoomBlock>> Obstacles,
    ImmutableArray<LayoutNode> Nodes, bool Fallback);
public sealed record LayoutNode(string Kind,int Room,ImmutableArray<int> Links);

public static class ExpeditionGenerator
{
    public const string Version="rooms.v1";
    // 32px sockets and a 48px-wide central escape lane remain clear in every room.
    public static GeneratedLayout Generate(ulong seed, int attempts=4)
    {
        for(var attempt=0;attempt<Math.Clamp(attempts,0,4);attempt++)
        {
            var random=new Random(unchecked((int)(seed^(seed>>32)))+attempt);
            var rooms=EndlessRules.RoomsFor(seed);
            var blocks=ImmutableArray.CreateBuilder<ImmutableArray<RoomBlock>>();
            for(var i=0;i<7;i++)
            {
                if(i==6) { blocks.Add([]);continue; }
                var x=240+random.Next(0,5)*24;
                blocks.Add([new(x,100,24,40),new(x+80,245,24,35)]);
            }
            var nodes=Graph(seed);
            var result=new GeneratedLayout(Version,seed,rooms,blocks.ToImmutable(),nodes,false);
            if(Connected(result)) return result;
        }
        return new(Version,seed,EndlessRules.RoomsFor(seed),[..Enumerable.Range(0,7).Select(_=>ImmutableArray<RoomBlock>.Empty)],Graph(seed),true);
    }
    private static ImmutableArray<LayoutNode> Graph(ulong seed)
    {
        // Start -> three combat branches -> checkpoint -> boss -> exit, with an optional reward spur.
        return [new("Start",-1,[1,2,3]),new("Combat",0,[4]),new("Combat",1,[4]),new("Combat",2,[4]),new("Checkpoint",3,[5,6]),new("Combat",4,[7]),new("Reward",5,[7]),new("Boss",6,[8]),new("Exit",-1,[])];
    }
    public static bool Connected(GeneratedLayout layout)
    {
        if(layout.Version is not (Version or RegionalContent.GeneratorVersion) || layout.Rooms.Length!=7 || layout.Obstacles.Length!=7 || layout.Nodes.Length!=9) return false;
        var reached=new HashSet<int>();var pending=new Queue<int>();pending.Enqueue(0);
        while(pending.TryDequeue(out var n))
        { if(n<0 || n>=layout.Nodes.Length) return false;if(!reached.Add(n)) continue;foreach(var link in layout.Nodes[n].Links)pending.Enqueue(link); }
        if(reached.Count!=layout.Nodes.Length) return false;
        foreach(var blocks in layout.Obstacles)
        {
            // Flood on a conservative 8px grid with a 12px actor footprint. Sockets/spawns are queried explicitly.
            bool Free(int x,int y)=>x>=48&&x<=592&&y>=76&&y<=296&&!blocks.Any(b=>x>=b.X-12&&x<=b.X+b.Width+12&&y>=b.Y-12&&y<=b.Y+b.Height+12);
            var seen=new HashSet<(int,int)>();var q=new Queue<(int,int)>();q.Enqueue((144,192));
            while(q.TryDequeue(out var p)) { if(!Free(p.Item1,p.Item2)||!seen.Add(p))continue;foreach(var d in new[]{(8,0),(-8,0),(0,8),(0,-8)})q.Enqueue((p.Item1+d.Item1,p.Item2+d.Item2)); }
            foreach(var point in new[]{(48,192),(592,192),(448,192),(512,128),(512,264)}) if(!seen.Contains(point))return false;
        }
        return true;
    }
    public static bool Same(GeneratedLayout a,GeneratedLayout b)=>a.Version==b.Version&&a.Seed==b.Seed&&a.Fallback==b.Fallback&&a.Rooms.SequenceEqual(b.Rooms)
        &&a.Obstacles.Length==b.Obstacles.Length&&a.Obstacles.Zip(b.Obstacles).All(p=>p.First.SequenceEqual(p.Second))
        &&a.Nodes.Length==b.Nodes.Length&&a.Nodes.Zip(b.Nodes).All(p=>p.First.Kind==p.Second.Kind&&p.First.Room==p.Second.Room&&p.First.Links.SequenceEqual(p.Second.Links));
}
