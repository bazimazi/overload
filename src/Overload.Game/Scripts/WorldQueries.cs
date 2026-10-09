using Godot;

namespace Overload.Game;

public static class WorldQueries
{
    public static bool SectorOverlaps(Vector2 origin, Vector2 aim, float reach, float arc, Vector2 point, float radius)
    {
        var polygon = Sector(origin, aim, reach, arc);
        if (Geometry2D.IsPointInPolygon(point, polygon)) return true;
        for (var i = 0; i < polygon.Length; i++)
            if (SegmentCircle(polygon[i], polygon[(i + 1) % polygon.Length], point, radius) is not null) return true;
        return false;
    }
    public static bool Connected(Vector2 origin, Vector2 destination, float radius, Rect2[]? walls = null, Overload.Domain.TacticalNavigation? navigation = null)
    {
        if(navigation is not null)return navigation.Clear(new(origin.X,origin.Y),new(destination.X,destination.Y),radius)||navigation.FindPath(new(origin.X,origin.Y),new(destination.X,destination.Y),radius).Length>0;
        bool Clear(Vector2 a, Vector2 b)
        {
            var steps = Math.Max(1, (int)Math.Ceiling(a.DistanceTo(b) / 4));
            for (var i = 0; i <= steps; i++)
                if ((walls ?? WorldView.Walls).Any(w => w.Grow(radius).HasPoint(a.Lerp(b, i / (float)steps)))) return false;
            return true;
        }
        if (Clear(origin, destination)) return true;
        var queue = new Queue<Vector2>(); var seen = new HashSet<Vector2>(); queue.Enqueue(origin); seen.Add(origin);
        while (queue.TryDequeue(out var point) && seen.Count <= 1200)
        {
            if (point.DistanceTo(destination) <= 24 && Clear(point, destination)) return true;
            foreach (var step in new[] { Vector2.Right, Vector2.Left, Vector2.Up, Vector2.Down })
            {
                var next = point + step * 16;
                if (next.X < 28 || next.X > 612 || next.Y < 56 || next.Y > 314 || seen.Contains(next) || !Clear(point, next)) continue;
                seen.Add(next); queue.Enqueue(next);
            }
        }
        return false;
    }
    public static bool ClearRay(Node2D world, Vector2 from, Vector2 to)
    {
        using var query=PhysicsRayQueryParameters2D.Create(from,to,1);
        using var hit=world.GetWorld2D().DirectSpaceState.IntersectRay(query);
        return hit.Count==0;
    }

