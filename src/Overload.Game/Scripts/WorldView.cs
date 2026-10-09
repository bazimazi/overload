using Godot;
using Overload.Domain;

namespace Overload.Game;

/// <summary>Atmosphere is independent of the collision kit and every gameplay RNG stream.</summary>
public partial class WorldView : Node2D
{
    public Arena? PerformanceOwner {get;init;}
    private bool terrainOnly,terrainDirty=true;
    private WorldView? terrainCanvas;
    private LandscapeGround surface=null!;
    private Vector2? terrainAnchor;
    private Region? region;
    private readonly Dictionary<string, Texture2D> backgrounds = [];
    private float time;
    private bool hearth;
    private bool exitOpen;
    public void OpenCheckpointExit() { exitOpen=true;QueueRedraw(); }
    public string RegionName => region?.ToString().ToUpperInvariant() ?? "THE BROKEN COURT";
    public void SetRegion(Region? value) { region = value; hearth = false; QueueRedraw(); }
    public void SetHearth() { region = null; hearth = true; QueueRedraw(); }
    public static readonly Rect2[] Walls = [new(16, 44, 608, 12), new(16, 314, 608, 14), new(16, 44, 12, 284), new(612, 44, 12, 284), new(205, 117, 24, 28), new(411, 237, 24, 28)];
    public Rect2[] CurrentWalls { get; private set; } = Walls;
    public TacticalNavigation Navigation { get; private set; } = new([]);
    public LevelGeometry Geometry { get; private set; } = LevelGeometry.Court;
    public override void _Ready()
    {
        if(terrainOnly)return;
        foreach (var name in new[] { "court", "ash", "glass", "hollow", "crown", "hearth" }) backgrounds[name] = GD.Load<Texture2D>($"res://Assets/Pixel/{name}.png");
        surface=new LandscapeGround {ZIndex=-2};AddChild(surface);
        terrainCanvas=new WorldView {terrainOnly=true,ZIndex=-1,roadPaths=roadPaths,PerformanceOwner=PerformanceOwner,TextureRepeat=TextureRepeatEnum.Mirror,TextureFilter=TextureFilterEnum.Linear};
        foreach(var entry in backgrounds)terrainCanvas.backgrounds.Add(entry.Key,entry.Value);
        AddChild(terrainCanvas);
        Configure(Walls.Skip(4).ToArray());
    }
    public void Configure(Rect2[] obstacles)
    {
        roadPaths.Clear();
        Exploration=null;
        surface.Configure(null);
        terrainAnchor=null;terrainDirty=true;
        if(terrainCanvas is not null){terrainCanvas.Exploration=null;terrainCanvas.QueueRedraw();}
        Geometry=new(LevelGeometry.Court.Bounds,[..obstacles.Select(r=>new RoomBlock((int)r.Position.X,(int)r.Position.Y,(int)r.Size.X,(int)r.Size.Y))]);
        exitOpen=false;
        foreach (var body in GetChildren().OfType<StaticBody2D>()) { body.CollisionLayer = 0; body.QueueFree(); }
        foreach (var prop in GetChildren().OfType<CourtProp>()) prop.QueueFree();
        foreach (var structure in GetChildren().OfType<WorldStructure>()) structure.QueueFree();
        CurrentWalls = [.. Walls.Take(4), .. obstacles];
        Navigation = new(obstacles.Select(r => new RoomBlock((int)r.Position.X, (int)r.Position.Y, (int)r.Size.X, (int)r.Size.Y)));
        foreach (var rect in CurrentWalls)
        {
            var wall = new StaticBody2D { Position = rect.GetCenter(), CollisionLayer = 1, CollisionMask = 0 };
            wall.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = rect.Size } }); AddChild(wall);
        }
        foreach (var rect in obstacles) AddChild(new CourtProp { Footprint = rect, Region = region, ZIndex = (int)rect.End.Y / 4 });
        QueueRedraw();
    }
    public void Render(double delta, bool paused)
    {
        if (!paused) time += (float)Math.Min(delta, .05);
        if(Exploration is not null&&terrainCanvas is not null)
        {
            surface.ObservePlayer(PerformanceOwner?.Player.VisualPosition??Viewer);
            foreach(var structure in GetChildren().OfType<WorldStructure>())structure.Observe(Viewer,PerformanceOwner?.Player.VisualPosition??Viewer,delta,paused);
            // Retain native draw commands between camera cells. The 80px overscan covers
            // the maximum 32px displacement from this anchor in both camera axes.
            var anchor=(Viewer/64).Round()*64;
            if(terrainDirty||terrainAnchor!=anchor)
            {
                terrainAnchor=anchor;terrainDirty=false;
                terrainCanvas.Exploration=Exploration;terrainCanvas.Navigation=Navigation;
                terrainCanvas.region=region;terrainCanvas.progress=Progress;
                terrainCanvas.MapClaimed=MapClaimed;terrainCanvas.MapRequired=MapRequired;
                terrainCanvas.Viewer=anchor;terrainCanvas.time=time;
                terrainCanvas.QueueRedraw();
            }
        }
        QueueRedraw();
    }
    public override void _Draw()
    {
        if(terrainOnly)
        {
            var began=PerformanceOwner?.QualityTimestamp??0;
            if(Exploration is not null)DrawExplorationGround();
            PerformanceOwner?.RecordQualityStage("retained ground refresh",began);return;
        }
        if(Exploration is not null) { var began=PerformanceOwner?.QualityTimestamp??0;DrawExploration();PerformanceOwner?.RecordQualityStage("world draw",began);return; }
        var name = hearth ? "hearth" : region?.ToString().ToLowerInvariant() ?? "court";
        // Overscan aligns the clear authored floor with the existing 28..612 / 56..314 collision field.
        DrawTextureRect(backgrounds[name], new(-28, -32, 696, 424), false);
        if (exitOpen)
        {
            var p=Arena.CheckpointExit;var breath=.75f+.15f*MathF.Sin(time*3);
            DrawSetTransform(p+new Vector2(0,2),0,new(1,.4f));
            for(var i=3;i>0;i--)DrawArc(Vector2.Zero,15+i*4,0,Mathf.Tau,40,new Color("7cdbbf",.1f*i*breath),2);
            DrawSetTransform(Vector2.Zero);
            DrawPolyline([p+new Vector2(-13,0),p+new Vector2(-13,-38),p+new Vector2(-8,-47),p+new Vector2(8,-47),p+new Vector2(13,-38),p+new Vector2(13,0)],new Color("9eeed3",breath),2);
            DrawColoredPolygon([p+new Vector2(-11,0),p+new Vector2(-11,-36),p+new Vector2(-7,-44),p+new Vector2(7,-44),p+new Vector2(11,-36),p+new Vector2(11,0)],new Color("4fbbab",.18f));
            for(var i=0;i<10;i++)
            { var offset=new Vector2(MathF.Sin(time*1.7f+i*3)*9,-Mathf.PosMod(time*15+i*6,44));DrawRect(new((p+offset).Round(),new(1,2)),new Color("b8ffe1",.5f)); }
            DrawString(ThemeDB.FallbackFont,p+new Vector2(-22,-56),"ONWARD",fontSize:9,modulate:new("b8eacf"));
        }
        if (hearth)
        {
            var glow = .018f + .005f * MathF.Sin(time * .7f);
            DrawColoredPolygon([new(326,8),new(392,8),new(525,305),new(354,305)], new Color(1,.77f,.42f,glow));
        }
        var tone = region switch { Region.Ash => new Color("eab277"), Region.Hollow => new Color("c6a2df"), Region.Glass => new Color("8ed8cb"), _ => new Color("dfb779") };
        for (var i = 0; i < 36; i++)
        {
            var x = 36 + (i * 137 % 568);
            var y = 68 + Mathf.PosMod(i * 61 - time * (3 + i % 4), 240);
            var alpha = .1f + .15f * MathF.Sin(time * .8f + i);
            DrawRect(new(new Vector2(x + MathF.Sin(time * .4f + i) * 4, y).Round(), Vector2.One), new Color(tone, Math.Max(0, alpha)));
        }
        // Small perimeter flames breathe independently of simulation and reward RNG.
        foreach (var point in hearth ? new[] { new Vector2(506,72), new Vector2(215,49), new Vector2(89,57) } : new[] { new Vector2(14,118), new Vector2(626,118), new Vector2(14,245), new Vector2(626,245) })
        {
            var pulse = .035f + .012f * MathF.Sin(time * 7 + point.X);
            for (var layer = 3; layer > 0; layer--) DrawCircle(point, layer * 10, new Color(tone, pulse / layer));
            for (var i = 0; i < 3; i++)
            {
                var rise = Mathf.PosMod(time * (11 + i * 3) + point.X + i * 7, 26);
                DrawRect(new((point + new Vector2(MathF.Sin(time * 2 + i) * 3, -rise)).Round(), Vector2.One), new Color(tone, (1 - rise / 26) * .65f));
            }
        }
    }
}

