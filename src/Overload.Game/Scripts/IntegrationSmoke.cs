using Godot;
using Overload.Domain;
using System.Text.Json;
using FileAccess = Godot.FileAccess;

namespace Overload.Game;

/// <summary>Opt-in fixture; never writes ordinary controls, saves, or progression.</summary>
public sealed class IntegrationSmoke(Arena arena)
{
    private readonly ulong startupTime = Time.GetTicksMsec();
    private int frame;
    private Vector2 origin;
    private long pausedTick;
    private bool failed;
    private Vector2 cursor;
    private ActorBody[] targets = [];
    private MemorySmokeSuite? memories;
    private PatternSmokeSuite? patterns;
    private InterfaceSmokeSuite? interfaces;
    private SliceSmokeSuite? slice;
    private readonly List<string> checks = [];
    public void BeforeTick()
    {
        // Let the native window finish its startup focus/resize events before injecting held keys.
        if (DisplayServer.GetName() != "headless" && Time.GetTicksMsec() - startupTime < 1000) return;
        if (failed) return;
        if (slice is not null) { slice.BeforeTick(); return; }
        if (interfaces is not null) { interfaces.BeforeTick(); return; }
        if (patterns is not null) { patterns.BeforeTick(); return; }
        if (memories is not null) { memories.BeforeTick(); return; }
        frame++;
        switch (frame)
        {
            case 2: Check(arena.Hud.MenuVisible, "title opens"); arena.StartEncounter(0); origin = arena.Player.Position; SendKey(Key.D, true); break;
            case 32: SendKey(Key.D, false); Check(arena.Player.Position.X - origin.X > 60, "keyboard moves at authored speed"); arena.Player.Position = new(50, 190); SendKey(Key.A, true); break;
            case 62: SendKey(Key.A, false); Check(arena.Player.Position.X >= 34, "wall blocks locomotion"); arena.StartEncounter(0); origin = arena.Player.Position; SendKey(Key.D, true); SendKey(Key.S, true); break;
            case 92: SendKey(Key.D, false); SendKey(Key.S, false); Check(Math.Abs(arena.Player.Position.DistanceTo(origin) - 64) < 3, "diagonal movement normalized"); arena.TogglePause(); pausedTick = arena.PlayerState.Tick; break;
            case 102: Check(arena.PlayerState.Tick == pausedTick, "pause freezes simulation"); arena.TogglePause(); arena.StartEncounter(0); arena.Player.Position = new(310, 130); Aim(new(400, 130)); Mouse(true); break;
            case 122: Mouse(false); Check(arena.Effects.HitsDealt > 0, "mouse attack damages through collision query"); arena.StartEncounter(0); Aim(new(500, 190)); SendKey(Key.E, true); break;
            case 123: SendKey(Key.E, false); break;
            case 142: Check(arena.PlayerState.Focus < arena.PlayerState.MaximumFocus && arena.PlayerState.Cooldown(SkillId.ChainLance) > 0, "lance commits cost and cooldown"); arena.StartEncounter(0); SendKey(Key.Q, true); break;
            case 143: SendKey(Key.Q, false); break;
            case 160: Check(arena.PlayerState.Focus < arena.PlayerState.MaximumFocus && arena.PlayerState.Cooldown(SkillId.ShieldPulse) > 0, "pulse input commits"); arena.StartEncounter(0); arena.PlayerState.ReceiveHit(100000, false); SendKey(Key.F, true); break;
            case 161: SendKey(Key.F, false); break;
            case 222: Check(arena.PlayerState.FlaskCharges == 2 && arena.PlayerState.Life == 195000, "flask heals exact 35 percent over one second"); arena.StartEncounter(0); Joy(JoyButton.A, true); break;
            case 223: Joy(JoyButton.A, false); Check(arena.PlayerState.Action?.Definition.Id == SkillId.Traverse, "controller evade routes to action"); break;
            case 244: arena.PlayerState.ReceiveHit(10000000, false); break;
            case 246: Check(arena.PlayerState.Dead && arena.Hud.MenuVisible, "death opens retry"); arena.StartEncounter(arena.Wave); Check(arena.PlayerState.Life == arena.PlayerState.MaximumLife && arena.PlayerState.FlaskCharges == 3, "retry restores resources"); arena.StartEncounter(3); arena.Player.Position = new(360, 180); break;
            case 370: Check(arena.Enemies[0].AttackAge >= 0 || arena.Effects.HitsTaken > 0, "Bellkeeper schedules telegraphed attack"); arena.ReturnToTitle(); break;
            case 372: Check(arena.Hud.MenuVisible && !arena.Playing, "return to title"); Joy(JoyButton.A, true); break;
            case 374: Joy(JoyButton.A, false); break;
            case 379: Check(arena.Playing, "controller confirms title selection"); arena.ReturnToTitle(); break;
            case 380: Check(arena.Hud.FocusPracticeForCheck(),"optional practice has a labeled native focus target"); break;
            case 381: Joy(JoyButton.DpadDown, false); break;
            case 382: break;
            case 383: Joy(JoyButton.DpadDown, false); break;
            case 384: break;
            case 385: Joy(JoyButton.DpadDown, false); break;
            case 386: break;
            case 387: break;
            case 388: Joy(JoyButton.DpadRight, false); Joy(JoyButton.A, true); break;
            case 389: Joy(JoyButton.A, false); break;
            case 390:
                Check(arena.Playing && arena.RewriteLessonActive, "controller confirms the labeled optional combat practice");
                arena.ReturnToTitle();
                arena.RestoreCombatSmokeFixture();
                arena.StartEncounter(0); ClearEnemies();
                targets = [arena.Spawn(EnemyRole.Caster, new(195, 190)), arena.Spawn(EnemyRole.Caster, new(230, 190)), arena.Spawn(EnemyRole.Caster, new(265, 190)), arena.Spawn(EnemyRole.Caster, new(300, 190))];
                foreach (var target in targets) target.Enemy!.ReceiveHit(0, 40, 0);
                Aim(new(500, 190)); SendKey(Key.E, true); break;
            case 391: SendKey(Key.E, false); break;
            case 435:
                Check(targets.Take(3).All(t => t.Enemy!.Life < t.Enemy.MaximumLife) && targets[3].Enemy!.Life == targets[3].Enemy!.MaximumLife && arena.Effects.HitsDealt == 3, "swept lance pierces exactly three distinct victims");
                arena.StartEncounter(0); ClearEnemies(); arena.Player.Position = new(140, 130);
                targets = [arena.Spawn(EnemyRole.Caster, new(270, 130))]; targets[0].Enemy!.ReceiveHit(0, 40, 0);
                Aim(new(500, 130)); SendKey(Key.E, true); break;
            case 436: SendKey(Key.E, false); break;
            case 470:
                Check(arena.Effects.HitsDealt == 0 && arena.Effects.ProjectileCount == 0, "terrain blocks lance before target");
                arena.StartEncounter(3); arena.Enemies[0].Enemy!.ReceiveHit(3100000, 0, 0);
                Check(arena.Enemies[0].Enemy!.Enraged, "boss enters half-life phase");
                arena.Enemies[0].Enemy!.ReceiveHit(10000000, 0, 0); break;
            case 575: Check(!arena.Playing && arena.Hud.MenuVisible, "boss death reaches victory menu"); memories = new MemorySmokeSuite(arena, Check, () => patterns = new PatternSmokeSuite(arena, Check, () => interfaces = new InterfaceSmokeSuite(arena, Check, () => slice = new SliceSmokeSuite(arena, Check, Finish)))); break;
        }
    }
    public void AfterTick() { }
    private void ClearEnemies()
    {
        foreach (var enemy in arena.Enemies) { enemy.CollisionLayer = 0; enemy.CollisionMask = 0; enemy.QueueFree(); }
        arena.Enemies.Clear();
    }
    private void Check(bool condition, string name)
    {
        if (!condition) { failed = true; GD.PushError($"SMOKE FAILED: {name}; position={arena.Player.Position}, origin={origin}, tick={arena.PlayerState.Tick}, move={Input.GetVector("move_left", "move_right", "move_up", "move_down")}, viewport={arena.Player.GetViewportRect()}"); arena.GetTree().Quit(1); return; }
        checks.Add(name); GD.Print($"SMOKE PASS: {name}");
    }
    private async void Finish()
    {
        if (failed) return;
        // Isolated legendary fixtures validate secondary effects without altering the campaign playthrough.
        arena.ReturnToTitle();
        arena.UpdateCharacter(s=>FrameRules.Create(FrameId.Warden) with {CharacterId=s.CharacterId,Inventory=s.Inventory});
        arena.RestoreCombatSmokeFixture();
        arena.UpdateCharacter(s=>s with {Inventory=s.Inventory.SetItem(0,s.Inventory[0] with {Band=5,Power="nova"})});
        arena.StartEncounter(0);ClearEnemies();arena.Player.Position=new(230,190);
        targets=[arena.Spawn(EnemyRole.Caster,new(275,190)),arena.Spawn(EnemyRole.Caster,new(330,190)),arena.Spawn(EnemyRole.Caster,new(365,190))];
        foreach(var target in targets)target.Enemy!.ReceiveHit(target.Enemy.Life-1,40,arena.PlayerState.Tick);
        await arena.ToSignal(arena.GetTree(),SceneTree.SignalName.PhysicsFrame);
        var circle=WorldQueries.SectorHits(arena.Player,new(330,190),Vector2.Right,64,360,4);
        Check(circle.Contains(targets[0])&&circle.Contains(targets[2]),"circular queries include targets behind the aim and across the seam");
        Check(arena.PlayerState.TryStart(SkillId.Cleave,System.Numerics.Vector2.UnitX),"legendary fixture starts an ordinary basic attack");
        for(var i=0;i<18;i++)await arena.ToSignal(arena.GetTree(),SceneTree.SignalName.PhysicsFrame);
        Check(targets[0].Enemy!.Dead&&targets[1].Enemy!.Dead&&!targets[2].Enemy!.Dead&&arena.Effects.Deathbursts==1,"one deathburst kills its neighbor without recursively exploding");
        Check(arena.Effects.RecentEvents.Any(e=>e.EffectId==1001&&e.Source==SourceKind.SecondaryEffect&&e.Kind=="hit"),"deathburst damage retains secondary provenance");
        arena.ReturnToTitle();arena.UpdateCharacter(s=>FrameRules.Create(FrameId.Threadseer) with {CharacterId=s.CharacterId});
        arena.UpdateCharacter(s=>s with {Inventory=s.Inventory.SetItem(0,s.Inventory[0] with {Band=5,Power="split"})});
        arena.StartEncounter(0);ClearEnemies();arena.Player.Position=new(230,190);
        targets=[arena.Spawn(EnemyRole.Caster,new(280,190)),arena.Spawn(EnemyRole.Caster,new(325,190)),arena.Spawn(EnemyRole.Caster,new(365,190)),arena.Spawn(EnemyRole.Caster,new(405,190))];
        foreach(var target in targets)target.Enemy!.ReceiveHit(0,40,arena.PlayerState.Tick);
        await arena.ToSignal(arena.GetTree(),SceneTree.SignalName.PhysicsFrame);
        Check(arena.PlayerState.TryStart(SkillId.ShardShot,System.Numerics.Vector2.UnitX),"split fixture starts an ordinary projectile attack");
        for(var i=0;i<35;i++)await arena.ToSignal(arena.GetTree(),SceneTree.SignalName.PhysicsFrame);
        Check(arena.Effects.SplitVolleys==1&&arena.Effects.HitsDealt==3&&targets.Take(3).All(t=>t.Enemy!.Life<t.Enemy.MaximumLife)&&targets[3].Enemy!.Life==targets[3].Enemy!.MaximumLife,"three fan projectiles hit three enemies once each and stop at their shared budget");
        if(failed)return;
        var folder = "user://tests";
        DirAccess.MakeDirRecursiveAbsolute(folder);
        using var result = FileAccess.Open(folder + "/smoke.json", FileAccess.ModeFlags.Write);
        result.StoreString(JsonSerializer.Serialize(new { success = true, checks, engine = Engine.GetVersionInfo()["string"].AsString() }));
        GD.Print("OVERLOAD_SMOKE_OK " + checks.Count); arena.QuitGame();
    }
    private static void SendKey(Key key, bool down) { Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = down }); Input.FlushBufferedEvents(); }
    private void Mouse(bool down) { Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = down, Position = cursor, GlobalPosition = cursor }); Input.FlushBufferedEvents(); }
    private static void Joy(JoyButton button, bool down) { Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = button, Pressed = down, Device = 0 }); Input.FlushBufferedEvents(); }
    private void Aim(Vector2 point)
    {
        cursor = arena.WorldToWindow(point);
        Input.ParseInputEvent(new InputEventMouseMotion { Position = cursor, GlobalPosition = cursor }); Input.FlushBufferedEvents();
    }
}
