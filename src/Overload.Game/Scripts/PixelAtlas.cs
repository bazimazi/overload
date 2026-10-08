using System.Text.Json;
using Godot;

namespace Overload.Game;

/// <summary>Validated source rectangles and foot pivots; textures are never stretched to a guessed grid.</summary>
public sealed class PixelAtlas
{
    private sealed record AtlasData(int Columns, int Rows, float Scale, int Idle, int[] Walk, int Windup, int Strike, int Hit, float[][] Frames);
    private static readonly Dictionary<string, PixelAtlas> cache = [];
    private readonly AtlasData data;
    public Texture2D Texture { get; }
    public int Columns => data.Columns;
    public int Idle => data.Idle;
    public int Windup => data.Windup;
    public int Strike => data.Strike;
    public int Hit => data.Hit;
    public int Walk(int step) => data.Walk[Math.Abs(step) % data.Walk.Length];
    public AtlasTexture Portrait(int facing = 2) { var f = data.Frames[data.Idle * data.Columns + facing % data.Columns]; return new AtlasTexture { Atlas = Texture, Region = new(f[0],f[1],f[2],f[3]), FilterClip = true }; }
    public Vector2 FrameSize(int column, int row) { var f = data.Frames[Math.Clamp(row, 0, data.Rows - 1) * data.Columns + column % data.Columns]; return new Vector2(f[2], f[3]) * data.Scale; }
    private PixelAtlas(string name)
    {
        var path = $"res://Assets/Pixel/{name}";
        data = JsonSerializer.Deserialize<AtlasData>(Godot.FileAccess.GetFileAsString(path + ".atlas.json"), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException($"Missing atlas metadata: {name}");
        Texture = GD.Load<Texture2D>(path + ".png");
        if (data.Columns < 1 || data.Rows < 1 || data.Frames.Length != data.Columns * data.Rows || data.Scale <= 0
            || data.Walk is null || data.Walk.Length == 0 || data.Walk.Append(data.Idle).Append(data.Windup).Append(data.Strike).Append(data.Hit).Any(row => row < 0 || row >= data.Rows)
            || data.Frames.Any(f => f.Length != 6 || f[2] <= 0 || f[3] <= 0 || f[0] < 0 || f[1] < 0 || f[0] + f[2] > Texture.GetWidth() || f[1] + f[3] > Texture.GetHeight() || f[4] < 0 || f[4] > f[2] || f[5] < 0 || f[5] > f[3]))
            throw new InvalidDataException($"Invalid atlas rectangles: {name}");
    }
    public static PixelAtlas Load(string name)
    {
        if (!cache.TryGetValue(name, out var atlas)) cache[name] = atlas = new(name);
        return atlas;
    }
    public void Draw(CanvasItem canvas, int column, int row, Vector2 foot, Color tint, float size = 1)
    {
        var f = data.Frames[Math.Clamp(row, 0, data.Rows - 1) * data.Columns + column % data.Columns];
        var source = new Rect2(f[0], f[1], f[2], f[3]);
        var dimensions = (source.Size * data.Scale * size).Round();
        var pivot = new Vector2(f[4], f[5]) * data.Scale * size;
        canvas.DrawTextureRectRegion(Texture, new Rect2((foot - pivot).Round(), dimensions), source, tint);
    }
}
