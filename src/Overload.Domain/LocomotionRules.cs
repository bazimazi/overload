using System.Numerics;

namespace Overload.Domain;

/// <summary>Short, deterministic ramps keep steering responsive at any simulation step.</summary>
public static class LocomotionRules
{
    public const float Acceleration = 36;
    public const float Braking = 38;
    public const float Turning = 42;

    public static Vector2 Advance(Vector2 velocity, Vector2 intent, float speed, float seconds)
    {
        if (speed <= 0) return Vector2.Zero;
        if (seconds <= 0) return velocity;
        if (intent.LengthSquared() > 1) intent = Vector2.Normalize(intent);
        var target = intent * speed;
        var rate = target.LengthSquared() < velocity.LengthSquared() * .25f ? Braking
            : Vector2.Dot(velocity, target) < velocity.LengthSquared() * .5f ? Turning : Acceleration;
        var delta = target - velocity;
        var length = delta.Length();
        return length <= speed * rate * seconds ? target : velocity + delta / length * (speed * rate * seconds);
    }

    public static float ArrivalSpeed(float distance, float speed, float seconds)
        => distance <= .7f ? 0 : Math.Min(speed, Math.Min(distance / seconds, MathF.Sqrt(2 * speed * Braking * distance)));

    // Midpoint sampling has a mean of exactly one: an unobstructed evade still covers its authored distance.
    public static float EvadeScale(int age, int ticks)
        => 1 + .28f * MathF.Cos(MathF.PI * (Math.Clamp(age, 0, ticks - 1) + .5f) / ticks);

    public static int Facing(float angle, int previous, int directions)
    {
        var step = MathF.Tau / directions;
        var difference = MathF.IEEERemainder(angle - previous * step, MathF.Tau);
        if (MathF.Abs(difference) <= step * .5f + .09f) return previous;
        return ((int)MathF.Round(angle / step) % directions + directions) % directions;
    }

    public static float AttackExtension(float age, int windup, int active, int recovery, float reach = 3)
    {
        static float Ease(float t) { t = Math.Clamp(t, 0, 1); return t * t * (3 - 2 * t); }
        if (age < windup) return -2 * Ease(age / Math.Max(1, windup));
        if (age < windup + active) return -2 + (reach + 2) * Ease((age - windup) / Math.Max(1, active));
        return reach * (1 - Ease((age - windup - active) / Math.Max(1, Math.Min(12, recovery))));
    }
}
