using System.Numerics;

namespace Overload.Domain;

/// <summary>Bounded room navigation using the same footprints as the world adapter.</summary>
public sealed class TacticalNavigation(IEnumerable<RoomBlock> obstacles, RoomBlock? bounds = null)
{
    private readonly float Step = bounds is null ? 12 : 24;
    private readonly RoomBlock floor = bounds ?? LevelGeometry.Court.Bounds;
    private readonly RoomBlock[] blocks = obstacles.ToArray();
    private readonly Dictionary<float, Grid> grids = [];
    private sealed record Grid(Vector2[] Points, int[][] Edges);

    public bool Clear(Vector2 from, Vector2 to, float radius)
    {
        if (!Inside(from, radius) || !Inside(to, radius)) return false;
        foreach (var b in blocks)
        {
            // Stay below Godot's 0.08-pixel safe margin so a body touching a wall can still repath.
            var min = new Vector2(b.X - radius - .05f, b.Y - radius - .05f);
            var max = new Vector2(b.X + b.Width + radius + .05f, b.Y + b.Height + radius + .05f);
            var direction = to - from;
            var low = 0f; var high = 1f;
            if (Clip(from.X, direction.X, min.X, max.X, ref low, ref high)
                && Clip(from.Y, direction.Y, min.Y, max.Y, ref low, ref high)) return false;
        }
        return true;
    }
    private bool Inside(Vector2 p, float r) => p.X > floor.X + r && p.X < floor.X + floor.Width - r && p.Y > floor.Y + r && p.Y < floor.Y + floor.Height - r;
    private static bool Clip(float origin, float direction, float min, float max, ref float low, ref float high)
    {
        if (Math.Abs(direction) < .0001f) return origin >= min && origin <= max;
        var a = (min - origin) / direction; var b = (max - origin) / direction;
        low = Math.Max(low, Math.Min(a, b)); high = Math.Min(high, Math.Max(a, b));
        return low <= high;
    }
    private Grid Build(float radius)
    {
        var points = new List<Vector2>(); var cells = new Dictionary<(int X, int Y), int>();
        for (var y = 0; floor.Y + 1 + radius + y * Step < floor.Y + floor.Height - radius; y++)
            for (var x = 0; floor.X + 1 + radius + x * Step < floor.X + floor.Width - radius; x++)
            {
                var p = new Vector2(floor.X + 1 + radius + x * Step, floor.Y + 1 + radius + y * Step);
                if (!Clear(p, p, radius)) continue;
                cells[(x, y)] = points.Count; points.Add(p);
            }
        var edges = new int[points.Count][];
        foreach (var (cell, index) in cells)
        {
            var neighbors = new List<int>(8);
            for (var y = -1; y <= 1; y++) for (var x = -1; x <= 1; x++)
                if ((x != 0 || y != 0) && cells.TryGetValue((cell.X + x, cell.Y + y), out var next)
                    && Clear(points[index], points[next], radius)) neighbors.Add(next);
            edges[index] = neighbors.ToArray();
        }
        return new(points.ToArray(), edges);
    }
    public Vector2[] FindPath(Vector2 from, Vector2 to, float radius)
    {
        if (radius <= 0 || !float.IsFinite(radius)) throw new ArgumentOutOfRangeException(nameof(radius));
        if (Clear(from, to, radius)) return [to];
        if (!Clear(from, from, radius) || !Clear(to, to, radius)) return [];
        if (floor.Width > 3000 || floor.Height > 3000) return FindLandscapePath(from, to, radius);
        if (!grids.TryGetValue(radius, out var grid))
        {
            if (grids.Count >= 8) grids.Clear();
            grids[radius] = grid = Build(radius);
        }
        int Nearest(Vector2 p) => Enumerable.Range(0, grid.Points.Length)
            .Where(i => Vector2.DistanceSquared(p, grid.Points[i]) < Step * Step * 9 && Clear(p, grid.Points[i], radius))
            .OrderBy(i => Vector2.DistanceSquared(p, grid.Points[i])).FirstOrDefault(-1);
        var start = Nearest(from); var goal = Nearest(to);
        if (start < 0 || goal < 0) return [];
        var costs = Enumerable.Repeat(float.PositiveInfinity, grid.Points.Length).ToArray();
        var parents = Enumerable.Repeat(-1, grid.Points.Length).ToArray();
        var closed = new bool[grid.Points.Length]; var queue = new PriorityQueue<int, float>();
        costs[start] = 0; queue.Enqueue(start, 0);
        while (queue.TryDequeue(out var current, out _))
        {
            if (closed[current]) continue;
            if (current == goal)
            {
                var reverse = new List<Vector2> { to };
                for (var n = goal; n >= 0; n = parents[n]) reverse.Add(grid.Points[n]);
                reverse.Reverse();
                var route = new List<Vector2>(); var anchor = from;
                for (var n = 0; n < reverse.Count;)
                {
                    var next = n;
                    while (next + 1 < reverse.Count && Clear(anchor, reverse[next + 1], radius)) next++;
                    route.Add(reverse[next]); anchor = reverse[next]; n = next + 1;
                }
                return route.ToArray();
            }
            closed[current] = true;
            foreach (var next in grid.Edges[current])
            {
                var cost = costs[current] + Vector2.Distance(grid.Points[current], grid.Points[next]);
                if (cost >= costs[next]) continue;
                costs[next] = cost; parents[next] = current;
                queue.Enqueue(next, cost + Vector2.Distance(grid.Points[next], grid.Points[goal]));
            }
        }
        return [];
    }

