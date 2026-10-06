using Godot;

namespace Overload.Game;

public partial class WorldView : Node2D
{
    private Overload.Domain.Region? region;
    public string RegionName=>region?.ToString().ToUpperInvariant()??"THE BROKEN COURT";
    public void SetRegion(Overload.Domain.Region? value) { region=value;QueueRedraw(); }
    public static readonly Rect2[] Walls = [new(16, 44, 608, 12), new(16, 314, 608, 14), new(16, 44, 12, 284), new(612, 44, 12, 284), new(205, 117, 24, 28), new(411, 237, 24, 28)];
    public Rect2[] CurrentWalls { get; private set; } = Walls;
    private Texture2D banner = null!;
    public override void _Ready() { banner = GD.Load<Texture2D>("res://Assets/court-banner.svg"); Configure(Walls.Skip(4).ToArray()); }
    public void Configure(Rect2[] obstacles)
    {
        foreach (var body in GetChildren().OfType<StaticBody2D>()) { body.CollisionLayer = 0; body.QueueFree(); }
        CurrentWalls = [.. Walls.Take(4), .. obstacles];
        foreach (var rect in CurrentWalls)
        {
            var wall = new StaticBody2D { Position = rect.GetCenter(), CollisionLayer = 1, CollisionMask = 0 };
            wall.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = rect.Size } });
            AddChild(wall);
        }
        QueueRedraw();
    }
    public override void _Draw()
    {
        DrawRect(new Rect2(0, 0, 640, 360), new Color("0c121e"));
        var tint=region switch { Overload.Domain.Region.Ash=>new Color("513529"),Overload.Domain.Region.Glass=>new Color("224a45"),Overload.Domain.Region.Hollow=>new Color("3c3450"),Overload.Domain.Region.Crown=>new Color("484030"),_=>new Color("1c2934") };
        DrawRect(new Rect2(28, 56, 584, 258), tint);
        for (var y = 58; y < 314; y += 16)
            for (var x = 30; x < 612; x += 24)
            {
                var tone = (x * 13 + y * 7) % 5;
                DrawRect(new Rect2(x, y, 22, 14), tint.Lightened(.06f).Darkened(tone * 0.027f));
            }
        DrawArc(new(320, 185), 90, 0, Mathf.Tau, 64, new Color("38434a"), 1);
        DrawArc(new(320, 185), 95, 0, Mathf.Tau, 64, new Color("38434a"), 1);
        DrawLine(new(300, 185), new(340, 185), new Color("475354"));
        DrawLine(new(320, 165), new(320, 205), new Color("475354"));
        if(region is { } r)
            for(var x=80;x<580;x+=64)
            {
                var color=tint.Lightened(.22f);
                if(r==Overload.Domain.Region.Ash)DrawRect(new(x,70,32,8),color);
                else if(r==Overload.Domain.Region.Glass)DrawPolyline([new(x,90),new(x+12,68),new(x+24,90)],color,2);
                else if(r==Overload.Domain.Region.Hollow) { DrawRect(new(x,70,24,16),color);DrawLine(new(x+12,70),new(x+12,86),tint); }
                else DrawPolyline([new(x,70),new(x+12,88),new(x+24,70)],color,2);
            }
        // The court's broken sun and paired banners remain readable below combat tells.
        for (var i = 0; i < 12; i++)
        {
            var direction = Vector2.FromAngle(i * Mathf.Tau / 12);
            DrawLine(new Vector2(320,185) + direction * 99, new Vector2(320,185) + direction * 105, new Color("596166"), 2);
        }
        foreach (var x in new[] { 110, 510 })
        {
            DrawRect(new Rect2(x - 16, 59, 32, 53), new Color("15232c"));
            DrawTextureRect(banner, new Rect2(x - 11, 56, 22, 44), false);
        }
        foreach (var rect in CurrentWalls)
        {
            DrawRect(new Rect2(rect.Position + new Vector2(3, 5), rect.Size), new Color(0, 0, 0, 0.3f));
            DrawRect(rect, new Color("45515a"));
            DrawRect(new Rect2(rect.Position, new Vector2(rect.Size.X, 3)), new Color("707775"));
            DrawRect(new Rect2(rect.Position + new Vector2(0, rect.Size.Y - 3), new Vector2(rect.Size.X, 3)), new Color("303c48"));
        }
        foreach (var x in new[] { 64, 576 })
        {
            DrawRect(new Rect2(x - 3, 56, 6, 9), new Color("987c54"));
            DrawCircle(new(x, 60), 3, new Color("f4c779"));
            DrawRect(new Rect2(x - 3, 300, 6, 9), new Color("987c54"));
            DrawCircle(new(x, 302), 3, new Color("f4c779"));
        }
    }
}
