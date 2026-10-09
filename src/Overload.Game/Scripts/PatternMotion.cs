using Godot;
using Overload.Domain;

namespace Overload.Game;

/// <summary>Authored movement remains swept against terrain on every tick, including changed geometry.</summary>
public sealed class PatternMotion(Arena arena)
{
    public void Reset() => arena.Player.CollisionMask = 5;
    public void Move(Vector2 baseVelocity)
    {
        var action = arena.PlayerState.Action;
        if (action is { Traversal: TraversePhase.AnchorPlace or TraversePhase.AnchorSwap })
        {
            Reset(); arena.Player.Velocity = Vector2.Zero;
            if (action.Traversal == TraversePhase.AnchorSwap && action.Age(arena.PlayerState.Tick) == 0 && action.Destination is { } returnPoint)
            { arena.Player.Position = new(returnPoint.X, returnPoint.Y); arena.Player.SnapMotion(); }
            return;
        }
        if (action?.Destination is not { } target || action.Implementation is not (ActionImplementation.Pursuit or ActionImplementation.Crossing))
        { Reset(); arena.Player.Move(baseVelocity); return; }
        var crossing = action.Implementation == ActionImplementation.Crossing;
        var duration = crossing ? arena.Balance.Hero.EvadeTicks : action.Definition.Windup;
        var age = action.Age(arena.PlayerState.Tick);
        if (age >= duration) { Reset(); arena.Player.Move(baseVelocity); return; }
        if (age == 0) arena.Effects.Record($"#{action.RootActionId} {action.Implementation} committed");
        arena.Player.CollisionMask = crossing ? 1u : 5u;
        var destination = new Vector2(target.X, target.Y);
        if (crossing) destination = WorldQueries.CrossingLanding(arena.Player, arena.Player.Position, destination, arena.Player.Radius);
        var remainingTicks = Math.Max(1, duration - age);
        var step = (destination - arena.Player.Position) / remainingTicks;
        arena.Player.Velocity = step * 60;
        var before = arena.Player.Position;
        arena.Player.MoveAndCollide(step);
        arena.Effects.TraceMovement(before, arena.Player.Position, crossing);
        if (age == duration - 1)
        {
            // Recheck occupied endpoints before restoring body collision. The search never crosses solid terrain.
            if (crossing && !WorldQueries.FreeLanding(arena.Player, arena.Player.Position, arena.Player.Radius))
            {
                var origin = new Vector2(action.Origin.X, action.Origin.Y);
                var safe = WorldQueries.CrossingLanding(arena.Player, arena.Player.Position, origin, arena.Player.Radius);
                arena.Player.MoveAndCollide(safe - arena.Player.Position);
            }
            Reset();
        }
    }
}