/// <summary>Raised stonework sorts at the collision footprint's foot, just like the actors.</summary>
public partial class CourtProp : Node2D
{
    private PixelAtlas atlas = null!;
    public Rect2 Footprint { get; init; }
    public Region? Region { get; init; }
    public override void _Ready() { atlas = PixelAtlas.Load("props"); QueueRedraw(); }
    public override void _Draw()
    {
        var r = Footprint;
        if (atlas is not null)
        {
            var column = Region switch { Overload.Domain.Region.Ash => 1, Overload.Domain.Region.Glass => 2, Overload.Domain.Region.Hollow => 3, _ => 0 };
            var row = r.Size.X >= 36 ? 2 : r.Size.Y > 36 ? 1 : 0;
            var dimensions = atlas.FrameSize(column, row);
            DrawSetTransform(new(r.GetCenter().X, r.End.Y + 3), 0, new(1,.3f));
            DrawCircle(Vector2.Zero, (r.Size.X + 8) / 2, new Color(0,0,0,.35f)); DrawSetTransform(Vector2.Zero);
            atlas.Draw(this, column, row, new(r.GetCenter().X, r.End.Y), Colors.White, (r.Size.X + 10) / dimensions.X);
            return;
        }
        var dark = new Color("20282e"); var face = new Color("3b484c"); var light = new Color("67706b");
        if (Region == Overload.Domain.Region.Ash) { dark = new("302626"); face = new("50433a"); light = new("87745b"); }
        if (Region == Overload.Domain.Region.Hollow) { face = new("454251"); light = new("787184"); }
        DrawRect(new(r.Position + new Vector2(3, 4), r.Size + new Vector2(3, 1)), new Color(0, 0, 0, .35f));
        DrawRect(new(r.Position - new Vector2(2, 12), r.Size + new Vector2(4, 12)), dark);
        DrawRect(new(r.Position - new Vector2(0, 10), r.Size + new Vector2(0, 8)), face);
        DrawRect(new(r.Position - new Vector2(2, 12), new(r.Size.X + 4, 8)), light.Darkened(.15f));
        DrawRect(new(r.Position - new Vector2(0, 11), new(r.Size.X, 2)), light);
        for (var y = r.Position.Y - 4; y < r.End.Y - 2; y += 8)
        {
            DrawLine(new(r.Position.X, y), new(r.End.X, y), dark);
            var x = r.Position.X + (Math.Abs((int)y) % 16 < 8 ? r.Size.X / 3 : r.Size.X * 2 / 3);
            DrawLine(new(x, y), new(x, Math.Min(y + 8, r.End.Y - 2)), dark);
        }
        DrawRect(new(r.Position + new Vector2(3, -8), new(2, r.Size.Y + 4)), light.Darkened(.2f));
        var center = new Vector2(r.GetCenter().X, r.Position.Y - 7);
        DrawPolyline([center + new Vector2(-3, 0), center + new Vector2(0, -3), center + new Vector2(3, 0), center + new Vector2(0, 3), center + new Vector2(-3, 0)], new Color("a3a17b"));
        DrawRect(new(r.Position + new Vector2(-1, 3), new(2, 5)), new Color("435f4c"));
        DrawRect(new(r.Position + new Vector2(1, 6), new(2, 6)), new Color("567458"));
    }
}
