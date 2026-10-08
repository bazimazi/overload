using System.Numerics;

namespace Overload.Domain;

/// <summary>Bounded room navigation using the same footprints as the world adapter.</summary>
public sealed class TacticalNavigation(IEnumerable<RoomBlock> obstacles)
{
    private const float Step = 12;
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
    private static bool Inside(Vector2 p, float r) => p.X > 28 + r && p.X < 612 - r && p.Y > 56 + r && p.Y < 314 - r;
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
        for (var y = 0; 57 + radius + y * Step < 314 - radius; y++)
            for (var x = 0; 29 + radius + x * Step < 612 - radius; x++)
            {
                var p = new Vector2(29 + radius + x * Step, 57 + radius + y * Step);
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
        if (!grids.TryGetValue(radius, out var grid)) grids[radius] = grid = Build(radius);
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
}
