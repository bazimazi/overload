using Godot;
using Overload.Domain;

namespace Overload.Game;

public partial class Arena
{
    private Vector2 groundVelocity;
    private int movementTeleport = -1;
    private bool authoredTravel;
    public float CurrentWalkSpeed=>Balance.Hero.Speed*(AdventureActive&&WorldZone.Id!="hearth"&&!HasEngagedEnemies?1.4f:1);

    private Vector2 ResolvePlayerMovement(Vector2 intent, double delta)
    {
        if (movementTeleport != Player.TeleportVersion)
        { groundVelocity = Vector2.Zero; authoredTravel = false; movementTeleport = Player.TeleportVersion; }
        var action = PlayerState.Action;
        if (action?.Definition.Id == SkillId.Traverse)
        {
            authoredTravel = true;
            groundVelocity = Vector2.Zero;
            return new Vector2(action.Aim.X, action.Aim.Y) * (Balance.Hero.EvadeDistance * 60 / Balance.Hero.EvadeTicks)
                * LocomotionRules.EvadeScale(action.Age(PlayerState.Tick), Balance.Hero.EvadeTicks);
        }
        if (action?.Implementation == ActionImplementation.Pursuit && action.Age(PlayerState.Tick) < action.Definition.Windup)
        { authoredTravel = true; groundVelocity = Vector2.Zero; return Vector2.Zero; }
        if (authoredTravel)
        { groundVelocity = intent.LengthSquared() > .01f ? Player.Velocity.LimitLength(CurrentWalkSpeed) : Vector2.Zero; authoredTravel = false; }
        var basic=FrameRules.Basic(Character?.State.Frame??FrameId.Warden);
        var scale = action is not null && action.Phase(PlayerState.Tick) != ActionPhase.Recovery ? action.Definition.Id==basic?.8f:.35f : 1;
        var next = LocomotionRules.Advance(new(groundVelocity.X, groundVelocity.Y), new(intent.X * scale, intent.Y * scale), CurrentWalkSpeed, (float)delta);
        return groundVelocity = new(next.X, next.Y);
    }

    private void FinishPlayerMovement()
    {
        if (!authoredTravel) groundVelocity = Player.Velocity;
    }

    private void FacePlayer(Vector2 move, Vector2 aim)
        => Player.Facing = PlayerState.Action is null && !Controls.AimingWithStick && move.LengthSquared() > .01f ? move.Normalized() : aim;
}
