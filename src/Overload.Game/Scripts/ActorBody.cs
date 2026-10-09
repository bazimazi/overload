using Godot;
using Overload.Domain;

namespace Overload.Game;

/// <summary>The collision body is authoritative. Interpolation and animation only move its drawing.</summary>
public partial class ActorBody : CharacterBody2D
{
    public Vector2? ReturnMark { get; set; }
    public int ActorId { get; init; }
    public EnemyCombat? Enemy { get; init; }
    public bool IsPlayer => Enemy is null;
    public Vector2 Facing { get; set; } = Vector2.Right;
    public bool Flash { get; set; }
    public bool Evasion { get; set; }
    public bool DebugFootprint { get; set; }
    public int AttackAge { get; set; } = -1;
    public int TellTicks { get; set; }
    public bool Volley { get; set; }
    public bool Recovering { get; set; }
    public bool Stunned { get; set; }
    public Vector2 AttackAim { get; set; }
    public float Radius => IsPlayer ? 7 : Enemy!.Definition.Role == EnemyRole.Bellkeeper ? 17 : 9;
    public bool AnchorHalo { get; set; }
    public bool ReducedFlash { get; set; }
    public bool ThreatVisible { get; set; }
    public bool HighContrast { get; set; }
    public long SlowedUntil { get; set; }
    public EliteKind Elite {get;set;}
    public long EliteReadyAt {get;set;}
    public long EliteWarningUntil {get;set;}
    public Vector2 EliteWarningPoint {get;set;}
    private PixelAtlas atlas = null!;
    private Vector2 previousPosition, visualPosition;
    private float stride, previousStride, visualStride, hitTime, deathTime, poseTime, spawnTime = .24f;
    private float movementWeight, facingAngle, bodyLean;
    private Vector2 bodyOffset, actualVelocity;
    private int facingIndex, animationRow, animationColumn;
    private ulong motionFrame;
    private Vector2 recoil;
    private ActionExecution? playerAction;
    private long tick;
    private bool playerDead;
    private Color regionalTint = Colors.White;
    public Vector2 VisualPosition => visualPosition;
    public Vector2 ActualVelocity => actualVelocity;
    public float WalkDistance => stride * 14;
    public float VisualStride => visualStride;
    public int AnimationRow => animationRow;
    public int AnimationColumn => animationColumn;
    public int TeleportVersion { get; private set; }
    public override void _Ready()
    {
        atlas = PixelAtlas.Load(IsPlayer ? "warden" : Enemy!.Definition.Role == EnemyRole.Bellkeeper ? "bosses" : "enemies");
        previousPosition = visualPosition = Position;
        facingAngle = Facing.Angle();
        facingIndex = LocomotionRules.Facing(facingAngle, 0, IsPlayer ? 8 : 4);
        CollisionLayer = IsPlayer ? 2u : 4u;
        CollisionMask = 1u | (IsPlayer ? 4u : 2u | 4u);
        MotionMode = MotionModeEnum.Floating;
        AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = Radius } });
        var portrait = Enemy?.Definition.Portrait ?? "";
        if (portrait.Contains("glass") || portrait.Contains("mirror") || portrait.Contains("reed") || portrait.Contains("prism") || portrait.Contains("marsh")) regionalTint = new(.76f, 1f, .94f);
        else if (portrait.Contains("archive") || portrait.Contains("index") || portrait.Contains("null") || portrait.Contains("ink") || portrait.Contains("page")) regionalTint = new(.91f, .82f, 1f);
        else if (portrait.Contains("crown") || portrait.Contains("scar") || portrait.Contains("royal") || portrait.Contains("rift")) regionalTint = new(1f, .9f, .75f);
    }
    public void SetFrame(FrameId frame) { atlas = PixelAtlas.Load(frame.ToString().ToLowerInvariant()); animationRow = atlas.Idle; }
    public void BeginTick() { previousPosition = Position; previousStride = stride; }
    public void EndTick()
    {
        var travel = Position - previousPosition;
        if (travel.LengthSquared() > 48 * 48) { SnapMotion(); return; }
        actualVelocity = travel * Engine.PhysicsTicksPerSecond;
        if (Enemy?.Dead != true && !playerDead) stride += travel.Length() / 14;
        motionFrame = Engine.GetPhysicsFrames();
    }
    public void SnapMotion()
    {
        previousPosition = visualPosition = Position; previousStride = stride = visualStride = 0;
        actualVelocity = Velocity = Vector2.Zero; bodyOffset = Vector2.Zero; movementWeight = bodyLean = 0;
        facingAngle = Facing.Angle(); facingIndex = LocomotionRules.Facing(facingAngle, 0, IsPlayer ? 8 : 4);
        animationRow = atlas?.Idle ?? 0; animationColumn = IsPlayer ? facingIndex : facingIndex * 2;
        TeleportVersion++;
    }
    public void TeleportVisual() { SnapMotion(); deathTime = 0; playerDead = false; hitTime = 0; recoil = Vector2.Zero; playerAction = null; }
    public void PublishPose(PlayerCombat state) { playerAction = state.Action; tick = state.Tick; playerDead = state.Dead; }
    public void HitReaction(Vector2 direction, bool staggered)
    { hitTime = staggered ? .22f : .12f; recoil = direction.Normalized() * (staggered ? 3 : 1.5f); }
    public void Render(double delta, bool paused)
    {
        var dt = (float)Math.Min(delta, .05);
        if (!paused)
        {
            poseTime += dt;
            hitTime = Math.Max(0, hitTime - dt);
            spawnTime = Math.Max(0, spawnTime - dt);
            if (Enemy?.Dead == true || playerDead) deathTime = Math.Min(.65f, deathTime + dt);
            var fraction = Engine.GetPhysicsFrames() == motionFrame ? (float)Engine.GetPhysicsInterpolationFraction() : 1;
            visualPosition = previousPosition.Lerp(Position, fraction);
            visualStride = Mathf.Lerp(previousStride, stride, fraction);
            UpdateAnimation(dt, fraction);
        }
        QueueRedraw();
    }
    public void Move(Vector2 velocity) { Velocity = velocity; MoveAndSlide(); }
    public void MoveResponsive(Vector2 velocity, float speed)
    {
        var next = LocomotionRules.Advance(new(Velocity.X, Velocity.Y), speed > 0 ? new(velocity.X / speed, velocity.Y / speed) : System.Numerics.Vector2.Zero, speed, 1f / 60);
        Move(new(next.X, next.Y));
    }
    private void UpdateAnimation(float dt, float fraction)
    {
        var facing = playerAction is not null ? new Vector2(playerAction.Aim.X, playerAction.Aim.Y) : AttackAge >= 0 ? AttackAim : Facing;
        var committed = playerAction is not null || AttackAge >= 0;
        facingAngle = committed ? facing.Angle() : Mathf.LerpAngle(facingAngle, facing.Angle(), 1 - MathF.Exp(-38 * dt));
        facingIndex = LocomotionRules.Facing(facingAngle, facingIndex, IsPlayer ? 8 : 4);
        var row = atlas.Idle;
        var column = facingIndex;
        var speed = actualVelocity.Length();
        movementWeight = Mathf.Lerp(movementWeight, Math.Clamp(speed / 40, 0, 1), 1 - MathF.Exp(-24 * dt));
        var moving = speed > 3;
        var bob = -MathF.Abs(MathF.Sin(visualStride * Mathf.Pi)) * 1.15f;
        var targetOffset = new Vector2(0, Mathf.Lerp(MathF.Sin(poseTime * 2.4f + ActorId) * .45f, bob, movementWeight));
        var targetLean = Math.Clamp(actualVelocity.X / 128, -1, 1) * .025f * movementWeight;
        if (IsPlayer)
        {
            if (moving) row = atlas.Walk((int)visualStride);
            if (playerAction is { } action)
            {
                var age = Math.Max(0, action.Age(tick) - 1 + fraction);
                row = age < action.Definition.Windup ? atlas.Windup : age < action.Definition.Windup + action.Definition.Active + 6 ? atlas.Strike : moving ? atlas.Walk((int)visualStride) : atlas.Idle;
                if (action.Definition.Id == SkillId.Traverse)
                {
                    row = moving ? atlas.Walk((int)visualStride) : atlas.Idle;
                    var surge = MathF.Sin(MathF.PI * Math.Clamp(age / action.Definition.Duration, 0, 1));
                    targetOffset += facing * (2 * surge); targetLean = facing.X * .09f * surge;
                }
                else
                {
                    var extension = LocomotionRules.AttackExtension(age, action.Definition.Windup, action.Definition.Active, action.Definition.Recovery);
                    targetOffset += facing * extension; targetLean = facing.X * extension * .015f;
                }
            }
            if (hitTime > 0) row = atlas.Hit;
        }
        else if (Enemy!.Definition.Role == EnemyRole.Bellkeeper)
        {
            var portrait = Enemy.Definition.Portrait ?? "bellkeeper";
            row = portrait switch { "regional-forgeheart" => 1, "regional-mirrorregent" => 2, "regional-reedwidow" => 3, "regional-indexer" => 4, "regional-nullabbot" => 5, "regional-scarmarshal" => 6, "regional-firstpattern" => 7, _ => 0 };
            column = Math.Abs(facing.X) > .35f ? facing.X >= 0 ? 0 : 3 : facing.Y >= 0 ? 6 : 7;
            if (AttackAge >= 0)
            { column = (facing.X >= 0 ? 0 : 3) + (AttackAge < TellTicks ? 1 : AttackAge < TellTicks + 12 ? 2 : 0); targetOffset += facing * LocomotionRules.AttackExtension(Math.Max(0, AttackAge - 1 + fraction), TellTicks, 12, Enemy.Definition.Recovery, 4); }
        }
        else
        {
            var roleRow = Enemy!.Definition.Role switch { EnemyRole.Brute => 1, EnemyRole.Caster => 2, EnemyRole.Bellkeeper => 3, _ => 0 };
            row = roleRow;
            var cardinal = facingIndex;
            column = cardinal * 2 + (moving ? (int)visualStride % 2 : 0);
            if (AttackAge >= 0)
            { if (AttackAge < TellTicks + 12) row += 4; column = cardinal * 2 + (AttackAge >= TellTicks ? 1 : 0); targetOffset += facing * LocomotionRules.AttackExtension(Math.Max(0, AttackAge - 1 + fraction), TellTicks, 12, Enemy.Definition.Recovery); }
        }
        targetOffset -= recoil * (hitTime / .22f);
        if (deathTime > 0) targetOffset += new Vector2(Facing.X * deathTime * 5, deathTime * 9);
        bodyOffset = bodyOffset.Lerp(targetOffset, 1 - MathF.Exp(-32 * dt));
        bodyLean = Mathf.Lerp(bodyLean, targetLean, 1 - MathF.Exp(-32 * dt));
        animationRow = row; animationColumn = column;
    }
    public override void _Draw()
    {
        if (atlas is null || deathTime >= .65f) return;
        var offset = visualPosition - Position;
        var opacity = 1 - deathTime / .65f;
        DrawSetTransform(offset + new Vector2(0, 3), 0, new(1, .35f));
        DrawCircle(Vector2.Zero, Radius + 5, new Color(0, 0, 0, .48f * opacity));
        // Project the animated silhouette along the same light direction as the scenery.
        DrawSetTransformMatrix(new Transform2D(Vector2.Right, new Vector2(-.48f, -.24f), offset + new Vector2(3, 4)));
        atlas.Draw(this, animationColumn, animationRow, Vector2.Zero, new Color(0, 0, 0, .27f * opacity));
        DrawSetTransform(offset);
        if (IsPlayer && !playerDead)
        {
            DrawArc(Vector2.Zero, 10, 0, Mathf.Tau, 24, new Color("78c9ba", HighContrast?.9f:.3f), HighContrast?2:1);
            var facing = Vector2.FromAngle(facingAngle);
            DrawLine(facing * 12, facing * 16, new Color("b9f4dd", .7f));
        }
        var tint = regionalTint * (IsPlayer ? 1.14f : 1.04f);
        if (hitTime > .065f && !ReducedFlash) tint = new(1.35f, 1.3f, 1.15f);
        else if (Evasion) tint = new(.65f, 1, .93f);
        tint.A = opacity;
        DrawSetTransform(offset + bodyOffset, bodyLean);
        atlas.Draw(this, animationColumn, animationRow, Vector2.Zero, tint);
        DrawSetTransform(offset);
        if (spawnTime > 0) DrawArc(Vector2.Zero, 8 + (1 - spawnTime / .24f) * 12, 0, Mathf.Tau, 24, new Color("b7d4c2", spawnTime), 1);
        if (IsPlayer && AnchorHalo) DrawArc(new(0, -24), 13, 0, Mathf.Tau, 24, new Color("c7b3ef"), 1);
        if (!IsPlayer && Stunned && Enemy is { Dead: false })
        {
            for (var i=0;i<3;i++)
            { var angle=poseTime*5+i*Mathf.Tau/3; DrawRect(new(new Vector2(MathF.Cos(angle)*8,-50+MathF.Sin(angle)*2).Round(),new(2,2)),new Color("ffe4a6")); }
        }
        if (Enemy is { Dead: false } enemy && (enemy.Life < enemy.MaximumLife||ThreatVisible) && enemy.Definition.Role != EnemyRole.Bellkeeper)
        {
            DrawRect(new(-16, -49, 32, 4), new Color("10151b"));
            DrawRect(new(-15, -48, 30 * CombatMath.BarBasisPoints(enemy.Life, enemy.MaximumLife) / 10000f, 2), new Color("d59b83"));
            if (enemy.Stagger > 0) DrawRect(new(-15, -44, 30 * enemy.Stagger / (float)enemy.Definition.StaggerThreshold, 1), new Color("d2c392"));
        }
        if (DebugFootprint) DrawArc(Vector2.Zero, Radius, 0, Mathf.Tau, 24, Colors.White, 1);
    }
}
