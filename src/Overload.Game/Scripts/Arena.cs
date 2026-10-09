using Godot;
using Overload.Content;
using Overload.Domain;
using FileAccess = Godot.FileAccess;

namespace Overload.Game;

public partial class Arena : Node
{
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
        IsSmoke = practice is not null || OS.GetCmdlineUserArgs().Any(a => a.StartsWith("--quality-", StringComparison.Ordinal) || a.StartsWith("--world-perf=",StringComparison.Ordinal) || a.StartsWith("--world-soak=",StringComparison.Ordinal) || a is "--identity-smoke" or "--identity-review" or "--world-seed-review" or "--world-smoke" or "--world-review" or "--experience-smoke" or "--experience-review" or "--smoke-test" or "--capture-polish" or "--capture-slice" or "--endless-smoke" or "--capture-endless" or "--production-smoke" or "--capture-production" or "--expansion-smoke" or "--capture-expansion");
        try { Balance = ProfileLoader.Load(FileAccess.GetFileAsString("res://Content/arena.json")); Balance = ExpansionLoader.Load(Balance, FileAccess.GetFileAsString("res://Content/expansion.json")); ReleaseContent.Load(FileAccess.GetFileAsString("res://Content/release.json")); }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); return; }
        BaseBalance = Balance;
        PlayerState = new(Balance, new ArenaActionPreflight(this));
        OpenCharacter();
        Controls = new(IsSmoke);
        Input.JoyConnectionChanged += ControllerConnectionChanged;
        Audio = new SliceAudio(); AddChild(Audio); Audio.Initialize(IsSmoke);
        Input.UseAccumulatedInput = false;
        worldContainer = new SubViewportContainer { Stretch = false, Size = new Vector2(640, 360), MouseFilter = Control.MouseFilterEnum.Ignore };
        AddChild(worldContainer);
        var viewport = new SubViewport { Size = new Vector2I(640, 360), RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            CanvasItemDefaultTextureFilter = Viewport.DefaultCanvasItemTextureFilter.Nearest, HandleInputLocally = false };
        worldContainer.AddChild(viewport);
        LayoutWorld();
        world = new WorldView(); viewport.AddChild(world);
        viewCamera = new Camera2D { Position = new Vector2(320, 180), PositionSmoothingEnabled = false }; world.AddChild(viewCamera);
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
        GD.Print("OVERLOAD_READY arena.v1");
    }
    public override void _Process(double delta)
    {
        LayoutWorld(); MeasureQualityFrame();MeasureWorldQuality();
        if (Player is not null) RenderPresentation(delta);
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
        worldContainer.Scale = Vector2.One * integerScale / outputScale;
        worldContainer.Position = (available - new Vector2(640, 360) * worldContainer.Scale) / 2;
    }
    public Vector2 WorldToWindow(Vector2 point) => GetViewport().GetScreenTransform() * (worldContainer.Position + (point-CameraOrigin) * worldContainer.Scale);
    public Vector2 CameraOrigin => viewCamera is null?Vector2.Zero:viewCamera.Position-new Vector2(320,180);
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
        EndRewriteLesson();ResetWorldNavigation();
        Hud.ResetDispatch();
        FlushWorldFog();if(WorldActive&&!GraphFracture)WorldTransaction($"world.return:{Guid.NewGuid():N}",WorldRules.Resume);WorldActive=false;LocalMapVisible=false;
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
        if(LocalMapVisible){LocalMapVisible=false;Controls.ClearBuffer();Audio.SetPaused(Paused);return;}
        if ((!Playing && !CheckpointRest) || PlayerState.Dead) return;
        if(WorldActive&&!GraphFracture&&Character!.State.World is {Ending:null} pending&&pending.Resolved.Contains(Region.Crown))
        {Paused=true;Controls.ClearBuffer();Audio.SetPaused(true);Hud.WorldEnding();return;}
        Paused = !Paused; Controls.ClearBuffer();
        Audio.SetPaused(Paused);
        if (Paused) Hud.Pause(); else Hud.HideMenu();
    }
    public override void _UnhandledInput(InputEvent input)
    {
        Controls.Observe(input);
        if (Hud.TryCaptureKey(input)) { GetViewport().SetInputAsHandled(); return; }
        if(WorldActive&&!PlayerState.Dead&&!input.IsEcho())
        {
            if(!Paused&&input.IsActionPressed("track")){CycleWorldTarget();GetViewport().SetInputAsHandled();return;}
            if(!Paused&&input.IsActionPressed("local_map")){FlushWorldFog();LocalMapVisible=!LocalMapVisible;Controls.ClearBuffer();Audio.SetPaused(LocalMapVisible);GetViewport().SetInputAsHandled();return;}
            if(input.IsActionPressed("region_map")){OpenRegionalMap();GetViewport().SetInputAsHandled();return;}
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
        var mouse = (Controls.PointerPosition - worldContainer.Position) / worldContainer.Scale + CameraOrigin;
        var intent = Controls.Read(PlayerState.Tick, Player.Position, mouse);
        if (intent.Move.LengthSquared() > 0) PlayerState.CancelRecoveryByMovement();
        if (intent.Action is { } id)
        {
            id = EquippedAction(id);
            var aim = id == SkillId.Traverse && intent.Move.LengthSquared() > 0 ? intent.Move : intent.Aim;
            if (PlayerState.TryStart(id, new(aim.X, aim.Y), intent.PreserveMemories))
            {
                Controls.ClearBuffer();
                if (id == SkillId.Traverse) Audio.Play("evade", "Player");
                else if (id == SkillId.Flask) Audio.Play("heal", "Player");
                if (PlayerState.Action?.Implementation is { } implementation && implementation != ActionImplementation.Base) Audio.Play("overload", "Player");
            }
            else if (id != FrameRules.Basic(Character?.State.Frame ?? FrameId.Warden)) NoticeRejectedAction();
        }
        Player.Facing = intent.Aim;
        var velocity = intent.Move * Balance.Hero.Speed;
        if (PlayerState.Action is { } action)
        {
            if (action.Definition.Id == SkillId.Traverse)
                velocity = new Vector2(action.Aim.X, action.Aim.Y) * (Balance.Hero.EvadeDistance * 60 / Balance.Hero.EvadeTicks);
            else if (action.Phase(PlayerState.Tick) != ActionPhase.Recovery) velocity *= 0.35f;
        }
        var beforeMovement = Player.Position;
        qualityStart=QualityTimestamp;
        var movementSource = PlayerState.Action?.Source ?? SourceKind.BasePlayerAction;
        var movementKind = PlayerState.Action?.Definition.Id == SkillId.Traverse ? LocomotionKind.StandardEvade : LocomotionKind.Walk;
        motion.Move(velocity);
        var displacement = Player.Position - beforeMovement;
        ObserveExperience(displacement);
        foreach (var enemy in Enemies.OrderBy(e => e.ActorId))
            if(EngagedEnemy(enemy)) director.Advance(enemy, Player, PlayerState.Tick, Effects,ActiveMutation,Enemies,world.Navigation);
        RecordQualityStage("movement/director",qualityStart);qualityStart=QualityTimestamp;
        PlayerState.Memories.ObservePosition(new(Player.Position.X,Player.Position.Y));
        Effects.PlayerAction();
        if (qualityMode == "perf") Effects.FillQualityStress();
        Effects.Advance();laws.Advance();
        if(WorldActive){AdvanceWorld();AdvanceWorldNavigation();}
        AdvanceRewriteLesson();
        RecordQualityStage("effects",qualityStart);qualityStart=QualityTimestamp;
        if(FractureActive && Character!.State.Fracture!.Region==Region.Ash&&PlayerState.Memories.Echo is not null)masteryEarned=true;
        if (!PlayerState.Dead)
        {
            PlayerState.SetCombatActive(HasEngagedEnemies);
            // Zero intent or collision recovery opposite to intent cannot earn locomotion credit.
            if (velocity.LengthSquared() > 0 && displacement.Dot(velocity) > 0)
                PlayerState.Memories.ObserveLocomotion(new(displacement.X, displacement.Y), movementKind, movementSource);
        }
        Effects.RecordMemories();
        PublishPredictions(intent.Aim, intent.Move);
        RecordQualityStage("predictions",qualityStart);
        foreach (var body in Enemies.Append(Player))
        {
            body.ReducedFlash = Audio.ReducedFlash;
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
