using System.Collections.Immutable;
using Godot;
using Overload.Domain;

namespace Overload.Game;

/// <summary>Read-only queries use the same actor footprint and collision masks as movement execution.</summary>
public sealed class ArenaActionPreflight(Arena arena) : IActionPreflight
{
    private static readonly ImmutableDictionary<ActionImplementation, PreflightResult> Available =
        ImmutableDictionary<ActionImplementation, PreflightResult>.Empty.Add(ActionImplementation.Base, PreflightResult.Ready);

    public ActionWorldSnapshot Capture(ActionIntent intent, SkillDefinition skill, ActorBehaviorProfile behavior)
    {
        var origin = arena.Player.Position;
        var aim = new Vector2(intent.Aim.X, intent.Aim.Y);
        var results = Available;
        if (skill.Id == SkillId.Traverse && arena.PlayerState.ElsewhereActive && arena.PlayerState.Anchor is { } anchor)
        {
            var point = new Vector2(anchor.Position.X, anchor.Position.Y);
            Vector2? landing = null;
            if (anchor.RoomId == arena.RoomId && origin.DistanceTo(point) <= 256 && WorldQueries.Connected(origin, point, arena.Player.Radius, arena.CollisionWalls,arena.WorldNavigation))
                for (var ring = 0; ring <= 4 && landing is null; ring++)
                    for (var direction = 0; direction < (ring == 0 ? 1 : 8); direction++)
                    {
                        var candidate = point + Vector2.FromAngle(direction * Mathf.Tau / 8) * ring * 4;
                        if (WorldQueries.FreeLanding(arena.Player, candidate, arena.Player.Radius)
                            && WorldQueries.ClipProjectile(arena.Player, point, candidate, arena.Player.Radius, out var blocked) == candidate && !blocked)
                        { landing = candidate; break; }
                    }
            results = results.SetItem(ActionImplementation.Base, landing is { } valid ? new(true, "Ready", new(valid.X, valid.Y))
                : PreflightResult.Blocked("Anchor blocked, disconnected or beyond 8 meters"));
        }
        foreach (var implementation in behavior.Bindings.Select(b => b.Implementation).Distinct())
        {
            if (!PatternExecution.Supports(skill, implementation)) continue;
            if (implementation == ActionImplementation.Shelter) results = results.SetItem(implementation, results[ActionImplementation.Base]);
            else if (implementation is ActionImplementation.Afterstrike or ActionImplementation.Convergence or ActionImplementation.Reprieve or ActionImplementation.Focused or ActionImplementation.Shatter or ActionImplementation.Cascade) results = results.SetItem(implementation, PreflightResult.Ready);
            else if (implementation is ActionImplementation.Pursuit or ActionImplementation.Crossing)
            {
                var destination = implementation == ActionImplementation.Crossing
                    ? WorldQueries.CrossingLanding(arena.Player, origin, origin + aim * PatternExecution.CrossingDistance, arena.Player.Radius)
                    : WorldQueries.ClipProjectile(arena.Player, origin, origin + aim * PatternExecution.PursuitDistance, arena.Player.Radius + 0.1f, out _, 5);
                var result = origin.DistanceTo(destination) < 1 ? PreflightResult.Blocked("No clear advance in that direction")
                    : new PreflightResult(true, "Ready", new(destination.X, destination.Y));
                results = results.SetItem(implementation, result);
            }
        }
        return new(arena.PlayerState.Tick, new(origin.X, origin.Y), results, arena.RoomId);
    }
}
