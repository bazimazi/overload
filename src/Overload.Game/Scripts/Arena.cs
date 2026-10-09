using Godot;
using Overload.Content;
using Overload.Domain;
using FileAccess = Godot.FileAccess;

namespace Overload.Game;

public partial class Arena : Node
{
    public const string BuildId="graphics.2026-10-09";
    public BalanceProfile Balance { get; private set; } = null!;
    public BalanceProfile BaseBalance { get; private set; } = null!;
    public PlayerCombat PlayerState { get; private set; } = null!;
    public ActorBody Player { get; private set; } = null!;
    public List<ActorBody> Enemies { get; } = [];
    public InputRouter Controls { get; private set; } = null!;
    public CombatEffects Effects { get; private set; } = null!;
    public ArenaHud Hud { get; private set; } = null!;
    public SliceAudio Audio { get; private set; } = null!;
    public bool Playing { get; private set; }
    public bool Paused { get; private set; }
    public bool Debug { get; private set; }
    public int Wave { get; private set; }
    public bool IsSmoke { get; private set; }
    public Dictionary<SkillId, ActionPlan> Predictions { get; } = [];
    public string RoomId => WorldActive ? WorldZone.Id : FractureActive ? $"fracture.{activeExpeditionId:N}.{FractureGroup}" : JourneyActive ? $"court.{JourneyRoom}" : $"arena.{Wave}";
    public bool OathPractice { get; private set; }
    private bool practiceReturnOath;
    public void StartOathPractice()
    {
        if (Playing) return;
        practiceReturnOath = PlayerState.ElsewhereActive;
        PlayerState.TryChangeOaths(["oath.elsewhere"]); OathPractice = true; StartEncounter(0);
    }
    private string EncounterBossName=>Enemies.FirstOrDefault(e=>e.Enemy!.Definition.Role==EnemyRole.Bellkeeper)?.Enemy!.Definition.Name??"Bellkeeper";
    public string Objective => WorldActive ? WorldObjective : FractureActive ? $"FRACTURE {CounterText.Short(EncounterTier)} / GROUP {FractureGroup + 1}/7" : JourneyActive ? $"{JourneyRoom + 1}/{JourneyRules.Length(Character!.State)} / {JourneyName.ToUpperInvariant()}" : Wave == 3 ? $"{EncounterBossName.ToUpperInvariant()} / Watch the tell. Take the opening." : $"THE BROKEN COURT  /  Encounter {Wave + 1} of 3";
    private WorldView world = null!;
    private SubViewportContainer worldContainer = null!;
    private const int WorldRasterScale=2;
    private Vector2 WorldScale=>worldContainer.Scale*WorldRasterScale;
    private readonly EnemyDirector director = new();
    private IntegrationSmoke? smoke;
    private EndlessSmoke? endlessSmoke;
    public System.Numerics.BigInteger? PracticeTier { get; private set; }
    private PatternMotion motion = null!;
    private int nextActorId = 1;
    private int transitionTicks;
    private bool deathShown;
    private bool quitting;
    private ulong lastRedrawPhysicsFrame=ulong.MaxValue;
    public async void QuitGame(int exitCode = 0)
    {
        if (quitting) return;
        ParkWorld();
        quitting = true; Playing = false; Paused = true; Controls?.ClearBuffer(); Audio?.StopAll();
        // Audio commands are mixed on another thread. Let stopped playback references retire before engine teardown.
        await Task.Delay(150);
        GetTree().Quit(exitCode);
    }
    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest) QuitGame();
        if (what == NotificationWMWindowFocusOut && !IsSmoke && (Playing || CheckpointRest) && !Paused)
        {LocalMapVisible=false;TogglePause();}
    }

    public override void _Ready()
    {
        GetTree().AutoAcceptQuit = false;
        DisplayServer.WindowSetTitle("Overload — The Palimpsest");
        // Simulation is 60 Hz. Bound redundant renders on high-refresh displays while retaining headroom.
        Engine.MaxFps=DisplayServer.GetName()=="headless"?0:120;
        var practice = OS.GetCmdlineUserArgs().FirstOrDefault(a=>a.StartsWith("--fracture-practice=",StringComparison.Ordinal));
        if(practice is not null)
        {
            try { PracticeTier=Quantity.Parse(practice.Split('=')[1]).Value; if(PracticeTier<1) throw new FormatException(); }
            catch(FormatException) { GD.PushError("Practice tier must be a positive decimal integer");GetTree().Quit(1);return; }
        }
        IsSmoke = practice is not null || OS.GetCmdlineUserArgs().Any(a => a.StartsWith("--quality-", StringComparison.Ordinal) || a.StartsWith("--world-perf=",StringComparison.Ordinal) || a.StartsWith("--world-soak=",StringComparison.Ordinal) || a is "--open-world-smoke" or "--open-world-review" or "--rpg-smoke" or "--rpg-review" or "--identity-smoke" or "--identity-review" or "--world-seed-review" or "--world-smoke" or "--world-review" or "--experience-smoke" or "--experience-review" or "--smoke-test" or "--capture-polish" or "--capture-slice" or "--endless-smoke" or "--capture-endless" or "--production-smoke" or "--capture-production" or "--expansion-smoke" or "--capture-expansion");
        try { Balance = ProfileLoader.Load(FileAccess.GetFileAsString("res://Content/arena.json")); Balance = ExpansionLoader.Load(Balance, FileAccess.GetFileAsString("res://Content/expansion.json")); ReleaseContent.Load(FileAccess.GetFileAsString("res://Content/release.json")); }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); return; }
        BaseBalance = Balance;
        IsSmoke |= OS.GetCmdlineUserArgs().Any(a=>a is "--motion-smoke" or "--motion-review");
        IsSmoke |= OS.GetCmdlineUserArgs().Any(a=>a is "--adventure-smoke" or "--adventure-review" or "--campaign-smoke" or "--campaign-review");
        IsSmoke |= OS.GetCmdlineUserArgs().Any(a=>a is "--graphics-smoke" or "--graphics-review");
        PlayerState = new(Balance, new ArenaActionPreflight(this));
        OpenCharacter();
        Controls = new(IsSmoke);
        Input.JoyConnectionChanged += ControllerConnectionChanged;
        Audio = new SliceAudio(); AddChild(Audio); Audio.Initialize(IsSmoke);
        Input.UseAccumulatedInput = false;
        worldContainer = new SubViewportContainer { Stretch = false, Size = new Vector2(1280, 720), MouseFilter = Control.MouseFilterEnum.Ignore,TextureFilter=CanvasItem.TextureFilterEnum.Linear };
        AddChild(worldContainer);
        var viewport = new SubViewport { Size = new Vector2I(1280, 720), RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            CanvasItemDefaultTextureFilter = Viewport.DefaultCanvasItemTextureFilter.Nearest, HandleInputLocally = false };
        worldContainer.AddChild(viewport);
        LayoutWorld();
        world = new WorldView {PerformanceOwner=this}; viewport.AddChild(world);
        viewCamera = new Camera2D { Position = new Vector2(320, 180), Zoom=Vector2.One*WorldRasterScale,PositionSmoothingEnabled = false }; world.AddChild(viewCamera);
        Player = new ActorBody { ActorId = 0, Position = new Vector2(140, 190) }; world.AddChild(Player);
        motion = new(this);
        Effects = new CombatEffects { ZIndex = 100 }; Effects.Initialize(this); world.AddChild(Effects);
        laws=new EndgameWorld { ZIndex=90 };laws.Initialize(this);world.AddChild(laws);
        var canvas = new CanvasLayer(); AddChild(canvas);
        Hud = new ArenaHud(); Hud.Initialize(this); canvas.AddChild(Hud);
        Hud.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        Hud.Title();
        if (OS.GetCmdlineUserArgs().Contains("--smoke-test")) smoke = new IntegrationSmoke(this);
        if (OS.GetCmdlineUserArgs().Contains("--endless-smoke")) endlessSmoke = new EndlessSmoke(this);
        if(PracticeTier is { } initialTier) CallDeferred(nameof(BeginPractice));
        if (OS.GetCmdlineUserArgs().Contains("--capture-slice")) CaptureSlice();
        if (OS.GetCmdlineUserArgs().Contains("--capture-endless")) CaptureEndless();
        if (OS.GetCmdlineUserArgs().Contains("--capture")) CapturePreview();
        if(OS.GetCmdlineUserArgs().Contains("--capture-production"))CaptureProduction();
        if(OS.GetCmdlineUserArgs().Contains("--production-smoke"))CallDeferred(nameof(RunProductionSmoke));
        if(OS.GetCmdlineUserArgs().Contains("--expansion-smoke")||OS.GetCmdlineUserArgs().Contains("--capture-expansion"))CallDeferred(nameof(RunExpansionSmoke));
        if(OS.GetCmdlineUserArgs().Any(a=>a.StartsWith("--quality-",StringComparison.Ordinal)))CallDeferred(nameof(BeginQuality));
        if(OS.GetCmdlineUserArgs().Contains("--capture-polish"))CallDeferred(nameof(ReviewPolish));
        if(OS.GetCmdlineUserArgs().Any(a=>a is "--experience-smoke" or "--experience-review"))CallDeferred(nameof(ReviewExperience));
        if(OS.GetCmdlineUserArgs().Any(a=>a is "--world-smoke" or "--world-review"))CallDeferred(nameof(RunWorldSmoke));
        if(OS.GetCmdlineUserArgs().Any(a=>a.StartsWith("--world-perf=",StringComparison.Ordinal)||a.StartsWith("--world-soak=",StringComparison.Ordinal)))CallDeferred(nameof(BeginWorldQuality));
        if(OS.GetCmdlineUserArgs().Contains("--world-seed-review"))CallDeferred(nameof(ReviewWorldSeeds));
        if(OS.GetCmdlineUserArgs().Any(a=>a is "--identity-smoke" or "--identity-review"))CallDeferred(nameof(ReviewIdentity));
        if(OS.GetCmdlineUserArgs().Any(a=>a is "--graphics-smoke" or "--graphics-review"))CallDeferred(nameof(ReviewGraphics));
        if(OS.GetCmdlineUserArgs().Any(a=>a is "--rpg-smoke" or "--rpg-review"))CallDeferred(nameof(ReviewRpg));
        if(OS.GetCmdlineUserArgs().Any(a=>a is "--open-world-smoke" or "--open-world-review"))CallDeferred(nameof(ReviewOpenWorld));
        if(OS.GetCmdlineUserArgs().Any(a=>a is "--motion-smoke" or "--motion-review"))CallDeferred(nameof(ReviewMotion));
        if(OS.GetCmdlineUserArgs().Any(a=>a is "--adventure-smoke" or "--adventure-review" or "--campaign-smoke" or "--campaign-review"))CallDeferred(nameof(ReviewAdventure));
        if(!IsSmoke&&Character?.State.World is not null)CallDeferred(nameof(StartWorld));
        GD.Print("OVERLOAD_READY "+BuildId);
    }
    public override void _Process(double delta)
    {
        LayoutWorld(); MeasureQualityFrame();MeasureWorldQuality();
        if (Player is not null) RenderPresentation(delta);
        RecordMotionFrame();
        var frame=Engine.GetPhysicsFrames();
        if(Player is null||frame==lastRedrawPhysicsFrame)return;
        lastRedrawPhysicsFrame=frame;
        Effects.QueueRedraw();Hud.QueueRedraw();
    }
    private void ControllerConnectionChanged(long device, bool connected)
    {
        if (connected || !Controls.Controller) return;
        Controls.Disconnected();
        if (!IsSmoke && Playing && !Paused) {LocalMapVisible=false;TogglePause();}
    }
    private void BeginPractice() { if(PracticeTier is { } tier) StartFracture(tier); }
    private void LayoutWorld()
    {
        if (worldContainer is null) return;
        var root = GetViewport();
        var available = root.GetVisibleRect().Size;
        var outputScale = DisplayServer.GetName() == "headless" ? 1 : root.GetScreenTransform().Scale.X;
        var integerScale = Math.Max(1, Mathf.Floor(Math.Min(available.X / 640, available.Y / 360) * outputScale));
        worldContainer.Scale = Vector2.One * integerScale / outputScale / WorldRasterScale;
        worldContainer.Position = (available - new Vector2(640, 360) * WorldScale) / 2;
    }
    public Vector2 WorldToWindow(Vector2 point) => GetViewport().GetScreenTransform() * (worldContainer.Position + (point-CameraOrigin) * WorldScale);
    public Vector2 CameraOrigin => viewCamera is null?Vector2.Zero:viewCamera.Position+viewCamera.Offset-new Vector2(320,180);
    private async void CapturePreview()
    {
        if (DisplayServer.GetName() == "headless") { GD.PushError("Capture requires a renderer"); GetTree().Quit(1); return; }
        var directory = System.IO.Path.Combine(ProjectSettings.GlobalizePath("res://"), "../../artifacts/screenshots");
        System.IO.Directory.CreateDirectory(directory);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory, "title.png"));
        StartEncounter(3);
        Player.Position = new Vector2(350, 180);
        for (var i = 0; i < 90; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory, "bellkeeper.png"));
        TogglePause();
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory, "pause.png"));
        GD.Print("OVERLOAD_CAPTURE_OK"); QuitGame();
    }
    public void StartEncounter(int wave, bool rest = true)
    {
        ResetPointerTravel();
        ResetWorldNavigation();
        WorldActive=false;LocalMapVisible=false;
        Effects.ZIndex=100;laws.ZIndex=90;
        FractureActive = false; EncounterTier = 1; TrialActive=false;SovereignActive=false;laws?.Clear();
        JourneyActive = false; world.SetRegion(null); world.Configure(WorldView.Walls.Skip(4).ToArray());
        deathReceipt=Guid.NewGuid();
        Wave = wave; Playing = true; Paused = false; transitionTicks = 0; deathShown = false;
        Audio.SetPaused(false);
        Controls.ClearBuffer(); PlayerState.ResetForLevel(rest); Effects.Reset(); director.Reset();
        ResetExperience();
        foreach (var enemy in Enemies) { enemy.CollisionLayer = 0; enemy.CollisionMask = 0; enemy.QueueFree(); }
        Enemies.Clear();
        Player.Position = new Vector2(140, 190); Player.Velocity = Vector2.Zero; Player.Facing = Vector2.Right;
        Player.TeleportVisual(); presentedRoom = ""; ArrivalTime = 2.6f;
        motion.Reset(); Player.SetFrame(Character?.State.Frame??FrameId.Warden);
        if (wave == 3) Spawn(EnemyRole.Bellkeeper, new(430, 180));
        else
        {
            Spawn(EnemyRole.Pursuer, new(365, 130)); Spawn(EnemyRole.Pursuer, new(460, 240));
            if (wave >= 1) Spawn(EnemyRole.Caster, new(520, 120));
            if (wave >= 2) { Spawn(EnemyRole.Brute, new(370, 270)); Spawn(EnemyRole.Caster, new(530, 260)); }
        }
        Hud.HideMenu(); Effects.Record(wave == 3 ? "Bellkeeper checkpoint restored" : $"Encounter {wave + 1} entered");
    }
    public ActorBody Spawn(EnemyRole role, Vector2 position) => Spawn(Balance.Enemies.Single(e => e.Role == role),position);
    public ActorBody Spawn(EnemyDefinition definition, Vector2 position)
    {
        var body = new ActorBody { ActorId = nextActorId++, Position = position, Enemy = new(definition, EncounterTier) };
        world.AddChild(body); Enemies.Add(body); return body;
    }
    public void ReturnToTitle()
    {
        ResetPointerTravel();
        EndRewriteLesson();ResetWorldNavigation();
        Hud.ResetDispatch();
        ParkWorld();WorldActive=false;LocalMapVisible=false;
        CheckpointRest=false;
        FractureActive = false; EncounterTier = 1; TrialActive=false;SovereignActive=false;laws?.Clear();
        covenantChallenge=false; JourneyActive = false; Playing = false; Paused = false; Controls.ClearBuffer(); motion.Reset(); PlayerState.Reset();
        if (OathPractice) { PlayerState.TryChangeOaths(practiceReturnOath ? ["oath.elsewhere"] : []); OathPractice = false; }
        ApplyCharacterBuild();
        PlayerState.Memories.Clear(MemoryClearReason.EncounterExit); Predictions.Clear(); Effects.Reset(); Audio.SetPaused(false);
        foreach (var enemy in Enemies) { enemy.CollisionLayer = 0; enemy.QueueFree(); } Enemies.Clear();
        Player.Position = new(430, 224); Player.Velocity = Vector2.Zero; Player.Facing = Vector2.Down; Player.TeleportVisual(); Hud.Title();
    }
    public void TogglePause()
    {
        ResetPointerTravel();
        if(LocalMapVisible){LocalMapVisible=false;Controls.ClearBuffer();Audio.SetPaused(Paused);return;}
        if ((!Playing && !CheckpointRest) || PlayerState.Dead) return;
        if(WorldActive&&!GraphFracture&&Character!.State.World is {Ending:null} pending&&pending.Resolved.Contains(Region.Crown))
        {Paused=true;Controls.ClearBuffer();Audio.SetPaused(true);Hud.WorldEnding();return;}
        Paused = !Paused; Controls.ClearBuffer();
        if(!Paused)Controls.SuppressPrimaryUntilRelease();
        Audio.SetPaused(Paused);
        if (Paused) Hud.Pause(); else Hud.HideMenu();
    }
    public override void _UnhandledInput(InputEvent input)
    {
        Controls.Observe(input);
        if (Hud.TryCaptureKey(input)) { GetViewport().SetInputAsHandled(); return; }
        if(input.IsActionPressed("move_to")){BeginPointerTravel();GetViewport().SetInputAsHandled();return;}
        if(input.IsActionReleased("move_to")){pointerSteering=false;GetViewport().SetInputAsHandled();return;}
        if(WorldActive&&!PlayerState.Dead&&!input.IsEcho())
        {
            if(!Paused&&!LocalMapVisible&&input is InputEventMouseButton {ButtonIndex:MouseButton.Left}&&!Input.IsActionPressed("stand_ground")&&input.IsActionPressed("cleave")&&TryClickLoot()){GetViewport().SetInputAsHandled();return;}
            if(input is InputEventMouseButton {ButtonIndex:MouseButton.Left})
            {
                if(input.IsActionReleased("cleave"))primaryWalkHeld=false;
                if(input.IsActionPressed("cleave")&&TryPrimaryMouse()){GetViewport().SetInputAsHandled();return;}
            }
            if(!Paused&&input.IsActionPressed("town_portal")){BeginTownPortal();GetViewport().SetInputAsHandled();return;}
            if(!Paused&&input.IsActionPressed("interact")&&TryPickUpNearby()){GetViewport().SetInputAsHandled();return;}
            if(!Paused&&input.IsActionPressed("track")){CycleWorldTarget();GetViewport().SetInputAsHandled();return;}
            if(!Paused&&input.IsActionPressed("local_map")){ToggleLocalMap();GetViewport().SetInputAsHandled();return;}
            if(input.IsActionPressed("region_map")){if(Paused&&Hud.WorldAtlasVisible)CloseWorldMenu();else OpenRegionalMap();GetViewport().SetInputAsHandled();return;}
            if(LocalMapVisible&&input.IsActionPressed("ui_cancel")){ToggleLocalMap();GetViewport().SetInputAsHandled();return;}
            if(!Paused&&input.IsActionPressed("interact")){InteractWorld();GetViewport().SetInputAsHandled();return;}
        }
        if (input.IsActionPressed("pause") && (Playing || CheckpointRest)) { TogglePause(); GetViewport().SetInputAsHandled(); }
        if (input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F3 }) Debug = !Debug;
    }
    public override void _PhysicsProcess(double delta)
    {
        if (Balance is null) return;
        var qualityStart=QualityTimestamp;
        AdvanceQuality();
        AdvanceWorldQuality();
        RecordQualityStage("fixture",qualityStart);
        smoke?.BeforeTick();
        endlessSmoke?.Tick();
        if (CheckpointRest) { AdvanceCheckpoint();smoke?.AfterTick();return; }
        if (!Playing || Paused || LocalMapVisible || PlayerState.Dead)
        {
            if (PlayerState.Dead && !deathShown) { deathShown = true; motion.Reset(); Effects.Reset(); if(BankEndgameDeath())Hud.Death(); }
            smoke?.AfterTick(); return;
        }
        PlayerState.AdvanceClock();
        foreach (var body in Enemies.Append(Player)) body.BeginTick();
        // Every live hostile in this authored arena actively pursues or targets the Warden.
        PlayerState.SetCombatActive(HasEngagedEnemies);
        Player.Flash = false;
        foreach (var enemy in Enemies) enemy.Flash = false;
        var mouse = (Controls.PointerPosition - worldContainer.Position) / WorldScale + CameraOrigin;
        var intent = PointerIntent(PrimaryIntent(Controls.Read(PlayerState.Tick, Player.Position, mouse)));
        if(Paused){smoke?.AfterTick();return;}
        if (intent.Move.LengthSquared() > 0) PlayerState.CancelRecoveryByMovement();
        if (intent.Action is { } id)
        {
            id = EquippedAction(id);
            var aim = id == SkillId.Traverse && intent.Move.LengthSquared() > 0 ? intent.Move : intent.Aim;
            if (PlayerState.TryStart(id, new(aim.X, aim.Y), intent.PreserveMemories))
            {
                PrimaryActionStarted(id);
                Controls.ClearBuffer();
                if (id == SkillId.Traverse) Audio.Play("evade", "Player");
                else if (id == SkillId.Flask) Audio.Play("heal", "Player");
                if (PlayerState.Action?.Implementation is { } implementation && implementation != ActionImplementation.Base) Audio.Play("overload", "Player");
            }
            else if (id != FrameRules.Basic(Character?.State.Frame ?? FrameId.Warden)) NoticeRejectedAction();
        }
        FacePlayer(intent.Move, intent.Aim);
        var velocity = ResolvePlayerMovement(intent.Move, delta);
        var beforeMovement = Player.Position;
        qualityStart=QualityTimestamp;
        var movementSource = PlayerState.Action?.Source ?? SourceKind.BasePlayerAction;
        var movementKind = PlayerState.Action?.Definition.Id == SkillId.Traverse ? LocomotionKind.StandardEvade : LocomotionKind.Walk;
        motion.Move(velocity);
        FinishPlayerMovement();
        var displacement = Player.Position - beforeMovement;
        ObserveExperience(displacement);
        foreach (var enemy in Enemies.OrderBy(e => e.ActorId))
            if(EngagedEnemy(enemy)) director.Advance(enemy, Player, PlayerState.Tick, Effects,ActiveMutation,Enemies,world.Navigation);
        AdvanceElites();
        RecordQualityStage("movement/director",qualityStart);qualityStart=QualityTimestamp;
        PlayerState.Memories.ObservePosition(new(Player.Position.X,Player.Position.Y));
        Effects.PlayerAction();
        if (qualityMode == "perf") Effects.FillQualityStress();
        Effects.Advance();laws.Advance();
        if(WorldActive){AdvanceWorld();AdvanceWorldNavigation();AdvanceAdventure();}
        AdvanceRewriteLesson();
        RecordQualityStage("effects",qualityStart);qualityStart=QualityTimestamp;
        if(FractureActive && Character!.State.Fracture!.Region==Region.Ash&&PlayerState.Memories.Echo is not null)masteryEarned=true;
        if (!PlayerState.Dead)
        {
            PlayerState.SetCombatActive(HasEngagedEnemies);
            // Only actual travel along the resolved movement can earn locomotion credit.
            if (velocity.LengthSquared() > 0 && displacement.Dot(velocity) > 0)
                PlayerState.Memories.ObserveLocomotion(new(displacement.X, displacement.Y), movementKind, movementSource);
        }
        Effects.RecordMemories();
        PublishPredictions(intent.Aim, intent.Move);
        RecordQualityStage("predictions",qualityStart);
        var highlighted=HighlightedEnemy;
        foreach (var body in Enemies.Append(Player))
        {
            body.EndTick();
            body.ReducedFlash = Audio.ReducedFlash;
            body.HighContrast=Audio.HighContrast;
            body.ThreatVisible=body==highlighted||body.Enemy is not null&&EngagedEnemy(body)&&body.Position.DistanceTo(Player.Position)<230;
            body.ZIndex = (int)body.Position.Y / 4;
            body.DebugFootprint = Debug;
        }
        Player.AnchorHalo=Character?.State.SovereignRewards.Contains("echo.anchor")==true;
        Player.Evasion = PlayerState.Evading;
        Player.PublishPose(PlayerState);
        if (!WorldActive && !RewriteLessonActive && !PlayerState.Dead && Enemies.All(e => e.Enemy!.Dead) && EndgameObjectiveReady)
        {
            transitionTicks++;
            if (transitionTicks >= 100)
            {
                if (TrialActive) CompleteTrialStage();
                else if(SovereignActive)CompleteSovereign();
                else if (FractureActive) CompleteFractureGroup();
                else if (JourneyActive) CompleteJourneyRoom();
                else if (Wave < 3) StartEncounter(Wave + 1);
                else { Playing = false; Effects.Reset(); Hud.Victory(); }
            }
        }
        smoke?.AfterTick();
    }
    public void PublishPredictions(Vector2 aim, Vector2 move)
    {
        foreach (var skill in Balance.Skills)
        {
            var direction = skill.Id == SkillId.Traverse && move.LengthSquared() > 0 ? move : aim;
            Predictions[skill.Id] = PlayerState.Preview(skill.Id, new(direction.X, direction.Y), Controls.PreservingMemories);
        }
    }
    public SkillId EquippedAction(SkillId input) => Character is null ? input : input switch
    { SkillId.Cleave => FrameRules.Basic(Character.State.Frame), SkillId.ShieldPulse => Character.State.EquippedSkills[0], SkillId.ChainLance => Character.State.EquippedSkills[1], SkillId.Faultline => Character.State.EquippedSkills[2], _ => input };
}
