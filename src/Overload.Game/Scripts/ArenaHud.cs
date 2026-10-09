using Godot;
using Overload.Domain;

namespace Overload.Game;

public partial class ArenaHud : Control
{
    private Arena arena = null!;
    private Control menu = null!;
    private VBoxContainer options = null!;
    private PanelContainer panel = null!;
    private ScrollContainer scroll = null!;
    private Button closeMenu=null!;
    private string? capture;
    private Label? captureLabel;
    private readonly Color ink = RpgTheme.Ink, gold = RpgTheme.Gold;
    private bool home;
    private Tween? panelEntrance;
    private Color muted => arena.Audio.HighContrast ? new("eee4d3") : RpgTheme.Muted;
    public bool MenuVisible => menu.Visible;
    public void Initialize(Arena owner) => arena = owner;
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        InitializeSlotHints();
        InitializeCheckpointButtons();
        InitializeGameDock();
        menu = new Control { MouseFilter = MouseFilterEnum.Stop };
        AddChild(menu); menu.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        panel = new RelicPanel();
        panel.AddThemeStyleboxOverride("panel", RpgTheme.Box(margin: 24));
        menu.AddChild(panel);
        scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true };
        panel.AddChild(scroll);
        options = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; options.AddThemeConstantOverride("separation", 10); scroll.AddChild(options);
        closeMenu=new Button {Text="×",Size=new(28,28),TooltipText="Back [Esc / B]",AccessibilityName="Back to previous screen"};menu.AddChild(closeMenu);
        closeMenu.AddThemeFontSizeOverride("font_size",16);
        closeMenu.AddThemeStyleboxOverride("normal",RpgTheme.Box(gold,margin:0));closeMenu.AddThemeStyleboxOverride("hover",RpgTheme.Box(ink,margin:0));
        closeMenu.Pressed+=()=>goBack?.Invoke();
        Resized += LayoutMenu; LayoutMenu();
    }
    private void LayoutMenu()
    {
        if (scroll is null) return;
        var wideHome = home && Size.X >= 1100;
        menu.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var contentWidth=wideHome?Math.Min(470,Size.X*.4f):Math.Clamp(Size.X-116,300,1040);
        var contentHeight=Math.Clamp(Size.Y-(home?132:100),180,800);
        scroll.CustomMinimumSize=new(contentWidth,contentHeight);
        panel.Size=new(contentWidth+48,contentHeight+48);
        panel.Position=wideHome?new Vector2(44,(Size.Y-panel.Size.Y)/2):(Size-panel.Size)/2;
        closeMenu.Position=panel.Position+new Vector2(panel.Size.X-38,10);
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
        Text(eyebrow, 11, gold); Text(title, 30, ink); Text(subtitle, 14, muted);
        options.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) }); QueueRedraw();
        panelEntrance?.Kill();panel.Modulate=arena.Audio.ReducedFlash?Colors.White:new Color(1,1,1,.35f);
        if(!arena.Audio.ReducedFlash){panelEntrance=CreateTween();panelEntrance.TweenProperty(panel,"modulate",Colors.White,.16);}
    }
    private Label Text(string text, int size, Color color)
    {
        var label = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        label.AddThemeFontSizeOverride("font_size", FontSize(size)); label.AddThemeColorOverride("font_color", color);
        if (size >= 21) label.AddThemeFontOverride("font", RpgTheme.Heading);
        options.AddChild(label); return label;
    }
    private void AddButton(string label, Action action, bool focus = false)
    {
        var button = new Button { CustomMinimumSize = new Vector2(0, 44), TooltipText = label, AccessibilityName=label, FocusMode = FocusModeEnum.All };
        var caption = new Label { Text = label, MouseFilter = MouseFilterEnum.Ignore, AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        caption.AddThemeFontSizeOverride("font_size", FontSize(16));
        button.AddChild(caption); caption.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); caption.OffsetLeft=16;caption.OffsetRight=-16;
        button.Resized += () => button.CustomMinimumSize = new Vector2(0, Math.Max(44, caption.GetMinimumSize().Y + 12));
        button.AddThemeStyleboxOverride("normal", ButtonStyle(new Color("25211b"), new Color("67543a")));
        button.AddThemeStyleboxOverride("hover", ButtonStyle(new Color("3c3021"), gold));
        button.AddThemeStyleboxOverride("pressed", ButtonStyle(new Color("4b3925"), gold));
        button.AddThemeStyleboxOverride("focus", new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0), BorderColor = gold,
            BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2 });
        button.Pressed += () => { arena.Audio.Play("ui", "UI"); action(); }; options.AddChild(button); if (focus) button.GrabFocus();
    }
    public void Title()
    {
        if (arena.Character is null) { SaveRecovery(); return; }
        arena.PresentHearth();
        ClearMenu("THE PALIMPSEST / HEARTH", "OVERLOAD", "Fight to remember. Change how you fight.");
        home = true; LayoutMenu();
        var s = arena.Character.State;
        if(s.World is not null)AddButton($"Explore the world · {s.World.ActiveZone.Name}",arena.StartWorld,true);
        else if(s.RunId==Guid.Empty||s.CheckpointRoom==JourneyRules.Length(s))AddButton("Enter the connected world campaign",arena.StartWorld);
        Text($"{s.Frame}  /  Level {CounterText.Short(s.ValidatedLevel)}  /  {(s.Mode==ProfileMode.Standard?"Standard journey":"Training character")}", 14, gold);
        if(s.World is null) AddButton(arena.Character.State.RunId != Guid.Empty && arena.Character.State.CheckpointRoom < JourneyRules.Length(arena.Character.State) ? $"Resume court — room {arena.Character.State.CheckpointRoom + 1}/{JourneyRules.Length(arena.Character.State)}" : arena.Character.State.RegionalCampaign?"Begin the four-region campaign":"Begin the eight-room court", () => { if (arena.IsSmoke) arena.StartEncounter(0); else arena.StartJourney(); }, true);
        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill }; grid.AddThemeConstantOverride("h_separation", 10); grid.AddThemeConstantOverride("v_separation", 10); options.AddChild(grid);
        HomeCard(grid, "Memories", "Overload bindings", Bindings);
        HomeCard(grid, "Arsenal", "Equipment & forge", Inventory);
        HomeCard(grid, "Character", "Skills, talents & Codex", CharacterMenu);
        HomeCard(grid, "Fracture Atlas", "Endless expeditions", Fractures);
        HomeCard(grid, "Forbidden oaths", "Override & mastery", Oaths);
        HomeCard(grid, "Learn to rewrite", "Play Overload & Override", arena.StartRewriteLesson);
        var links = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; options.AddChild(links);
        HomeLink(links, "Field guide", Tutorial); HomeLink(links, "Settings", Settings); HomeLink(links, "Controls", Controls); HomeLink(links, "Quit", () => arena.QuitGame());
        HomeLink(links,"Characters",NewFrameMenu);
        goBack = null;
        Text($"Move → remember → change your next action. Hold {arena.Controls.Glyph("preserve")} to save a memory.", 13, muted);
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
        if(HandleFieldPanelInput(input))return;
        if(HandleWorldMapInput(input))return;
        if (TryCaptureKey(input)) GetViewport().SetInputAsHandled();
        else if (MenuVisible && input.IsActionPressed("ui_cancel"))
        { goBack?.Invoke(); arena.Controls.ClearBuffer(); GetViewport().SetInputAsHandled(); }
    }
    private Font DisplayFont(int size) => size >= 21 ? RpgTheme.Heading : ThemeDB.FallbackFont;
    private void Write(Vector2 position, string text, int size, Color color)
    {
        DrawString(DisplayFont(size), position + new Vector2(1, 1), text, fontSize: FontSize(size), modulate: new Color(0, 0, 0, .8f));
        DrawString(DisplayFont(size), position, text, fontSize: FontSize(size), modulate: color);
    }
    private void Bar(Rect2 rect, float fraction, Color color)
    {
        DrawRect(rect, new Color("283842")); DrawRect(new Rect2(rect.Position, new Vector2(rect.Size.X * fraction, rect.Size.Y)), color);
    }
}
