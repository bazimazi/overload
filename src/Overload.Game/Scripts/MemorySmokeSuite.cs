using Godot;
using Overload.Domain;

namespace Overload.Game;

/// <summary>Opt-in O01 fixtures: real input, movement, terrain and hit queries against a stunned live hostile.</summary>
public sealed class MemorySmokeSuite(Arena arena, Action<bool, string> check, Action finish)
{
    private int frame;
    private long pausedTick;
    private long? echoId;
    private int remaining;
    private bool capturePending;

    public void BeforeTick()
    {
        frame++;
        var state = arena.PlayerState;
        switch (frame)
        {
            case 1: Setup(); arena.Player.Position = new(36, 190); KeyEvent(Key.A, true); break;
            case 181:
                KeyEvent(Key.A, false);
                check(state.Memories.Momentum is null && state.Memories.MomentumDistance < 1, "blocked wall locomotion cannot farm Momentum");
                Setup(); KeyEvent(Key.D, true); break;
            case 231:
                KeyEvent(Key.D, false); check(state.Memories.Momentum is not null, "actual walking generates Momentum");
                arena.TogglePause(); pausedTick = state.Tick; remaining = state.Memories.RemainingTicks(MemoryType.Momentum); break;
            case 241:
                check(state.Tick == pausedTick && state.Memories.RemainingTicks(MemoryType.Momentum) == remaining, "pause freezes memory expiry and generation clocks");
                arena.TogglePause(); break;
            case 541:
                check(state.Memories.Momentum is null, "stored Momentum expires while standing");
                Setup(); AimRight(); KeyEvent(Key.Space, true); break;
            case 542: KeyEvent(Key.Space, false); break;
            case 560:
                check(state.Memories.Momentum is not null, "standard evade displacement generates Momentum");
                check(state.Memories.Echo is null, "empty dodge cannot generate Echo");
                Setup(); AimRight(); KeyEvent(Key.Space, true); break;
            case 561:
                KeyEvent(Key.Space, false);
                for (var i = 0; i < 3; i++) VolleyContact(9000, 1000);
                break;
            case 562:
                check(state.Memories.Echo?.HostileAttackId == 9000 && state.Life == state.MaximumLife, "actual evaded projectile collision generates Echo without damage");
                check(state.Memories.TrackedEchoAttackCount == 1, "volley siblings share one Echo receipt");
                echoId = state.Memories.Echo?.Id; break;
            case 580:
                if (OS.GetCmdlineUserArgs().Contains("--capture-memories")) Capture();
                break;
            case 683: AimRight(); KeyEvent(Key.Space, true); break;
            case 684: KeyEvent(Key.Space, false); VolleyContact(9000, 1000); break;
            case 685:
                check(state.Memories.Echo?.Id == echoId, "same root cannot refresh Echo after generation cooldown");
                state.ReceiveHit(10000000, false);
                check(state.Memories.Momentum is null && state.Memories.Echo is null, "lethal damage clears both memory types immediately"); break;
            case 687:
                Setup(); check(state.Memories.Echo is null && state.Memories.Momentum is null && state.Memories.TrackedEchoAttackCount == 0, "checkpoint retry clears tokens, distance, cooldowns and receipts");
                arena.Player.Position = new(195, 130); AimRight(); KeyEvent(Key.Space, true);
                arena.Effects.Launch(new(245, 130), Vector2.Left, 480, 5, 600, 10000, false, 1, 0, 9010, 500); break;
            case 688: KeyEvent(Key.Space, false); break;
            case 697:
                check(state.Memories.Echo is null && state.Life == state.MaximumLife && arena.Effects.ProjectileCount == 0, "terrain-blocked projectile cannot generate Echo");
                Setup(); KeyEvent(Key.D, true); break;
            case 747:
                KeyEvent(Key.D, false); check(state.Memories.Momentum is not null, "memory reacquisition works after retry");
                VerifyDispatch();
                if (OS.GetCmdlineUserArgs().Contains("--capture-dispatch"))
                {
                    KeyEvent(Key.F3, true); KeyEvent(Key.F3, false);
                    Capture("dispatch.png", "dispatch diagnostics capture saved");
                }
                break;
            case 749:
                arena.ReturnToTitle();
                check(state.Memories.Momentum is null && state.Memories.Echo is null && !state.Memories.InCombat, "leaving encounter clears memories"); break;
        }
        if (frame >= 750 && !capturePending) finish();
    }
    private void VerifyDispatch()
    {
        var state = arena.PlayerState;
        var token = state.Memories.Momentum;
        var revision = state.Revision; var memoryRevision = state.Memories.Revision; var focus = state.Focus;
        var plan = state.Preview(SkillId.ChainLance, System.Numerics.Vector2.UnitX);
        for (var i = 0; i < 100; i++) state.Preview(SkillId.ChainLance, System.Numerics.Vector2.UnitX);
        check(state.Revision == revision && state.Memories.Revision == memoryRevision && state.Focus == focus && state.Memories.Momentum == token,
            "arena prediction leaves tokens, resource and simulation revisions unchanged");
        check(plan.Selection.Implementation == ActionImplementation.Pursuit && plan.Selection.Rejections.Length == 0,
            "real Pursuit adapter predicts an executable pattern");
        check(state.TryCommit(plan) && state.Action?.SelectedDefinitionId == plan.Selection.DefinitionId
            && state.Action.Origin == new System.Numerics.Vector2(arena.Player.Position.X, arena.Player.Position.Y),
            "arena commits the predicted action and actual world origin");
        var action = state.Action;
        check(!state.TryCommit(plan) && ReferenceEquals(state.Action, action) && state.Focus == focus - 24000
            && state.Cooldown(SkillId.ChainLance) == 120 && state.Strain == 25000 && state.Memories.Momentum is null,
            "duplicate arena commitment cannot double spend pattern resources");
    }
    private void Setup()
    {
        arena.StartEncounter(0);
        foreach (var enemy in arena.Enemies) { enemy.CollisionLayer = 0; enemy.CollisionMask = 0; enemy.QueueFree(); }
        arena.Enemies.Clear();
        var definition = arena.Balance.Enemies.Single(e => e.Role == EnemyRole.Brute) with { StunTicks = 3600 };
        var fixture = new ActorBody { ActorId = 1000001, Position = new(565, 275), Enemy = new EnemyCombat(definition) };
        fixture.Enemy.ReceiveHit(0, definition.StaggerThreshold, 0);
        arena.Player.GetParent().AddChild(fixture); arena.Enemies.Add(fixture);
    }
    private void VolleyContact(long id, long expiresAt) => arena.Effects.Launch(arena.Player.Position + new Vector2(4, 0), Vector2.Left, 120, 5, 700, 10000, false, 1, 0, id, expiresAt);
    private static void KeyEvent(Key key, bool pressed)
    {
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = pressed }); Input.FlushBufferedEvents();
    }
    private void AimRight()
    {
        var point = arena.WorldToWindow(arena.Player.Position + Vector2.Right * 100);
        Input.ParseInputEvent(new InputEventMouseMotion { Position = point, GlobalPosition = point }); Input.FlushBufferedEvents();
    }
    private async void Capture(string filename = "memories.png", string description = "memory HUD capture saved")
    {
        if (DisplayServer.GetName() == "headless") return;
        capturePending = true;
        try
        {
            await arena.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            var directory = System.IO.Path.Combine(ProjectSettings.GlobalizePath("res://"), "../../artifacts/screenshots");
            System.IO.Directory.CreateDirectory(directory);
            var error = arena.GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory, filename));
            check(error == Error.Ok, description);
        }
        finally { capturePending = false; }
    }
}