    public static Vector2 ClipProjectile(Node2D world, Vector2 from, Vector2 to, float radius, out bool blocked, uint mask = 1)
    {
        using var shape = new CircleShape2D { Radius = radius };
        using var query = new PhysicsShapeQueryParameters2D { Shape = shape, Transform = new Transform2D(0, from), CollisionMask = mask };
        var space = world.GetWorld2D().DirectSpaceState;
        var overlaps=space.IntersectShape(query,1);
        using var overlapOwner=(Godot.Collections.Array)overlaps;
        if (overlaps.Count > 0) { blocked = true; return from; }
        // IntersectShape applies Motion too. Test the origin at rest before sweeping to the endpoint.
        query.Motion=to-from;
        var fractions = space.CastMotion(query);
        var safe = fractions.Length > 0 ? fractions[0] : 1;
        blocked = safe < 1;
        return from.Lerp(to, safe);
    }
    public static bool FreeLanding(Node2D world, Vector2 point, float radius)
    {
        using var shape = new CircleShape2D { Radius = radius + 0.1f };
        using var query = new PhysicsShapeQueryParameters2D { Shape = shape, Transform = new Transform2D(0, point), CollisionMask = 5 };
        var overlaps=world.GetWorld2D().DirectSpaceState.IntersectShape(query,1);
        using var overlapOwner=(Godot.Collections.Array)overlaps;
        return overlaps.Count==0;
    }
    public static Vector2 CrossingLanding(Node2D world, Vector2 origin, Vector2 destination, float radius)
    {
        var end = ClipProjectile(world, origin, destination, radius + 0.1f, out _);
        var length = origin.DistanceTo(end); var direction = origin.DirectionTo(end);
        for (var distance = length; distance > 0; distance -= 1)
        {
            var candidate = origin + direction * distance;
            if (FreeLanding(world, candidate, radius)) return candidate;
        }
        return origin;
    }
    public static Vector2[] Lane(Vector2 origin, Vector2 aim, float reach, float halfWidth)
    {
        var side = aim.Orthogonal() * halfWidth;
        return [origin + side, origin + aim * reach + side, origin + aim * reach - side, origin - side];
    }
    public static ActorBody[] LaneHits(Node2D world, Vector2 origin, Vector2 aim, float reach, float halfWidth)
    {
        using var shape = new ConvexPolygonShape2D { Points = Lane(Vector2.Zero, aim, reach, halfWidth) };
        using var query = new PhysicsShapeQueryParameters2D { Shape = shape, Transform = new Transform2D(0, origin), CollisionMask = 4 };
        var overlaps=world.GetWorld2D().DirectSpaceState.IntersectShape(query,64);
        using var overlapOwner=(Godot.Collections.Array)overlaps;
        return Bodies(overlaps)
            .Where(body => ClearRay(world, origin, body.Position)).OrderBy(body => (body.Position - origin).Dot(aim)).ThenBy(body => body.ActorId).ToArray();
    }

    public static Vector2[] Sector(Vector2 origin, Vector2 aim, float radius, float degrees)
    {
        var half = Mathf.DegToRad(degrees) / 2;
        var angle = aim.Angle();
        var vertices = new Vector2[18];
        vertices[0] = origin;
        for (var i = 0; i <= 16; i++) vertices[i + 1] = origin + Vector2.FromAngle(angle - half + 2 * half * i / 16) * radius;
        return vertices;
    }
    public static ActorBody[] SectorHits(Node2D world, Vector2 origin, Vector2 aim, float radius, float degrees, uint mask)
    {
        if(radius<=0||degrees<=0)return [];
        var points=Sector(Vector2.Zero,aim,radius,degrees);
        // At 180 degrees the origin is collinear with the end vertices; floating-point sin/cos can make it a concave seam.
        if(degrees>=180)points=points[1..];
        using var shape = new ConvexPolygonShape2D { Points = points };
        using var query = new PhysicsShapeQueryParameters2D { Shape = shape, Transform = new Transform2D(0, origin), CollisionMask = mask };
        var overlaps=world.GetWorld2D().DirectSpaceState.IntersectShape(query,64);
        using var overlapOwner=(Godot.Collections.Array)overlaps;
        var result = Bodies(overlaps)
            .Where(body => ClearRay(world, origin, body.Position)).OrderBy(body => body.ActorId).ToArray();
        return result;
    }
    private static IEnumerable<ActorBody> Bodies(Godot.Collections.Array<Godot.Collections.Dictionary> hits)
    {
        foreach(var hit in hits)
        {
            ActorBody? body;
            using(hit)body=hit["collider"].AsGodotObject() as ActorBody;
            if(body is not null)yield return body;
        }
    }
    public static float? SegmentCircle(Vector2 from, Vector2 to, Vector2 center, float radius)
    {
        var direction = to - from;
        var offset = from - center;
        var c = offset.LengthSquared() - radius * radius;
        if (c <= 0) return 0;
        var a = direction.LengthSquared();
        if (a == 0) return null;
        var b = offset.Dot(direction);
        var discriminant = b * b - a * c;
        if (discriminant < 0) return null;
        var t = (-b - Mathf.Sqrt(discriminant)) / a;
        return t >= 0 && t <= 1 ? t : null;
    }
}
