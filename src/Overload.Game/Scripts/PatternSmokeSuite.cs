using Godot;
using Overload.Domain;
using NVector = System.Numerics.Vector2;

namespace Overload.Game;

/// <summary>Opt-in authored fixtures grant tokens, then use real input, physics, hit queries and delayed execution.</summary>
public sealed class PatternSmokeSuite(Arena arena, Action<bool, string> check, Action finish)
{
    private int frame;
    private ActorBody[] targets = [];
    private long root;
    private long pausedTick;
    private bool capturePending;
    private int nextActorId = 2000000;
    private PlayerCombat State => arena.PlayerState;
    public void BeforeTick()
    {
        frame++;
        switch (frame)
        {
            case 1: Setup(new(140, 190), new(230, 190), new(230, 215)); break;
            case 2: Grant(true); Cast(SkillId.Cleave, true); break;
            case 3: Cast(SkillId.Cleave, false); root = State.Action!.RootActionId;
                check(State.Action.Implementation == ActionImplementation.Pursuit && State.Strain == 25000 && State.Memories.Momentum is null, "Pursuit input consumes Momentum and adds 25 Strain"); break;
            case 10: if (OS.GetCmdlineUserArgs().Contains("--capture-patterns")) Capture("pursuit.png"); break;
            case 15:
                check(Math.Abs(arena.Player.Position.X - 204) < 0.2f, "Pursuit advances two meters during windup");
                check(Life(0) == 945000 && Life(1) == 1000000 && arena.Effects.HitsDealt == 1, "Pursuit Cleave uses one 110 percent narrow lane instead of the base arc");
                CheckProvenance(1); break;
            case 20: Setup(new(140, 190), new(250, 190), new(285, 190), new(320, 190), new(355, 190)); break;
            case 21: Grant(true); Cast(SkillId.ChainLance, true); break;
            case 22: Cast(SkillId.ChainLance, false); root = State.Action!.RootActionId; break;
            case 45:
                check(targets.Take(3).All(t => t.Enemy!.Life == 890000) && Life(3) == 1000000 && arena.Effects.ProjectileCount == 0,
                    "Pursuit Lance replaces its projectile with a lane capped at three victims"); CheckProvenance(3); break;
            case 50: Setup(new(140, 190), new(250, 190), new(250, 220)); break;
            case 51: Grant(true); Cast(SkillId.ShieldPulse, true); break;
            case 52: Cast(SkillId.ShieldPulse, false); break;
            case 74:
                check(Life(0) == 967000 && Life(1) == 1000000 && targets[0].Position == new Vector2(250, 190), "Pursuit Pulse uses its lane budget without a hidden base pulse or push"); break;
            case 80: Setup(new(140, 190), new(190, 190), new(240, 190)); break;
            case 81:
                Grant(true);
                // Keep the token but make its generation lock ready, so this catches accidental self-generation.
                for (var i = 0; i < 120; i++) State.AdvanceClock();
                Cast(SkillId.Traverse, true); break;
            case 82:
                Cast(SkillId.Traverse, false);
                check(State.Action?.Implementation == ActionImplementation.Crossing && State.Evading, "Crossing retains the standard opening evasion window");
                arena.Effects.Launch(arena.Player.Position + new Vector2(4, 0), Vector2.Right, 1200, 5, 700, 10000, false, 1, 0, 909090, 600); break;
            case 83: check(State.Life == State.MaximumLife && State.Memories.Echo is null, "Crossing evades real projectile contact without generating Echo"); break;
            case 89: check(!State.Evading, "Crossing does not extend the seven-tick evasion window"); break;
            case 90: if (OS.GetCmdlineUserArgs().Contains("--capture-patterns")) Capture("crossing.png"); break;
            case 101:
                check(Math.Abs(arena.Player.Position.X - 300) < 0.2f && arena.Player.CollisionMask == 5, "Crossing passes through two enemies for five meters and restores body collision");
                check(State.Memories.Momentum is null && State.Memories.Echo is null && State.Memories.CooldownTicks(MemoryType.Momentum) == 0
                    && State.Strain == 25000, "Crossing cannot refund its memory through its own movement or evasion"); break;
            case 110: Setup(new(140, 130), new Vector2(260, 130)); break;
            case 111: Grant(true); Cast(SkillId.Traverse, true); break;
            case 112: Cast(SkillId.Traverse, false); break;
            case 131:
                check(arena.Player.Position.X is > 190 and < 198.1f && arena.Player.CollisionMask == 5, "Crossing stops its swept footprint before solid terrain"); break;
            case 140: Setup(new(197.8f, 130), new Vector2(270, 130)); break;
            case 141:
                Grant(true, true);
                var fallback = State.Preview(SkillId.Cleave, NVector.UnitX);
                check(fallback.Selection.Implementation == ActionImplementation.Afterstrike && fallback.Selection.Rejections.Length == 1, "blocked Pursuit geometry falls through to Afterstrike");
                Cast(SkillId.Cleave, true); break;
            case 142: Cast(SkillId.Cleave, false); break;
            case 151: check(State.LastSelection?.Implementation == ActionImplementation.Afterstrike && State.Memories.Momentum is not null && State.Memories.Echo is null, "geometry fallback spends only the selected pattern's token"); break;
            case 160: Setup(new(140, 190), new(180, 185), new(180, 200), new(500, 220)); break;
            case 161: Grant(false, true); Cast(SkillId.Cleave, true); break;
            case 162: Cast(SkillId.Cleave, false); root = State.Action!.RootActionId; break;
            case 169:
                check(Life(0) == 970000 && Life(1) == 970000 && arena.Effects.PendingEffectCount == 1, "Afterstrike first hit is 60 percent and schedules one repeat");
                arena.Player.Position = new(140, 260); targets[1].Position = new(500, 200); break;
            case 174: targets[2].Position = new(180, 200); break;
            case 175: arena.TogglePause(); pausedTick = State.Tick; break;
            case 180:
                check(State.Tick == pausedTick && arena.Effects.PendingEffectCount == 1 && State.Strain == 30000, "pause freezes delayed Afterstrike and Strain");
                arena.TogglePause(); break;
            case 194: check(Life(0) == 970000 && arena.Effects.PendingEffectCount == 1, "Afterstrike repeat waits the full 21 simulation ticks"); break;
            case 195:
                check(Life(0) == 940000 && Life(1) == 970000 && Life(2) == 970000, "Afterstrike repeats at fixed origin and aim after movement; targets can leave or enter");
                check(arena.Effects.CriticalRolls == 1 && arena.Effects.HitsDealt == 4 && arena.Effects.PendingEffectCount == 0, "Afterstrike rolls critical once and hits each victim at most once per part");
                check(State.Action is null && State.Strain == 30000 && State.Memories.Echo is null && State.Memories.Momentum is null, "delayed repeat survives recovery without payment or memory generation");
                CheckProvenance(4, true); break;
            case 200: Setup(new(140, 190), new(195, 190), new(230, 190), new(265, 190), new(300, 190)); break;
            case 201: Grant(false, true); Cast(SkillId.ChainLance, true); break;
            case 202: Cast(SkillId.ChainLance, false); root = State.Action!.RootActionId; break;
            case 225: arena.Player.Position = new(140, 260); break;
            case 260:
                check(targets.Take(3).All(t => t.Enemy!.Life == 880000) && Life(3) == 1000000, "Afterstrike Lance repeats the original projectile with independent three-victim receipts");
                check(arena.Effects.CriticalRolls == 1 && State.Focus is > 76000 and < 85000 && State.Strain == 30000, "Afterstrike projectiles reuse one critical roll and one Focus payment");
                CheckProvenance(6, true); break;
            case 270: Setup(new(140, 190), new Vector2(180, 190)); break;
            case 271: Grant(false, true); Cast(SkillId.Cleave, true); break;
            case 272: Cast(SkillId.Cleave, false); break;
            case 280:
                check(arena.Effects.PendingEffectCount == 1, "death fixture has a pending repeat"); State.ReceiveHit(10000000, false); break;
            case 281: check(arena.Effects.PendingEffectCount == 0 && arena.Effects.ProjectileCount == 0, "death cancels delayed player effects"); break;
            case 290: Setup(new(140, 190), new Vector2(180, 190)); break;
            case 291: Grant(false, true); Cast(SkillId.Cleave, true); break;
            case 292: Cast(SkillId.Cleave, false); break;
            case 300:
                check(arena.Effects.PendingEffectCount == 1, "unload fixture has a pending repeat"); arena.ReturnToTitle();
                check(arena.Effects.PendingEffectCount == 0 && arena.Effects.ProjectileCount == 0, "encounter unload cancels delayed player effects"); break;
            case 310: Setup(new(140, 190), new Vector2(500, 190)); break;
            case 311:
                Grant(true); var plan = State.Preview(SkillId.ShieldPulse, NVector.UnitX);
                arena.Player.Position = new(197.8f, 130);
                check(!State.TryCommit(plan) && State.Focus == State.MaximumFocus && State.Strain == 0 && State.Memories.Momentum is not null,
                    "real world revalidation rejects newly blocked advance without payment");
                check(State.TryStart(SkillId.ShieldPulse, NVector.UnitX) && State.Action?.Implementation == ActionImplementation.Base
                    && State.Strain == 0 && State.Memories.Momentum is not null, "fully blocked advance falls back to base without deleting Momentum"); break;
            case 320: Setup(new(140, 130), new Vector2(270, 130)); break;
            case 321: Grant(false, true); Cast(SkillId.ChainLance, true); break;
            case 322: Cast(SkillId.ChainLance, false); break;
            case 368:
                check(Life(0) == 1000000 && arena.Effects.PendingEffectCount == 0 && arena.Effects.ProjectileCount == 0, "terrain blocks both Afterstrike projectiles"); break;
            case 370: Setup(new(140, 190), new Vector2(300, 190)); break;
            case 371: Grant(true); Cast(SkillId.Traverse, true); break;
            case 372: Cast(SkillId.Traverse, false); break;
            case 391:
                check(arena.Player.Position.X is > 270 and < 284 && WorldQueries.FreeLanding(arena.Player, arena.Player.Position, arena.Player.Radius), "Crossing shortens its path to avoid an occupied endpoint"); break;
            case 400: Setup(new(140, 190), new Vector2(500, 190)); break;
            case 401: Grant(true); Cast(SkillId.Traverse, true); break;
            case 402: Cast(SkillId.Traverse, false); break;
            case 406: targets[0].Position = new(300, 190); break;
            case 421:
                check(arena.Player.Position.X is > 270 and < 284 && arena.Player.CollisionMask == 5
                    && WorldQueries.FreeLanding(arena.Player, arena.Player.Position, arena.Player.Radius), "Crossing rechecks a newly occupied landing during movement"); break;
            case 430: Setup(new(250, 190), new(290, 180), new(290, 210)); break;
            case 431: Grant(false, true); Cast(SkillId.ShieldPulse, true); break;
            case 432: Cast(SkillId.ShieldPulse, false); root = State.Action!.RootActionId; break;
            case 445:
                if (OS.GetCmdlineUserArgs().Contains("--capture-patterns")) Capture(); break;
            case 471:
                check(Life(0) == 964000 && Life(1) == 964000 && arena.Effects.HitsDealt == 4, "Afterstrike Pulse applies two 60 percent parts with independent victim receipts");
                check(arena.Effects.CriticalRolls == 1 && State.Focus is > 75000 and < 85000 && State.Strain == 30000, "Afterstrike Pulse has one payment and one critical roll");
                CheckProvenance(4, true); break;
            case 475: Setup(new(140, 190), new Vector2(310, 190)); break;
            case 476: Grant(false, true); Cast(SkillId.ChainLance, true); break;
            case 477: Cast(SkillId.ChainLance, false); break;
            case 493:
                check(arena.Effects.ProjectileCount == 1 && arena.Effects.PendingEffectCount == 1, "death fixture contains an in-flight Afterstrike projectile");
                State.ReceiveHit(10000000, false); break;
            case 500:
                check(Life(0) == 1000000 && arena.Effects.ProjectileCount == 0 && arena.Effects.PendingEffectCount == 0, "death cancels in-flight and delayed Afterstrike projectiles");
                arena.ReturnToTitle(); break;
            case 505: Setup(new(140, 190), new Vector2(162, 190)); break;
            case 506: Grant(false, true); Cast(SkillId.ChainLance, true); break;
            case 507: Cast(SkillId.ChainLance, false); break;
            case 521:
                // This enemy hit is queued before the player's projectile emits on the same simulation tick.
                arena.Effects.Launch(arena.Player.Position + new Vector2(4, 0), Vector2.Left, 480, 5, 100, 10000000, false, 1, 0, 888888, State.Tick + 100); break;
            case 524:
                check(State.Dead && Life(0) == 1000000 && arena.Effects.PendingEffectCount == 0 && arena.Effects.ProjectileCount == 0,
                    "lethal projectile cancels later player effects within the same tick");
                arena.ReturnToTitle(); break;
        }
        if (frame >= 526 && !capturePending) finish();
    }
    private System.Numerics.BigInteger Life(int index) => targets[index].Enemy!.Life;
    private void Setup(Vector2 position, params Vector2[] positions)
    {
        arena.StartEncounter(0);
        foreach (var enemy in arena.Enemies) { enemy.CollisionLayer = 0; enemy.CollisionMask = 0; enemy.QueueFree(); }
        arena.Enemies.Clear(); arena.Player.Position = position;
        targets = positions.Select(point =>
        {
            var definition = arena.Balance.Enemies.Single(e => e.Role == EnemyRole.Brute) with { Life = 1000, StunTicks = 3600 };
            var actor = new ActorBody { ActorId = nextActorId++, Position = point, Enemy = new(definition) };
            actor.Enemy.ReceiveHit(0, definition.StaggerThreshold, 0);
            arena.Player.GetParent().AddChild(actor); arena.Enemies.Add(actor); return actor;
        }).ToArray();
        State.SetCombatActive(true);
    }
    private void Grant(bool momentum, bool echo = false)
    {
        if (momentum) State.Memories.ObserveLocomotion(new(96, 0), LocomotionKind.Walk);
        if (echo) State.Memories.ObserveEvadedHit(new(77777 + frame, State.Tick + 600, NVector.UnitY), SourceKind.BasePlayerAction);
    }
    private void Cast(SkillId id, bool down)
    {
        var point = arena.WorldToWindow(arena.Player.Position + Vector2.Right * 100);
        Input.ParseInputEvent(new InputEventMouseMotion { Position = point, GlobalPosition = point });
        if (id == SkillId.Cleave) Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = down, Position = point, GlobalPosition = point });
        else
        {
            var key = id == SkillId.ShieldPulse ? Key.Q : id == SkillId.ChainLance ? Key.E : Key.Space;
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = down });
        }
        Input.FlushBufferedEvents();
    }
    private void CheckProvenance(int hits, bool repeat = false)
    {
        var events = arena.Effects.RecentEvents.Where(e => e.Kind == "hit").ToArray();
        check(events.Length == hits && events.All(e => e.RootActionId == root && e.Source == SourceKind.OverloadedPlayerAction)
            && (!repeat || events.Any(e => e.EffectId == 1 && e.ParentEffectId == 0)), "pattern hits preserve root and child provenance");
    }
    private async void Capture(string filename = "patterns.png")
    {
        if (DisplayServer.GetName() == "headless") return;
        capturePending = true;
        try
        {
            await arena.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            var directory = System.IO.Path.Combine(ProjectSettings.GlobalizePath("res://"), "../../artifacts/screenshots");
            System.IO.Directory.CreateDirectory(directory);
            check(arena.GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory, filename)) == Error.Ok, $"pattern presentation capture saved: {filename}");
        }
        finally { capturePending = false; }
    }
}
