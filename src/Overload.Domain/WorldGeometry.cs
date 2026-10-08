using System.Collections.Immutable;
using System.Numerics;

namespace Overload.Domain;

public readonly record struct WorldPoint(float X, float Y)
{
    public Vector2 Vector => new(X, Y);
    public static implicit operator WorldPoint(Vector2 p) => new(p.X, p.Y);
}

/// <summary>Authoritative floor extents; walls, navigation, map and camera use this same geometry.</summary>
public sealed record LevelGeometry(RoomBlock Bounds, ImmutableArray<RoomBlock> Blocks)
{
    public static LevelGeometry Court { get; } = new(new(28, 56, 584, 258), []);
    public bool Contains(WorldPoint p, float radius = 0) => float.IsFinite(p.X) && float.IsFinite(p.Y)
        && p.X > Bounds.X + radius && p.Y > Bounds.Y + radius
        && p.X < Bounds.X + Bounds.Width - radius && p.Y < Bounds.Y + Bounds.Height - radius;
    public ImmutableArray<RoomBlock> Collision => [new(Bounds.X - 12, Bounds.Y - 12, Bounds.Width + 24, 12),
        new(Bounds.X - 12, Bounds.Y + Bounds.Height, Bounds.Width + 24, 12),
        new(Bounds.X - 12, Bounds.Y, 12, Bounds.Height), new(Bounds.X + Bounds.Width, Bounds.Y, 12, Bounds.Height), .. Blocks];
    public TacticalNavigation Navigation() => new(Blocks, Bounds);
    public void Validate()
    {
        if (Bounds.Width is < 256 or > 4096 || Bounds.Height is < 256 or > 4096 || Blocks.IsDefault || Blocks.Length > 256
            || Blocks.Any(b => b.Width <= 0 || b.Height <= 0 || b.X < Bounds.X || b.Y < Bounds.Y
                || b.X + b.Width > Bounds.X + Bounds.Width || b.Y + b.Height > Bounds.Y + Bounds.Height))
            throw new InvalidDataException("Invalid level geometry");
    }
}
