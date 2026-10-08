using Godot;
using Overload.Domain;

namespace Overload.Game;

public partial class ArenaHud : Control
{
    private Arena arena = null!;
    private CenterContainer menu = null!;
    private VBoxContainer options = null!;
    private PanelContainer panel = null!;
    private ScrollContainer scroll = null!;
    private string? capture;
    private Label? captureLabel;
    private readonly Color ink = new("dae4e8"), gold = new("e4bf7d");
    private bool home;
    private Color muted => arena.Audio.HighContrast ? new("d2e3ea") : new("8eabb6");
    public bool MenuVisible => menu.Visible;
    public void Initialize(Arena owner) => arena = owner;
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        InitializeSlotHints();
        InitializeCheckpointButtons();
        menu = new CenterContainer { MouseFilter = MouseFilterEnum.Stop };
        AddChild(menu); menu.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color("101a20", .97f), BorderColor = new Color("756449"),
            BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 2, BorderWidthBottom = 1,
            ContentMarginLeft = 28, ContentMarginRight = 28, ContentMarginTop = 24, ContentMarginBottom = 24,
            ShadowColor = new Color(0,0,0,.45f), ShadowSize = 12 });
        menu.AddChild(panel);
        scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true };
        panel.AddChild(scroll);
        options = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; options.AddThemeConstantOverride("separation", 10); scroll.AddChild(options);
        Resized += LayoutMenu; LayoutMenu();
    }
    private void LayoutMenu()
    {
        if (scroll is null) return;
        var wideHome = home && Size.X >= 1100;
        menu.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        if (wideHome) { menu.OffsetLeft = 44; menu.OffsetRight = -(Size.X - Math.Min(570, Size.X * .46f)); menu.OffsetTop = 40; menu.OffsetBottom = -40; }
        scroll.CustomMinimumSize = new Vector2(wideHome ? Math.Min(470, Size.X * .4f) : Math.Clamp(Size.X - 116, 300, 760), Math.Clamp(Size.Y - 132, 180, 570));
    }
    private int FontSize(int size) => (int)Math.Round(size * arena.Audio.TextPercent / 100f);
    private void ClearMenu(string eyebrow, string title, string subtitle)
    {
        home = false;
        foreach (var child in options.GetChildren()) { options.RemoveChild(child); child.QueueFree(); }
        menu.Show(); capture = null;
        LayoutMenu(); menu.Theme = MakeTheme();
        Theme=menu.Theme;
        goBack = () => { if (arena.Playing) Pause(); else Title(); };
        Text(eyebrow, 14, gold); Text(title, 38, ink); Text(subtitle, 17, muted);
        options.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) }); QueueRedraw();
    }
    private Label Text(string text, int size, Color color)
    {
        var label = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        label.AddThemeFontSizeOverride("font_size", FontSize(size)); label.AddThemeColorOverride("font_color", color); options.AddChild(label); return label;
    }
    private void AddButton(string label, Action action, bool focus = false)
    {
        var button = new Button { CustomMinimumSize = new Vector2(0, 44), TooltipText = label, AccessibilityName=label, FocusMode = FocusModeEnum.All };
        var caption = new Label { Text = label, MouseFilter = MouseFilterEnum.Ignore, AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        caption.AddThemeFontSizeOverride("font_size", FontSize(19));
        button.AddChild(caption); caption.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); caption.OffsetLeft=16;caption.OffsetRight=-16;
        button.Resized += () => button.CustomMinimumSize = new Vector2(0, Math.Max(44, caption.GetMinimumSize().Y + 12));
        button.AddThemeStyleboxOverride("normal", ButtonStyle(new Color("1e2d33"), new Color("3d4c4e")));
        button.AddThemeStyleboxOverride("hover", ButtonStyle(new Color("2a4147"), gold));
        button.AddThemeStyleboxOverride("pressed", ButtonStyle(new Color("365450"), gold));
        button.AddThemeStyleboxOverride("focus", new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0), BorderColor = gold,
            BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2 });
        button.Pressed += () => { arena.Audio.Play("ui", "UI"); action(); }; options.AddChild(button); if (focus) button.GrabFocus();
    }
    public void Title()
    {
        if (arena.Character is null) { SaveRecovery(); return; }
        arena.PresentHearth();
        ClearMenu("THE PALIMPSEST / HEARTH", "O V E R L O A D", "Fight to remember. Change how you fight.");
        home = true; LayoutMenu();
        var s = arena.Character.State;
        Text($"{s.Frame}  /  Level {CounterText.Short(s.ValidatedLevel)}  /  {(s.Mode==ProfileMode.Standard?"Standard journey":"Training character")}", 14, gold);
        AddButton(arena.Character.State.RunId != Guid.Empty && arena.Character.State.CheckpointRoom < JourneyRules.Length(arena.Character.State) ? $"Resume court — room {arena.Character.State.CheckpointRoom + 1}/{JourneyRules.Length(arena.Character.State)}" : arena.Character.State.RegionalCampaign?"Begin the four-region campaign":"Begin the eight-room court", () => { if (arena.IsSmoke) arena.StartEncounter(0); else arena.StartJourney(); }, true);
        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill }; grid.AddThemeConstantOverride("h_separation", 10); grid.AddThemeConstantOverride("v_separation", 10); options.AddChild(grid);
        HomeCard(grid, "Memories", "Overload bindings", Bindings);
        HomeCard(grid, "Arsenal", "Equipment & forge", Inventory);
        HomeCard(grid, "Character", "Skills, talents & Codex", CharacterMenu);
        HomeCard(grid, "Fracture Atlas", "Endless expeditions", Fractures);
        HomeCard(grid, "Forbidden oaths", "Override & mastery", Oaths);
        HomeCard(grid, "The Bellkeeper", "Combat practice", () => arena.StartEncounter(3));
        var links = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; options.AddChild(links);
        HomeLink(links, "Field guide", Tutorial); HomeLink(links, "Settings", Settings); HomeLink(links, "Controls", Controls); HomeLink(links, "Quit", () => arena.QuitGame());
        HomeLink(links,"Characters",NewFrameMenu);
        goBack = null;
        Text("Motion becomes Pursuit. A perfect evade becomes Afterstrike.", 13, muted);
        if (!string.IsNullOrEmpty(arena.Character.Notice)) Text(arena.Character.Notice, 12, gold);
    }
    public void HideMenu() { menu.Hide(); capture = null; QueueRedraw(); }
    public void Pause()
    {
        ClearMenu("TAKE A BREATH", "Paused", "The encounter clock is stopped.");
        AddButton("Continue", arena.TogglePause, true); AddButton("Controls", Controls);
        AddButton("Field guide", Tutorial); AddButton("Settings", Settings);
        if (arena.CheckpointRest) AddButton("Review checkpoint rewards",arena.InspectCheckpoint);
        else AddButton("Retry this encounter", arena.RetryCurrentRoom);
        AddButton("Return to Hearth", arena.ReturnToTitle);
        goBack = arena.TogglePause;
    }
    public void Death()
    {
        ClearMenu("THE COURT REMEMBERS", "Frame fallen", "Watch the warning. Move before the strike.");
        AddButton("Retry encounter", arena.RetryCurrentRoom, true); AddButton("Return to Hearth", arena.ReturnToTitle);
        Text("Life, Focus, and all three flasks return on retry.", 15, muted);
        goBack = arena.ReturnToTitle;
    }
    public void Victory()
    {
        ClearMenu("THE BELL FALLS SILENT", "Court cleared", "The practice encounter is complete.");
        AddButton("Run the court again", () => arena.StartEncounter(0), true);
        AddButton("Practice the Bellkeeper", () => arena.StartEncounter(3)); AddButton("Return to title", arena.ReturnToTitle);
        Text("Practice complete. Your campaign and saved rewards start at Hearth.", 14, muted);
    }
    private void Controls()
    {
        ClearMenu("MAKE IT YOURS", "Controls", "Select a binding, then press a key. Esc cancels.");
        captureLabel = Text("WASD movement · Mouse aim · F3 diagnostics", 15, muted);
        var grid = new GridContainer { Columns = 2 }; options.AddChild(grid);
        foreach (var name in InputRouter.Names.Where(n => n != "pause"))
        {
            var button = new Button { Text = $"{name.Replace('_', ' ')}  [{arena.Controls.Glyph(name)}]", CustomMinimumSize = new Vector2(210, 32) };
            button.Pressed += () => { capture = name; captureLabel.Text = $"Press a key for {name}…"; }; grid.AddChild(button);
        }
        Text("Controller: LS move · RS aim · RB basic attack\nX / Y / B active skills · A Traverse · LB Flask · Start pause\nD-pad / A navigate menus · B back · Keyboard arrows / Enter", 15, muted);
        AddButton("Back", () => { if (arena.Playing) Pause(); else Title(); }, true);
    }
    public bool TryCaptureKey(InputEvent input)
    {
        if (capture is null || input is not InputEventKey { Pressed: true, Echo: false } key) return false;
        if (key.Keycode != Key.Escape && arena.Controls.Rebind(capture, key.PhysicalKeycode == Key.None ? key.Keycode : key.PhysicalKeycode) is { } issue)
        { captureLabel!.Text = issue; return true; }
        capture = null; Controls(); return true;
    }
    public override void _Input(InputEvent input)
    {
        arena.Controls.Observe(input);
        if (TryCaptureKey(input)) GetViewport().SetInputAsHandled();
        else if (MenuVisible && input.IsActionPressed("ui_cancel"))
        { goBack?.Invoke(); arena.Controls.ClearBuffer(); GetViewport().SetInputAsHandled(); }
    }
    private void Write(Vector2 position, string text, int size, Color color) => DrawString(ThemeDB.FallbackFont, position, text, fontSize: FontSize(size), modulate: color);
    private void Bar(Rect2 rect, float fraction, Color color)
    {
        DrawRect(rect, new Color("283842")); DrawRect(new Rect2(rect.Position, new Vector2(rect.Size.X * fraction, rect.Size.Y)), color);
    }
}
