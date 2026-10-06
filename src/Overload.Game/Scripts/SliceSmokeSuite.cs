using Godot;
using Overload.Domain;
using Overload.Content;

namespace Overload.Game;
public sealed class SliceSmokeSuite(Arena arena, Action<bool, string> check, Action finish)
{
    private int frame;
    private int room;
    private long revision;
    private ActorBody? target;
    private Vector2 bossOrigin;
    public void BeforeTick()
    {
        frame++;
        switch (frame)
        {
            case 1:
                arena.ReturnToTitle(); arena.UpdateCharacter(s => s with { Bindings = [new("pattern.traverse.shelter", 0)] });
                arena.StartOathPractice(); break;
            case 3:
                arena.PlayerState.Memories.ObserveEvadedHit(new(99901, 1000, System.Numerics.Vector2.UnitX), SourceKind.BasePlayerAction);
                check(arena.PlayerState.TryStart(SkillId.Traverse, System.Numerics.Vector2.UnitX), "Shelter accepts Elsewhere placement"); break;
            case 4:
                check(arena.Effects.ShelterCharges == 2 && !arena.PlayerState.Evading, "Shelter leaves its two-charge field without a placement dodge");
                for (var i = 0; i < 2; i++) arena.Effects.Launch(arena.Player.Position, Vector2.Right, 120, 5, 100, 10000, false, 1, 0, 99910+i, 1000);
                break;
            case 5:
                check(arena.Effects.ShelterCharges == 0 && arena.Effects.HitsTaken == 0, "departure field intercepts exactly two ordinary projectiles");
                arena.Effects.Launch(arena.Player.Position, Vector2.Right, 120, 5, 100, 10000, false, 1, 0, 99930, 1000); break;
            case 6: check(arena.Effects.HitsTaken == 1, "third projectile is not intercepted"); break;
            case 12: check(arena.PlayerState.Anchor is not null, "Shelter preserves completed Elsewhere anchor"); arena.ReturnToTitle();
                arena.UpdateCharacter(s => s with { Bindings = [new("pattern.traverse.shelter", 0)] }); arena.StartEncounter(0); break;
            case 14:
                arena.PlayerState.Memories.ObserveEvadedHit(new(99902, 1000, System.Numerics.Vector2.UnitX), SourceKind.BasePlayerAction);
                arena.PlayerState.TryStart(SkillId.Traverse, System.Numerics.Vector2.UnitX); break;
            case 15:
                arena.Effects.Launch(new(140,190), Vector2.Right, 120, 5, 100, 10000, false, 1, 0, 99931, 1000, interceptable: false); break;
            case 16: check(arena.Effects.ShelterCharges == 2, "Shelter never consumes or blocks a boss projectile");
                arena.ReturnToTitle(); arena.UpdateCharacter(s => s with { Bindings = [new("pattern.assault.convergence", 0)] }); arena.StartEncounter(3);
                arena.Player.Position = new(340,180); arena.Enemies[0].Position = new(440,180); bossOrigin = arena.Enemies[0].Position;
                arena.Enemies[0].Enemy!.ReceiveHit(0, 200, 0);
                target = arena.Spawn(EnemyRole.Brute, new(450,230)); target.Enemy!.ReceiveHit(0, 40, 0); break;
            case 18:
                arena.PlayerState.Memories.ObserveLocomotion(new(96, 0), LocomotionKind.Walk);
                arena.PlayerState.Memories.ObserveEvadedHit(new(99903, 1000, System.Numerics.Vector2.UnitX), SourceKind.BasePlayerAction);
                arena.PlayerState.TryStart(SkillId.Cleave, System.Numerics.Vector2.UnitX); break;
            case 34:
                check(target!.Position.DistanceTo(arena.Player.Position) < 100 && target.Enemy!.Life < target.Enemy.MaximumLife, "Convergence pulls an ordinary target into its damaging cone");
                check(arena.Enemies[0].Position.DistanceTo(bossOrigin) < 1, "Convergence does not pull the boss");
                arena.ReturnToTitle(); arena.UpdateCharacter(s => s with { Bindings = arena.BaseBalance.Overload.Bindings });
                arena.StartJourney(); room = 0; break;
        }
        if (frame >= 36 && frame < 36 + 8 * 105)
        {
            var age = (frame - 36) % 105;
            if (age == 0)
            {
                check(arena.JourneyActive && arena.JourneyRoom == room && arena.Enemies.Count > 0, $"authored room {room + 1} starts at its checkpoint");
                foreach (var e in arena.Enemies) e.Enemy!.ReceiveHit(e.Enemy.MaximumLife, 0, arena.PlayerState.Tick);
                revision = arena.Character!.State.Revision;
            }
            if (age == 102)
            {
                var s = arena.Character!.State;
                check(!arena.Playing && arena.Hud.MenuVisible && s.CheckpointRoom == room + 1 && s.Revision == revision + 1,
                    $"room {room + 1} saves one reward and opens continuation");
                var reloaded = CharacterStore.Decode(CharacterStore.Encode(s));
                check(reloaded.TotalXp == s.TotalXp && reloaded.Inventory.Length == s.Inventory.Length && reloaded.CheckpointRoom == room + 1, $"room {room + 1} checkpoint round-trips");
                arena.ContinueJourney(); room++;
            }
        }
        if (frame == 880)
        {
            check(!arena.Playing && !arena.JourneyActive && arena.Character!.State.CheckpointRoom == 8, "boss exit returns to Hearth with completed journey saved");
            check(arena.Character!.State.ClaimedRooms.Count == 8 && arena.Character.State.SkillMilestones.Count == 4, "journey grants all eight receipts and four unique milestones");
            arena.Hud.CaptureMenu("settings");
        }
        if (frame == 882) Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = JoyButton.B, Pressed = true });
        if (frame == 883) Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = JoyButton.B, Pressed = false });
        if (frame == 884) check(arena.Hud.MenuVisible && !arena.Playing && arena.PlayerState.Action is null, "controller Back exits settings without executing an active skill");
        if (frame >= 900 && frame < 1020)
        {
            var index = (frame - 900) / 40; var age = (frame - 900) % 40;
            var skill = new[] { SkillId.Faultline, SkillId.IronSweep, SkillId.GuardBolt }[index];
            if (age == 0)
            {
                arena.ReturnToTitle(); arena.UpdateCharacter(s => s with { Bindings = [], EquippedSkills = [SkillId.ShieldPulse, SkillId.ChainLance, skill] });
                arena.StartEncounter(0);
                foreach (var enemy in arena.Enemies) { enemy.CollisionLayer = 0; enemy.QueueFree(); } arena.Enemies.Clear();
                arena.Player.Position = new(140,190); target = arena.Spawn(EnemyRole.Brute, new(skill == SkillId.IronSweep ? 185 : 240,190)); target.Enemy!.ReceiveHit(0,40,0);
            }
            if (age == 2)
            {
                var cursor = arena.WorldToWindow(new(500,190));
                Input.ParseInputEvent(new InputEventMouseMotion { Position = cursor, GlobalPosition = cursor });
                Input.ParseInputEvent(new InputEventKey { Keycode = Key.R, PhysicalKeycode = Key.R, Pressed = true });
            }
            if (age == 3) Input.ParseInputEvent(new InputEventKey { Keycode = Key.R, PhysicalKeycode = Key.R, Pressed = false });
            if (age == 37) check(target!.Enemy!.Life < target.Enemy.MaximumLife && arena.Effects.CriticalRolls == 1, $"third active slot executes {skill} with one real damage budget");
        }
        if (frame == 1022) { arena.ReturnToTitle(); finish(); }
    }
}
