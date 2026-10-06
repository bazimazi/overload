using Godot;
using Overload.Domain;

namespace Overload.Game;

public sealed class InterfaceSmokeSuite(Arena arena, Action<bool, string> check, Action finish)
{
    private int frame;
    public void BeforeTick()
    {
        frame++;
        switch (frame)
        {
            case 1:
                arena.ReturnToTitle(); arena.Hud.Bindings(); break;
            case 3: Joy(JoyButton.A, true); break;
            case 4: Joy(JoyButton.A, false); break;
            case 6: Joy(JoyButton.DpadDown, true); break;
            case 7: Joy(JoyButton.DpadDown, false); break;
            case 9: Joy(JoyButton.DpadDown, true); break;
            case 10: Joy(JoyButton.DpadDown, false); break;
            case 12: Joy(JoyButton.DpadDown, true); break;
            case 13: Joy(JoyButton.DpadDown, false); break;
            case 15: Joy(JoyButton.A, true); break;
            case 16: Joy(JoyButton.A, false); break;
            case 19:
                check(arena.PlayerState.Behavior.Bindings.Any(b => b.Implementation == ActionImplementation.Convergence)
                    && arena.PlayerState.Behavior.Bindings.All(b => b.Implementation != ActionImplementation.Pursuit), "controller changes and applies a binding at Hearth");
                arena.UpdateCharacter(s => s with { Bindings = arena.Balance.Overload.Bindings });
                arena.StartEncounter(0); break;
            case 20:
                var p = arena.PlayerState; p.Memories.ObserveLocomotion(new(96, 0), LocomotionKind.Walk);
                var revision = p.Revision; var memoryRevision = p.Memories.Revision; var focus = p.Focus;
                for (var i = 0; i < 100; i++) arena.PublishPredictions(Vector2.Right, Vector2.Zero);
                check(p.Revision == revision && p.Memories.Revision == memoryRevision && p.Focus == focus, "HUD prediction publication is read-only");
                var plan = arena.Predictions[SkillId.Cleave];
                check(p.TryCommit(plan) && p.Action!.SelectedDefinitionId == plan.Selection.DefinitionId, "HUD prediction agrees with the accepted snapshot");
                check(!p.TryChangeBindings([]), "combat rejects binding changes even when a menu is opened");
                arena.ReturnToTitle(); break;
            case 22: arena.StartOathPractice(); break;
            case 24: SendKey(Key.Space, true); break;
            case 25: SendKey(Key.Space, false); check(!arena.PlayerState.Evading && arena.PlayerState.Action?.Traversal == TraversePhase.AnchorPlace, "Elsewhere places an anchor without hidden rolling or evasion"); break;
            case 35:
                check(arena.PlayerState.Anchor is not null && arena.Player.Position.DistanceTo(new Vector2(140, 190)) < 0.1f, "anchor placement finishes at the departure position");
                arena.Player.Position = new(210, 190); break;
            case 50: SendKey(Key.Space, true); break;
            case 51:
                SendKey(Key.Space, false); check(arena.Player.Position.DistanceTo(new Vector2(140, 190)) < 0.1f && arena.PlayerState.Anchor is null && arena.PlayerState.Evading, "Elsewhere swaps legally and consumes its anchor once");
                arena.Effects.Launch(arena.Player.Position, Vector2.Right, 120, 5, 100, 10000, false, 1, 0, 12345678, arena.PlayerState.Tick + 60); break;
            case 52: check(arena.PlayerState.Memories.Echo is not null, "base anchor swap can earn Echo from actual hostile contact"); break;
            case 60:
                check(WorldQueries.Connected(new(140, 130), new(270, 130), 7), "anchor connectivity allows a route around a pillar");
                arena.ReturnToTitle(); check(!arena.PlayerState.ElsewhereActive && arena.PlayerState.Anchor is null, "leaving oath simulation restores the prior contract"); break;
            case 62: finish(); break;
        }
    }
    private static void Joy(JoyButton button, bool down)
    { Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = button, Pressed = down, Device = 0 }); Input.FlushBufferedEvents(); }
    private static void SendKey(Key key, bool down)
    { Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = down }); Input.FlushBufferedEvents(); }
}
