using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Godot;
using Overload.Content;
using Overload.Domain;
using BigInteger = System.Numerics.BigInteger;

namespace Overload.Game;

// Explicit command-line fixtures always use CharacterSession's isolated test profiles.
public partial class Arena
{
    private string qualityMode = "";
    private readonly Stopwatch qualityClock = new();
    private double qualityPreviousFrame;
    private double qualitySeconds;
    private long qualityTicks;
    private int qualityCheckpoints;
    private int qualitySignatures;
    private long qualityLastRoot;
    private int qualityPeakStrain;
    private bool qualityCaptured;
    private readonly List<double> qualityFrames = [];
    private readonly List<object> qualityMemory = [];
    private readonly Dictionary<string,List<double>> qualityStages=[];
    public long QualityTimestamp => qualityMode is "perf" or "ordinary" || worldQualityClock.IsRunning&&!worldSoak ? Stopwatch.GetTimestamp() : 0;
    public void RecordQualityStage(string name,long began)
    {
        if(began==0)return;
        if(!qualityStages.TryGetValue(name,out var samples))qualityStages[name]=samples=[];
        if(samples.Count<128000)samples.Add(Stopwatch.GetElapsedTime(began).TotalMilliseconds);
    }
    private object QualityStageSummary() => qualityStages.ToDictionary(pair=>pair.Key,pair=>
    {
        var values=pair.Value.Order().ToArray();return new {samples=values.Length,meanMs=values.Average(),p95Ms=values[(int)(values.Length*.95)],maxMs=values[^1]};
    });
    private string QualityDirectory => System.IO.Path.Combine(ProjectSettings.GlobalizePath("res://"), "../../artifacts/quality");
    private void WriteQuality(string name, object value)
    {
        System.IO.Directory.CreateDirectory(QualityDirectory);
        System.IO.File.WriteAllText(System.IO.Path.Combine(QualityDirectory, name), JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
    }
    private void BeginQuality()
    {
        var arg = OS.GetCmdlineUserArgs().First(a => a.StartsWith("--quality-", StringComparison.Ordinal));
        if (arg == "--quality-matrix") { QualityMatrix(); return; }
        if (arg == "--quality-install") { QualityInstall(); return; }
        qualityMode = arg.StartsWith("--quality-perf=", StringComparison.Ordinal) ? "perf" : arg.StartsWith("--quality-ordinary=",StringComparison.Ordinal)?"ordinary":"soak";
        if (!double.TryParse(arg.Split('=').Last(), CultureInfo.InvariantCulture, out qualitySeconds) || qualitySeconds < 1 || qualitySeconds > 7200)
        { qualityMode=""; GD.PushError("Quality duration must be 1..7200 seconds"); QuitGame(1); return; }
        if (qualityMode != "soak" && DisplayServer.GetName() == "headless")
        { qualityMode=""; GD.PushError("Performance measurement requires a renderer"); QuitGame(1); return; }
        Engine.MaxFps = qualityMode=="soak"?0:120; DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
        if (qualityMode == "soak")
        {
            Engine.PhysicsTicksPerSecond = 1920; Engine.TimeScale = 32; Engine.MaxPhysicsStepsPerFrame = 64;
            UpdateCharacter(s => EndlessFixtures.Reference(10) with { CharacterId=s.CharacterId });
            StartRegional(10, Region.Ash, ActivityFamily.Hunt);
        }
        else
        {
            UpdateCharacter(s => FrameRules.BuildFixture(FrameId.Threadseer, 0) with { CharacterId=s.CharacterId,
                Bindings=[new("pattern.assault.afterstrike",0),new("pattern.assault.pursuit",1),new("pattern.traverse.crossing",2)] }); StartEncounter(2);
            if(qualityMode=="perf")
            {
                ClearEncounterEnemies(); world.Configure([]);
                for (var i = 0; i < 60; i++)
                    Spawn(BaseBalance.Enemies[i % 3] with { Life = 1000000000 }, new(300 + i % 10 * 26, 100 + i / 10 * 30));
            }
        }
        PlayerState.ChangeMaximumLife(BigInteger.Pow(10, 20)); PlayerState.Reset();
        qualityClock.Start(); qualityPreviousFrame = 0;
        GD.Print($"QUALITY_BEGIN {qualityMode} duration={qualitySeconds} seed=42 timeScale={Engine.TimeScale}");
    }
    private void MeasureQualityFrame()
    {
        if (qualityMode is not ("perf" or "ordinary")) return;
        var now = qualityClock.Elapsed.TotalSeconds;
        if(qualityMode=="perf"&&now>=10&&!qualityCaptured){qualityCaptured=true;CaptureQualityStress();}
        if (now >= 30 && qualityPreviousFrame >= 30) qualityFrames.Add((now - qualityPreviousFrame) * 1000);
        qualityPreviousFrame = now;
        if (now < qualitySeconds + 30) return;
        var mode=qualityMode;qualityMode = "";
        qualityFrames.Sort();
        double Percentile(double fraction) => qualityFrames[Math.Min(qualityFrames.Count - 1, (int)Math.Ceiling(qualityFrames.Count * fraction) - 1)];
        var p95 = Percentile(.95); var p99 = Percentile(.99);
        WriteQuality(mode=="perf"?"performance.json":"ordinary-performance.json", new { seconds = qualitySeconds, warmupSeconds = 30, samples = qualityFrames.Count, p50Ms = Percentile(.5), p95Ms = p95, p99Ms = p99,
            targetPassed = p95 <= 16.7 && p99 < 25, renderer = RenderingServer.GetVideoAdapterName(), backend = RenderingServer.GetCurrentRenderingMethod(),
            output = DisplayServer.WindowGetSize().ToString(), world = "1280x720 raster / 640x360 world units", vsync = "Disabled",fpsCap=Engine.MaxFps, enemies = Enemies.Count, projectileCapacity = 200, seed = 42,
            overloadActions=qualitySignatures,peakStrainSubunits=qualityPeakStrain,
            stages=QualityStageSummary(),
            peakWorkingSetBytes = Process.GetCurrentProcess().PeakWorkingSet64, note = "Rendered frame intervals measured with Stopwatch. Stress supplies/effects/memory evidence are isolated fixture grants with normal authority caps. Ground effects and echo are combined to stress both implementations. Loot is awarded at checkpoints; there are no live floor pickups. This is not a player encounter." });
        GD.Print($"OVERLOAD_QUALITY_PERF_OK mode={mode} samples={qualityFrames.Count} p95={p95:0.00}ms p99={p99:0.00}ms target={p95 <= 16.7 && p99 < 25}");
        QuitGame();
    }
    private async void CaptureQualityStress()
    {
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        System.IO.Directory.CreateDirectory(QualityDirectory);
        using var capture=GetViewport().GetTexture().GetImage();capture.SavePng(System.IO.Path.Combine(QualityDirectory,"stress.png"));
    }
    private void AdvanceQuality()
    {
        if (qualityMode is not ("soak" or "perf" or "ordinary")) return;
        qualityTicks++;
        if (qualityMode == "soak")
        {
            if (!Playing)
            {
                if (Character!.State.Fracture is { Completed: false }) ContinueFracture();
                else
                {
                    if (!Character.State.ChapterOffers.IsEmpty) UpdateCharacter(s => EndlessRules.ChooseRoute(s, s.ChapterOffers[0]));
                    StartRegional(10, (Region)(qualityCheckpoints / 7 % 4), (ActivityFamily)(qualityCheckpoints / 28 % 3));
                }
                PlayerState.ChangeMaximumLife(BigInteger.Pow(10, 20)); PlayerState.Reset();
            }
            if (qualityTicks % 1800 == 0)
            {
                CompleteFractureGroup(); qualityCheckpoints++;
                if (!string.IsNullOrEmpty(SaveProblem)) { GD.PushError(SaveProblem); QuitGame(1); qualityMode=""; return; }
                var checkpoint = Character!.State;
                ReloadCharacterForCheck();
                if (Character!.State != checkpoint && CharacterStore.Encode(Character.State) != CharacterStore.Encode(checkpoint))
                    throw new InvalidDataException("Soak reload differs from committed checkpoint");
            }
            if (qualityTicks % 3600 == 0)
            {
                qualityMemory.Add(new { simulationSeconds = qualityTicks / 60, wallSeconds = qualityClock.Elapsed.TotalSeconds, managedRetainedBytes = GC.GetTotalMemory(true),
                    workingSetBytes = Process.GetCurrentProcess().WorkingSet64, engineStaticBytes = Godot.Performance.GetMonitor(Godot.Performance.Monitor.MemoryStatic),
                    nodes = Godot.Performance.GetMonitor(Godot.Performance.Monitor.ObjectNodeCount), revision = Character!.State.Revision });
                if (qualityTicks % 36000 == 0) GD.Print($"QUALITY_SOAK_PROGRESS simulatedMinutes={qualityTicks / 3600} checkpoints={qualityCheckpoints}");
            }
            if (qualityTicks >= qualitySeconds * 60)
            {
                qualityMode = "";
                WriteQuality("soak.json", new { simulationSeconds = qualityTicks / 60, wallSeconds = qualityClock.Elapsed.TotalSeconds, accelerated = true, checkpoints = qualityCheckpoints,
                    runRecords = Character!.State.RunRecords.Length, receipts = Character.State.RecentTransactions.Length, peakWorkingSetBytes = Process.GetCurrentProcess().PeakWorkingSet64, samples = qualityMemory,
                    note = "32x simulation clock; checkpoint completion is a fixture command, not proof of combat wins. Every checkpoint is flushed, atomically committed and reloaded. A real-time hour and other hardware remain separate acceptance checks." });
                GD.Print($"OVERLOAD_QUALITY_SOAK_OK simulatedSeconds={qualityTicks / 60} checkpoints={qualityCheckpoints} wallSeconds={qualityClock.Elapsed.TotalSeconds:0.00}"); QuitGame(); return;
            }
        }
        if(qualityMode=="ordinary"&&!Playing){StartEncounter(2);PlayerState.ChangeMaximumLife(BigInteger.Pow(10,20));PlayerState.Reset();}
        if (!Playing) return;
        if(qualityMode=="perf")
        {
            PlayerState.Memories.ObserveLocomotion(new(96,0),LocomotionKind.Walk);
            PlayerState.Memories.ObserveEvadedHit(new(qualityTicks,PlayerState.Tick+2,new(1,0)),SourceKind.BasePlayerAction);
            qualityPeakStrain=Math.Max(qualityPeakStrain,PlayerState.Strain);
            if(PlayerState.Action is { Emitted:true } selected&&selected.RootActionId!=qualityLastRoot)
            {
                qualityLastRoot=selected.RootActionId;if(selected.Implementation!=ActionImplementation.Base)qualitySignatures++;
            }
        }
        if (PlayerState.Life < PlayerState.MaximumLife / 2) PlayerState.Reset();
        // Normal authority, director, queries and effect lifecycle continue to run between checkpoints.
        var aim = (Enemies.FirstOrDefault(e => !e.Enemy!.Dead)?.Position - Player.Position) ?? Vector2.Right;
        if (qualityTicks % 120 == 0) PlayerState.TryStart(SkillId.Traverse, new(0, 1));
        else foreach (var id in Character!.State.EquippedSkills.Append(FrameRules.Basic(Character.State.Frame)))
            if (PlayerState.TryStart(id, new(aim.X, aim.Y))) break;
        motion.Move(new Vector2(0, qualityTicks % 240 < 120 ? 45 : -45));
        if (qualityMode == "perf" && qualityTicks % 3600 == 0) GD.Print($"QUALITY_PERF_PROGRESS seconds={qualityClock.Elapsed.TotalSeconds:0} projectiles={Effects.ProjectileCount}");
    }
    private async void QualityMatrix()
    {
        var checks = new List<object>();
        try
        {
            if (DisplayServer.GetName() == "headless") throw new InvalidOperationException("Matrix requires a renderer");
            UpdateCharacter(s=>EndlessFixtures.Reference(10) with { CharacterId=s.CharacterId });
            foreach (var size in new[] { new Vector2I(960,540), new Vector2I(1280,720), new Vector2I(1920,1080), new Vector2I(2560,1440), new Vector2I(1920,800) })
            {
                DisplayServer.WindowSetSize(size);
                foreach (var textPercent in new[] { 100,125 })
                {
                    if (Audio.TextPercent != textPercent) Audio.ToggleText();
                    foreach(var page in new[]{"settings","bindings","inventory","character","fractures","frames","controls","tutorial"})
                    {
                        Hud.QualityPage(page);
                        for(var tick=0;tick<4;tick++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                        Hud.CheckQualityLayout();
                        System.IO.Directory.CreateDirectory(QualityDirectory);
                        var actual = DisplayServer.WindowGetSize();
                        var name = $"{page}-{size.X}x{size.Y}-{textPercent}.png";
                        using var capture=GetViewport().GetTexture().GetImage();
                        capture.SavePng(System.IO.Path.Combine(QualityDirectory,name));
                        checks.Add(new { requested=size.ToString(),actual=actual.ToString(),textPercent,menu=page,layoutPassed=true,capture=name });
                    }
                }
            }
            using var controllerPress=new InputEventJoypadButton { ButtonIndex=JoyButton.X,Pressed=true };
            Controls.Observe(controllerPress);
            if (!Controls.Controller) throw new InvalidOperationException("Controller glyph routing failed");
            Input.ActionPress("cleave");Input.ActionPress("move_right");
            Controls.Disconnected();
            var disconnectedIntent=Controls.Read(100,Vector2.Zero,Vector2.Right);
            if (Controls.Controller || disconnectedIntent.Action is not null || disconnectedIntent.Move!=Vector2.Zero)
                throw new InvalidOperationException("Disconnect kept held or buffered input");
            var oldFlash = Audio.ReducedFlash; var oldContrast = Audio.HighContrast;
            Audio.ToggleFlash(); Audio.ToggleContrast();
            Hud.QualitySettings(); Hud.CheckQualityLayout();
            Audio.ToggleFlash(); Audio.ToggleContrast();
            if (Audio.ReducedFlash != oldFlash || Audio.HighContrast != oldContrast) throw new InvalidOperationException("Presentation toggle failed");
            WriteQuality("matrix.json",new { displays=checks,syntheticControllerDisconnect=true,physicalControllerTest="pending", hardware="current development machine only",focusLossPause="implemented; human validation pending" });
            GD.Print($"OVERLOAD_QUALITY_MATRIX_OK displays={checks.Count} syntheticControllerDisconnect=true"); QuitGame();
        }
        catch(Exception e) { GD.PushError(e.ToString());QuitGame(1); }
    }
    private async void QualityInstall()
    {
        try
        {
            foreach(var frame in Enum.GetValues<FrameId>())
            {
                OpenCharacter(separate:true,standard:true,frame:frame);
                var fresh=Character!.State;
                if(fresh.Mode!=ProfileMode.Standard||fresh.ValidatedLevel!=1||fresh.Gold!=0||fresh.Seals.Any(s=>s!=0))
                    throw new InvalidDataException("Clean install has sandbox grants");
                StartJourney();
                for(var i=0;i<12;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
                foreach(var e in Enemies)e.Enemy!.ReceiveHit(e.Enemy.MaximumLife,0,PlayerState.Tick);
                for(var i=0;i<105;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
                if(Playing||Character!.State.CheckpointRoom!=1)throw new InvalidDataException("Clean install checkpoint failed");
                ReloadCharacterForCheck();
                if(Character!.State.ValidatedLevel<=1||Character.State.CheckpointRoom!=1||Character.State.Mode!=ProfileMode.Standard)
                    throw new InvalidDataException("Clean install reload failed");
                ReturnToTitle();
            }
            GD.Print("OVERLOAD_QUALITY_INSTALL_OK three Standard Frames; checkpoint and reload");QuitGame();
        }
        catch(Exception e){GD.PushError(e.ToString());QuitGame(1);}
    }
}

public partial class ArenaHud
{
    public void QualitySettings() => Settings();
    public void QualityPage(string page)
    {
        switch(page)
        {
            case "settings":Settings();break;case "bindings":Bindings();break;case "inventory":Inventory();break;
            case "character":CharacterMenu();break;case "fractures":Fractures();break;case "frames":NewFrameMenu();break;
            case "controls":Controls();break;case "tutorial":Tutorial();break;default:throw new ArgumentException("Unknown quality page");
        }
    }
    public void CheckQualityLayout()
    {
        var bounds = panel.GetGlobalRect();
        if (bounds.Position.X < 0 || bounds.Position.Y < 0 || bounds.End.X > Size.X+1 || bounds.End.Y > Size.Y+1)
            throw new InvalidOperationException($"Menu escapes {Size}: {bounds}");
        var focused = GetViewport().GuiGetFocusOwner();
        if(focused is null || !focused.IsVisibleInTree()) throw new InvalidOperationException("Menu focus missing");
    }
}

public partial class CombatEffects
{
    public void FillQualityStress()
    {
        // Fill the real bounded list after natural emissions, before movement/collision processing.
        while (projectiles.Count < 200)
        {
            var i=projectiles.Count;
            Launch(new(80+i%20*24,90+i/20*20),new Vector2(i%2==0?1:-1,.2f).Normalized(),45,2,700,1,false,1,0,-1,long.MaxValue);
        }
        while(groundCasts.Count<4)
            groundCasts.Add(new(new(200+groundCasts.Count*70,200),Vector2.Right,1,SkillId.StormLoom,arena.PlayerState.Tick+24,new(-2,SourceKind.BasePlayerAction)) { Pulses=3,Interval=24 });
        commandedEcho ??= new(new(400,240),Vector2.Right,1,SkillId.EchoOrder,arena.PlayerState.Tick+18,new(-3,SourceKind.BasePlayerAction)) { Pulses=3,Interval=18 };
    }
}
