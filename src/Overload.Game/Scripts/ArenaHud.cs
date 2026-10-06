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
    private Color muted => arena.Audio.HighContrast ? new("d2e3ea") : new("8eabb6");
    public bool MenuVisible => menu.Visible;
    public void Initialize(Arena owner) => arena = owner;
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        menu = new CenterContainer { MouseFilter = MouseFilterEnum.Stop };
        AddChild(menu); menu.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color("14232f"), BorderColor = new Color("5f716f"),
            BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 2, BorderWidthBottom = 1,
            ContentMarginLeft = 24, ContentMarginRight = 24, ContentMarginTop = 16, ContentMarginBottom = 16 });
        menu.AddChild(panel);
        scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true };
        panel.AddChild(scroll);
        options = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; options.AddThemeConstantOverride("separation", 10); scroll.AddChild(options);
        Resized += LayoutMenu; LayoutMenu();
    }
    private void LayoutMenu()
    {
        if (scroll is null) return;
        scroll.CustomMinimumSize = new Vector2(Math.Clamp(Size.X - 96, 300, 800), Math.Clamp(Size.Y - 96, 180, 600));
    }
    private int FontSize(int size) => (int)Math.Round(size * arena.Audio.TextPercent / 100f);
    private void ClearMenu(string eyebrow, string title, string subtitle)
    {
        foreach (var child in options.GetChildren()) { options.RemoveChild(child); child.QueueFree(); }
        menu.Show(); capture = null;
        LayoutMenu(); menu.Theme = new Theme { DefaultFontSize=FontSize(18) };
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
        button.AddThemeStyleboxOverride("normal", new StyleBoxFlat { BgColor = new Color("243b49"), ContentMarginLeft = 16, ContentMarginRight = 16 });
        button.AddThemeStyleboxOverride("hover", new StyleBoxFlat { BgColor = new Color("365567") });
        button.AddThemeStyleboxOverride("focus", new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0), BorderColor = gold,
            BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2 });
        button.Pressed += () => { arena.Audio.Play("ui", "UI"); action(); }; options.AddChild(button); if (focus) button.GrabFocus();
    }
    public void Title()
    {
        if (arena.Character is null) { SaveRecovery(); return; }
        ClearMenu("HEARTH / "+arena.Character.State.Frame+" / "+arena.Character.State.Mode.ToString().ToUpperInvariant(), "O V E R L O A D", "A broken court. An old bell. One more attempt.");
        AddButton(arena.Character.State.RunId != Guid.Empty && arena.Character.State.CheckpointRoom < JourneyRules.Length(arena.Character.State) ? $"Resume court — room {arena.Character.State.CheckpointRoom + 1}/{JourneyRules.Length(arena.Character.State)}" : arena.Character.State.RegionalCampaign?"Begin the four-region campaign":"Begin the eight-room court", () => { if (arena.IsSmoke) arena.StartEncounter(0); else arena.StartJourney(); }, true);
        AddButton("Practice the Bellkeeper", () => arena.StartEncounter(3));
        AddButton("Bindings", Bindings);
        AddButton("Fracture board and attunement", Fractures);
        AddButton("Equipment and forge", Inventory);
        AddButton("Character, talents and Codex", CharacterMenu);
        AddButton("Oath journey and Trial of Contradiction", Oaths);
        if(!arena.IsSmoke)AddButton("Create separate Standard character",NewFrameMenu);
        AddButton("Field guide / tutorial", Tutorial); AddButton("Settings", Settings);
        AddButton("Controls", Controls); AddButton("Quit", () => arena.QuitGame());
        goBack = null;
        Text("Move → Pursuit / Crossing   ·   Evade a hit → Afterstrike", 14, muted);
        if (!string.IsNullOrEmpty(arena.Character.Notice)) Text(arena.Character.Notice, 12, gold);
    }
    public void HideMenu() { menu.Hide(); capture = null; QueueRedraw(); }
    public void Pause()
    {
        ClearMenu("TAKE A BREATH", "Paused", "The encounter clock is stopped.");
        AddButton("Continue", arena.TogglePause, true); AddButton("Controls", Controls);
        AddButton("Field guide", Tutorial); AddButton("Settings", Settings);
        AddButton("Retry this encounter", arena.RetryCurrentRoom); AddButton("Return to Hearth", arena.ReturnToTitle);
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
    public override void _Draw()
    {
        if (arena is null || arena.PlayerState is null) return;
        var width = Size.X; var height = Size.Y;
        DrawRect(new Rect2(0, 0, width, 90), new Color("101c29"));
        if (width >= 1200)
        {
            Write(new(30, 28), "O V E R L O A D", 21, gold);
            Write(new(30, 54), $"{arena.Character?.State.Frame.ToString().ToUpperInvariant()} / {arena.RegionTitle}", 12, muted);
            Write(new(30, 76), $"{arena.Character?.State.Mode.ToString().ToUpperInvariant()}  ·  LEVEL {CounterText.Short(arena.Character?.State.ValidatedLevel ?? 1)}", 12, muted);
        }
        var state = arena.PlayerState;
        var left = width < 1200 ? 24 : Math.Max(300, width / 2 - 270);
        Write(new(left, 26), $"LIFE   {CounterText.Short(state.Life / 1000)} / {CounterText.Short(state.MaximumLife / 1000)}", 16, ink);
        Bar(new(left, 34, 230, 10), CombatMath.BarBasisPoints(state.Life, state.MaximumLife) / 10000f, new Color("de8087"));
        Write(new(left, 68), state.RedCovenantActive?$"RESERVED {state.ReservedPermille/10f:0.#}% / 40%":$"FOCUS   {state.Focus / 1000} / {state.MaximumFocus / 1000}", 16, ink);
        Bar(new(left, 76, 230, 5), state.RedCovenantActive?state.ReservedPermille/400f:state.Focus / (float)state.MaximumFocus, state.RedCovenantActive?new Color("de8087"):new Color("70bfd1"));
        DrawMemories(left + 250);
        Write(new(width - 315, 28), $"{arena.Controls.Glyph("pause")}  PAUSE     F3  INSPECT", 14, muted);
        Write(new(width - 315, 53), arena.Playing ? $"{arena.Enemies.Count(e => !e.Enemy!.Dead)} HOSTILES REMAIN" : "ENTER THE COURT", 16, ink);
        Write(new(width - 315, 77), "MOVE → AIM → COMMIT → EVADE", 12, gold);
        DrawRect(new Rect2(0, height - 94, width, 94), new Color("101c29"));
        var input = new[] { "cleave", "pulse", "lance", "special", "evade", "flask" };
        var ids = new[] { arena.EquippedAction(SkillId.Cleave), arena.EquippedAction(SkillId.ShieldPulse), arena.EquippedAction(SkillId.ChainLance), arena.EquippedAction(SkillId.Faultline), SkillId.Traverse, SkillId.Flask };
        var labels = ids.Select(id => id.ToString().ToUpperInvariant()).ToArray();
        var tileWidth = Math.Min(190, (width - 48) / 6);
        var start = (width - tileWidth * 6) / 2;
        for (var i = 0; i < ids.Length; i++)
        {
            var x = start + i * tileWidth; var cooldown = state.Cooldown(ids[i]);
            DrawRect(new Rect2(x, height - 82, tileWidth - 10, 63), new Color("223743"));
            DrawRect(new Rect2(x, height - 82, 3, 63), cooldown > 0 ? muted : gold);
            Write(new(x + 12, height - 60), arena.Controls.Glyph(input[i]), 16, gold); Write(new(x + 12, height - 39), labels[i], 13, ink);
            var hint = cooldown > 0 ? $"{cooldown / 60f:0.0}s" : ids[i] == SkillId.Flask ? $"{state.FlaskCharges} charges" : $"{arena.Balance.Skills.Single(s => s.Id == ids[i]).FocusCost} Focus";
            if (arena.Predictions.TryGetValue(ids[i], out var prediction) && prediction.Selection.Accepted)
                hint = prediction.Selection.Implementation == ActionImplementation.Base ? "Next: base" : $"Next: {prediction.Selection.Implementation}";
            if (ids[i] == SkillId.Traverse && state.ElsewhereActive) hint = cooldown > 0 ? $"{cooldown / 60f:0.0}s" : state.Anchor is null ? "Place anchor" : "Return to anchor";
            if (state.Action is { } active && active.Definition.Id == ids[i] && active.Implementation != ActionImplementation.Base) hint = active.Implementation.ToString();
            if(state.RedCovenantActive&&cooldown==0&&arena.Balance.Skills.Single(s=>s.Id==ids[i]).FocusCost>0)hint=$"{arena.Balance.Skills.Single(s=>s.Id==ids[i]).FocusCost*.4f:0.#}% Life / 4s";
            Write(new(x + 12, height - 24), hint, 11, muted);
        }
        if (arena.Playing)
        {
            Write(new(38, 119), arena.Objective, 15, ink);
            if(arena.PracticeTier is not null) Write(new(38,140),"ISOLATED FRACTURE PRACTICE · Your ordinary character is unchanged.",13,gold);
            if (arena.JourneyActive && arena.Audio.ShowTutorialHints) Write(new(38, 140), arena.JourneyLesson, 13, gold);
            if (arena.OathPractice) Write(new(38, 142), "ELSEWHERE SIMULATION · Place, wait, then return. No instant dodge without an anchor.", 13, gold);
            if(!string.IsNullOrEmpty(arena.EndgameHint))Write(new(38,151),arena.EndgameHint,12,gold);
            if(arena.ActiveMutation is { } mutation)Write(new(38, height-130),WorldLaws.MutationDescription(mutation),12,gold);
            var explanation = arena.Predictions.Values.SelectMany(p => p.Selection.Rejections).FirstOrDefault();
            if (explanation is not null) Write(new(38, height - 105), $"Fallback: {explanation.Reason}", 13, gold);
            var boss = arena.Enemies.FirstOrDefault(e => e.Enemy!.Definition.Role == EnemyRole.Bellkeeper)?.Enemy;
            if (boss is not null)
            {
                var bossY = !string.IsNullOrEmpty(arena.EndgameHint) || arena.PracticeTier is not null || arena.JourneyActive && arena.Audio.ShowTutorialHints ? 172 : 143;
                Write(new(width / 2 - 100, bossY), (boss.Definition.Name??"Bellkeeper").ToUpperInvariant()+(boss.Enraged?" / ENRAGED":""), 14, gold);
                Bar(new(width / 2 - 200, bossY + 10, 400, 6), CombatMath.BarBasisPoints(boss.Life, boss.MaximumLife) / 10000f, gold);
                Bar(new(width / 2 - 200, bossY + 19, 400, 3), boss.Stagger / (float)boss.Definition.StaggerThreshold, new Color("acb9c3"));
            }
        }
        if (arena.Debug)
        {
            DrawRect(new Rect2(38, 180, 410, 228), new Color(0.025f, 0.05f, 0.08f, 0.94f));
            var action = state.Action;
            Write(new(50, 203), $"Tick {state.Tick} | {action?.Definition.Id.ToString() ?? "Idle"} {action?.Phase(state.Tick)}", 14, ink);
            Write(new(50, 224), $"{state.LastSelection?.Implementation.ToString() ?? "Base"} | Strain {state.Strain / 1000f:0.0}/100 | Projectiles {arena.Effects.ProjectileCount}", 13, muted);
            Write(new(50, 245), state.LastReason, 13, gold);
            var memories = state.Memories;
            Write(new(50, 264), $"Travel {memories.MomentumDistance / 32:0.00}m | M/E cooldown {memories.CooldownTicks(MemoryType.Momentum) / 60f:0.0}/{memories.CooldownTicks(MemoryType.Echo) / 60f:0.0}s", 11, muted);
            var fallback = state.LastSelection?.Rejections.FirstOrDefault();
            Write(new(50, 284), fallback is null ? "No rejected pattern candidates" : $"Fallback: {fallback.Reason}", 11, gold);
            var y = 307; foreach (var line in arena.Effects.Log) { Write(new(50, y), line, 12, muted); y += 15; }
        }
        if (menu is not null && menu.Visible) DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.015f, 0.025f, 0.04f, 0.76f));
    }
    private void DrawMemories(float left)
    {
        Write(new(left, 18), "MEMORIES", 11, muted);
        Write(new(left + 140, 18), $"STRAIN {arena.PlayerState.Strain / 1000f:0.#} / 100", 11, gold);
        var memories = arena.PlayerState.Memories;
        foreach (var type in Enum.GetValues<MemoryType>())
        {
            var x = left + ((int)type%2)*140;
            var y = 24+((int)type/2)*32;
            var token = memories.Get(type);
            var color = token is null ? muted : type == MemoryType.Momentum ? new Color("8bedd0") : new Color("c7b3ef");
            DrawRect(new Rect2(x, y, 130, 30), new Color("223743"));
            DrawRect(new Rect2(x, y, 3, 30), color);
            Write(new(x + 9, y+12), type.ToString().ToUpperInvariant(), 10, color);
            var hint=token is null?type switch { MemoryType.Momentum=>"Travel 3m",MemoryType.Echo=>"Evade a hit",MemoryType.Stillness=>"Still 0.8s + hit",_=>"Base stagger break" }:$"Stored {memories.RemainingTicks(type)/60f:0.0}s";
            Write(new(x+9,y+24),hint,10,ink);
            var fraction = token is not null ? memories.RemainingTicks(type) / (float)arena.Balance.Memories.LifetimeTicks
                : type == MemoryType.Momentum ? (float)Math.Min(1, memories.MomentumDistance / arena.Balance.Memories.MomentumDistancePixels) : 0;
            Bar(new(x + 9, y+28, 112, 2), fraction, color);
        }
    }
}
