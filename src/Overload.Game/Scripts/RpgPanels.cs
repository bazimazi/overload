using Godot;
using Overload.Domain;

namespace Overload.Game;

public partial class ArenaHud
{
    private string DescribeSkill(SkillId id)=>RpgTheme.Description(arena.Balance.Skills.Single(s=>s.Id==id));
    private void AnimateHover(Control control, bool entered)
    {
        var tween = control.CreateTween();
        tween.TweenProperty(control, "modulate", entered ? new Color(1.12f, 1.08f, 1) : Colors.White, .12);
    }
    private Label BodyLabel(Node parent, string value, int size = 14, Color? color = null, bool heading = false)
    {
        var label = new Label { Text = value, AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        label.AddThemeFontSizeOverride("font_size", FontSize(size)); label.AddThemeColorOverride("font_color", color ?? ink);
        if (heading) label.AddThemeFontOverride("font", RpgTheme.Heading);
        parent.AddChild(label); return label;
    }
    private Button PanelButton(Node parent, string label, Action action, bool disabled = false)
    {
        var button = new Button { Text = label, Disabled = disabled, CustomMinimumSize = new(0, 38), SizeFlagsHorizontal = SizeFlags.ExpandFill, AccessibilityName = label };
        button.AddThemeFontSizeOverride("font_size", FontSize(14));
        button.Pressed += () => { arena.Audio.Play("ui", "UI"); action(); }; parent.AddChild(button); return button;
    }
    private void PanelTabs(string active)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; row.AddThemeConstantOverride("separation", 6); options.AddChild(row);
        foreach (var entry in new (string Name, Action Open)[] { ("Character", CharacterMenu), ("Arsenal", Inventory), ("Skills", SkillsMenu), ("Talents", TalentsMenu), ("Memories", Bindings), ("Atlas", Fractures) })
        {
            var b = PanelButton(row, entry.Name, entry.Open, arena.Playing&&(!arena.WorldActive||arena.GraphFracture||entry.Name is "Memories" or "Atlas"));
            if (entry.Name == active) b.AddThemeColorOverride("font_color", gold);
        }
    }
    private VBoxContainer Section(Node parent, string title)
    {
        var frame = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        frame.AddThemeStyleboxOverride("panel", RpgTheme.Box()); parent.AddChild(frame);
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; frame.AddChild(box);
        BodyLabel(box, title, 17, gold, true); box.AddChild(new HSeparator()); return box;
    }
    private Button ArtButton(Node parent, int icon, string name, string detail, Action action, bool selected = false, bool disabled = false)
    {
        var button = new Button { CustomMinimumSize = new(0, 94), SizeFlagsHorizontal = SizeFlags.ExpandFill, TooltipText = name + "\n" + detail, AccessibilityName = name + ". " + detail, Disabled = disabled };
        parent.AddChild(button);
        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore }; button.AddChild(row); row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        row.OffsetLeft = 10; row.OffsetRight = -10; row.OffsetTop = 10; row.OffsetBottom = -10; row.AddThemeConstantOverride("separation", 12);
        row.AddChild(new RelicIcon { Index = icon, Accent = selected ? teal : gold, CustomMinimumSize = new(64, 64), SizeFlagsVertical = SizeFlags.ShrinkCenter });
        var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center }; row.AddChild(column);
        BodyLabel(column, name, 16, selected ? teal : gold, true).MouseFilter = MouseFilterEnum.Ignore;
        BodyLabel(column, detail, 12, muted).MouseFilter = MouseFilterEnum.Ignore;
        if (selected) button.AddThemeStyleboxOverride("normal", ButtonStyle(new("1c2924"), teal.Darkened(.3f)));
        button.Pressed += () => { arena.Audio.Play("ui", "UI"); action(); };
        return button;
    }
    private GridContainer CardGrid(int preferredColumns = 2)
    {
        var grid = new GridContainer { Columns = Size.X < 1100 ? Math.Min(2, preferredColumns) : preferredColumns, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 12); grid.AddThemeConstantOverride("v_separation", 12); options.AddChild(grid); return grid;
    }
    private void StatLine(Node parent, string name, string value, Color? color = null)
    {
        var row = new HBoxContainer(); parent.AddChild(row);
        BodyLabel(row, name, 13, muted); var number = BodyLabel(row, value, 14, color ?? ink); number.HorizontalAlignment = HorizontalAlignment.Right;
    }
}

public partial class EquippedFigure : Control
{
    public CharacterState State { get; init; } = new();
    public Action<Guid>? Inspect { get; init; }
    public override void _Ready()
    {
        CustomMinimumSize = new(248, 292);
        var figure = new FramePortrait { Frame = State.Frame }; AddChild(figure);
        figure.Position = new(44, 42); figure.Size = new(160, 210);
        var positions = new Vector2[] { new(8, 95), new(182, 95), new(95, 4), new(95, 216), new(8, 206), new(182, 206) };
        foreach (var slot in Enum.GetValues<GearSlot>())
        {
            var item = State.Equipment.TryGetValue(slot, out var id) ? EquipmentRules.Find(State, id) : null;
            var button = new Button { Position = positions[(int)slot], Size = new(58, 64), TooltipText = item is null ? slot.ToString() : item.Name + "\n" + string.Join("\n", item.Affixes.Select(a => a.Kind + " +" + a.Value)), AccessibilityName = slot + ": " + (item?.Name ?? "Empty") };
            AddChild(button); var art = new RelicIcon { Index = 18 + (int)slot }; button.AddChild(art); art.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); art.OffsetLeft = 3; art.OffsetTop = 3; art.OffsetRight = -3; art.OffsetBottom = -3;
            if (item is not null) button.Pressed += () => Inspect?.Invoke(item.Id);
        }
    }
}