    // Outdoor navigation scales with the number of structures, rather than the area of the map.
    // Inflated rectangle corners form a visibility graph and retain the same swept collision rule.
    private Vector2[] FindLandscapePath(Vector2 from, Vector2 to, float radius)
    {
        if (!grids.TryGetValue(radius, out var graph))
        {
            var margin = radius + .2f;
            var points = blocks.SelectMany(b => new Vector2[] {
                new(b.X-margin,b.Y-margin), new(b.X+b.Width+margin,b.Y-margin),
                new(b.X-margin,b.Y+b.Height+margin), new(b.X+b.Width+margin,b.Y+b.Height+margin)
            }).Where(p => Clear(p,p,radius)).Distinct().ToArray();
            var edges = points.Select((p,i) => Enumerable.Range(0,points.Length)
                .Where(j => j != i && Clear(p,points[j],radius)).ToArray()).ToArray();
            if (grids.Count >= 8) grids.Clear();
            grids[radius] = graph = new(points,edges);
        }
        var costs = Enumerable.Repeat(float.PositiveInfinity,graph.Points.Length).ToArray();
        var parents = Enumerable.Repeat(-1,graph.Points.Length).ToArray();
        var closed = new bool[graph.Points.Length];
        var queue = new PriorityQueue<int,float>();
        for(var i=0;i<graph.Points.Length;i++)
            if(Clear(from,graph.Points[i],radius))
            {costs[i]=Vector2.Distance(from,graph.Points[i]);queue.Enqueue(i,costs[i]+Vector2.Distance(graph.Points[i],to));}
        var best = float.PositiveInfinity; var goal = -1;
        while(queue.TryDequeue(out var current,out var estimate))
        {
            if(estimate>=best)break;
            if(closed[current])continue;
            closed[current]=true;
            if(Clear(graph.Points[current],to,radius))
            {best=costs[current]+Vector2.Distance(graph.Points[current],to);goal=current;}
            foreach(var next in graph.Edges[current])
            {
                var cost=costs[current]+Vector2.Distance(graph.Points[current],graph.Points[next]);
                if(cost>=costs[next])continue;
                costs[next]=cost;parents[next]=current;
                queue.Enqueue(next,cost+Vector2.Distance(graph.Points[next],to));
            }
        }
        if(goal<0)return [];
        var route=new List<Vector2>{to};
        for(var i=goal;i>=0;i=parents[i])route.Add(graph.Points[i]);
        route.Reverse();return [..route];
    }
}
