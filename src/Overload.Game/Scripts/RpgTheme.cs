using Godot;
using Overload.Domain;

namespace Overload.Game;

/// <summary>Shared materials and artwork for the output-resolution interface.</summary>
public static class RpgTheme
{
    public static readonly Color Gold = new("c5a46a"), Ink = new("e8dfcd"), Muted = new("a49d90");
    public static Font Heading => heading ??= GD.Load<Font>("res://Assets/Fonts/Cinzel.ttf");
    private static Font? heading;
    private static Texture2D? icons;
    private static readonly Dictionary<int,AtlasTexture> artwork=[];
    public static StyleBoxFlat Box(Color? border = null, Color? fill = null, int margin = 12) => new()
    {
        BgColor = fill ?? new Color("171614", .98f), BorderColor = border ?? new Color("62533c"),
        BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 2, BorderWidthBottom = 2,
        ContentMarginLeft = margin, ContentMarginRight = margin, ContentMarginTop = margin, ContentMarginBottom = margin,
        ShadowColor = new Color(0, 0, 0, .6f), ShadowSize = 8
    };
    public static int Icon(SkillId id) => id switch
    {
        SkillId.Cleave => 0, SkillId.ShieldPulse => 1, SkillId.ChainLance => 2, SkillId.Faultline => 3,
        SkillId.Traverse => 4, SkillId.Flask => 5, SkillId.Needle => 6, SkillId.ShardShot => 7,
        SkillId.Tether => 8, SkillId.CinderOrb => 9, SkillId.EmberWell => 9, SkillId.StormLoom => 10,
        SkillId.Veil => 11, SkillId.BoneVolley => 12, SkillId.Reap => 13, SkillId.EchoOrder => 14, SkillId.RecallThread => 15,
        SkillId.IronSweep=>0,SkillId.GuardBolt=>1,SkillId.SunderingBlow=>3,SkillId.Bulwark=>1,
        SkillId.FrostFan=>7,SkillId.ThreadCut=>6,SkillId.GraveLine=>13,SkillId.SoulLance=>8,SkillId.DuskRing=>17,_ => 0
    };
    public static string Description(SkillDefinition skill)=>skill.Id switch
    {
        SkillId.EmberWell=>"Place an ember well along your aim. It erupts after 0.6 seconds.",
        SkillId.StormLoom=>"Weave a storm at the aimed point. Three pulses strike over two seconds.",
        SkillId.Tether=>"Pierce and slow ordinary foes for two seconds. Against rulers, break their guard.",
        SkillId.BoneVolley=>"Release three bone bolts in a spread. Each foe is struck once per volley.",
        SkillId.EchoOrder=>"Command an echo to fire three bolts. A new command replaces the old echo.",
        SkillId.Veil or SkillId.Bulwark or SkillId.RecallThread=>"Raise a temporary barrier to absorb incoming damage.",
        _=>skill.ProjectileSpeed>0?"Send a piercing attack along your aim. Stored memories can change its form.":skill.ArcDegrees<=0?"Strike a line of enemies ahead. Stored memories can change its form.":"Sweep enemies in front of you. Stored memories can change its form."
    };
    public static AtlasTexture Artwork(int index)
    {
        if(artwork.TryGetValue(index,out var existing))return existing;
        icons ??= GD.Load<Texture2D>("res://Assets/UI/relic-icons.png");
        var cell = new Vector2(icons.GetWidth() / 6f, icons.GetHeight() / 4f);
        var texture=new AtlasTexture { Atlas = icons, Region = new Rect2(new Vector2(index % 6, index / 6) * cell, cell) };
        artwork.Add(index,texture);return texture;
    }
    public static void Frame(CanvasItem canvas, Rect2 r, Color color, bool flourishes = true)
    {
        canvas.DrawRect(r, new Color("100f0d", .93f));
        canvas.DrawRect(r, color.Darkened(.45f), false, 2);
        canvas.DrawRect(r.Grow(-4), new Color(color, .38f), false);
        canvas.DrawLine(r.Position + new Vector2(8, 1), r.Position + new Vector2(r.Size.X - 8, 1), color);
        if (!flourishes) return;
        foreach (var corner in new[] { r.Position, new Vector2(r.End.X, r.Position.Y), r.End, new Vector2(r.Position.X, r.End.Y) })
        {
            var direction = (r.GetCenter() - corner).Sign();
            canvas.DrawPolyline([corner + new Vector2(direction.X * 22, 0), corner, corner + new Vector2(0, direction.Y * 22)], color, 2);
            var p = corner + direction * 6;
            canvas.DrawColoredPolygon([p - new Vector2(3, 0), p - new Vector2(0, 3), p + new Vector2(3, 0), p + new Vector2(0, 3)], color);
        }
    }
}

public partial class RelicPanel : PanelContainer
{
    public override void _Draw()
    {
        // The panel's native style supplies layout margins; ornamentation is purely visual.
        RpgTheme.Frame(this, new(Vector2.Zero, Size), RpgTheme.Gold);
        for (var y = 8; y < Size.Y - 8; y += 4)
            DrawLine(new(8, y), new(Size.X - 8, y), new Color(1, .8f, .5f, .012f));
    }
}

public partial class RelicIcon : Control
{
    public int Index { get; set; }
    public Color Accent { get; set; } = RpgTheme.Gold;
    public override void _Ready() { MouseFilter = MouseFilterEnum.Ignore; TextureFilter = TextureFilterEnum.Linear; }
    public override void _Draw()
    {
        var side=Math.Min(Size.X,Size.Y);var rect=new Rect2((Size-Vector2.One*side)/2,Vector2.One*side);
        DrawTextureRect(RpgTheme.Artwork(Index),rect,false);
        DrawRect(rect.Grow(-1),Accent,false,1);
    }
}

public partial class FramePortrait : Control
{
    public FrameId Frame { get; init; }
    public override void _Ready() { MouseFilter = MouseFilterEnum.Ignore; }
    public override void _Draw()
    {
        var center = new Vector2(Size.X / 2, Size.Y * .85f);
        DrawSetTransform(center, 0, new(1, .35f));
        for (var i = 6; i > 0; i--) DrawCircle(Vector2.Zero, i * 16, new Color("a78959", .018f));
        DrawArc(Vector2.Zero, Math.Min(80, Size.X * .36f), 0, Mathf.Tau, 64, new Color("92764e", .5f), 2);
        DrawSetTransform(Vector2.Zero);
        var atlas = PixelAtlas.Load(Frame.ToString().ToLowerInvariant());
        atlas.Draw(this, 2, atlas.Idle, center, Colors.White, Math.Min(3.5f, Size.Y / 65f));
    }
}
