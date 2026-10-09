using System.Numerics;
using Overload.Domain;
using Xunit;

namespace Overload.Domain.Tests;

public sealed class LocomotionTests
{
    [Theory]
    [InlineData(96)] [InlineData(128)] [InlineData(160)]
    public void SteeringStartsImmediatelyReachesSpeedWithin50msAndStopsWithinTwoTicks(float speed)
    {
        var velocity = LocomotionRules.Advance(Vector2.Zero, Vector2.UnitX, speed, 1f / 60);
        Assert.InRange(velocity.X, speed * .4f, speed * .65f);
        for (var i = 0; i < 2; i++) velocity = LocomotionRules.Advance(velocity, Vector2.UnitX, speed, 1f / 60);
        Assert.Equal(speed, velocity.X);
        var drift = 0f;
        for (var i = 0; i < 2; i++) { velocity = LocomotionRules.Advance(velocity, Vector2.Zero, speed, 1f / 60); drift += velocity.Length() / 60; }
        Assert.Equal(Vector2.Zero, velocity);
        Assert.InRange(drift, 0, speed / 128);
    }

    [Fact]
    public void ReversalsTurnWithinThreeTicksAndDiagonalInputCannotIncreaseTopSpeed()
    {
        var velocity = Vector2.UnitX * 128;
        for (var i = 0; i < 3; i++) velocity = LocomotionRules.Advance(velocity, -Vector2.UnitX, 128, 1f / 60);
        Assert.Equal(-Vector2.UnitX * 128, velocity);
        for (var i = 0; i < 12; i++) velocity = LocomotionRules.Advance(velocity, Vector2.One, 128, 1f / 60);
        Assert.InRange(velocity.Length(), 127.999f, 128.001f);
        Assert.Equal(velocity.X, velocity.Y);
    }

    [Theory]
    [InlineData(12)] [InlineData(18)] [InlineData(24)]
    public void EasedEvadesRetainAuthoredDistanceAndDecreaseSpeedThroughoutTravel(int ticks)
    {
        var scales = Enumerable.Range(0, ticks).Select(i => LocomotionRules.EvadeScale(i, ticks)).ToArray();
        Assert.InRange(scales.Sum() * 96 / ticks, 95.999f, 96.001f);
        Assert.True(scales.Zip(scales.Skip(1)).All(pair => pair.First > pair.Second));
        Assert.InRange(scales[^1], .72f, .74f);
    }

    [Fact]
    public void ArrivalBrakesAtTheDestinationWithoutOvershootingOrOrbiting()
    {
        var position = Vector2.Zero; var goal = new Vector2(83, 37); var velocity = Vector2.Zero;
        var arrived = false;
        for (var i = 0; i < 180; i++)
        {
            var distance = Vector2.Distance(position, goal);
            if (distance < .7f) arrived = true;
            var desired = arrived ? Vector2.Zero : Vector2.Normalize(goal - position) * (LocomotionRules.ArrivalSpeed(distance, 128, 1f / 60) / 128);
            velocity = LocomotionRules.Advance(velocity, desired, 128, 1f / 60);
            position += velocity / 60;
        }
        Assert.True(arrived); Assert.Equal(Vector2.Zero, velocity); Assert.InRange(Vector2.Distance(position, goal), 0, 1);
    }

    [Fact]
    public void FacingDoesNotFlickerAtDiagonalBoundariesButStillFollowsDeliberateTurns()
    {
        var index = 0;
        foreach (var angle in new[] { .38f, .41f, .37f, .42f, .4f }) index = LocomotionRules.Facing(angle, index, 8);
        Assert.Equal(0, index);
        index = LocomotionRules.Facing(.6f, index, 8); Assert.Equal(1, index);
        foreach (var angle in new[] { .4f, .37f, .41f }) index = LocomotionRules.Facing(angle, index, 8);
        Assert.Equal(1, index); Assert.Equal(4, LocomotionRules.Facing(-MathF.PI, index, 8));
    }

    [Theory]
    [InlineData(6, 4, 12)] [InlineData(18, 8, 24)]
    public void AttackPosesRemainContinuousAcrossWindupStrikeAndRecovery(int windup, int active, int recovery)
    {
        foreach (var boundary in new[] { windup, windup + active, windup + active + recovery })
            Assert.InRange(Math.Abs(LocomotionRules.AttackExtension(boundary - .001f, windup, active, recovery)
                - LocomotionRules.AttackExtension(boundary + .001f, windup, active, recovery)), 0, .001f);
        Assert.Equal(0, LocomotionRules.AttackExtension(0, windup, active, recovery));
        Assert.Equal(0, LocomotionRules.AttackExtension(windup + active + recovery, windup, active, recovery));
    }
}
