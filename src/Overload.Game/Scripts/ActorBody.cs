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
    public long SlowedUntil { get; set; }
    private PixelAtlas atlas = null!;
    private Vector2 previousPosition, visualPosition;
    private float stride, hitTime, deathTime, poseTime, spawnTime = .24f;
    private Vector2 recoil;
    private ActionExecution? playerAction;
    private long tick;
    private bool playerDead;
    private Color regionalTint = Colors.White;
    public Vector2 VisualPosition => visualPosition;
    public override void _Ready()
    {
        atlas = PixelAtlas.Load(IsPlayer ? "warden" : Enemy!.Definition.Role == EnemyRole.Bellkeeper ? "bosses" : "enemies");
        previousPosition = visualPosition = Position;
        CollisionLayer = IsPlayer ? 2u : 4u;
        CollisionMask = 1u | (IsPlayer ? 4u : 2u | 4u);
        MotionMode = MotionModeEnum.Floating;
        AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = Radius } });
        var portrait = Enemy?.Definition.Portrait ?? "";
        if (portrait.Contains("glass") || portrait.Contains("mirror") || portrait.Contains("reed") || portrait.Contains("prism") || portrait.Contains("marsh")) regionalTint = new(.76f, 1f, .94f);
        else if (portrait.Contains("archive") || portrait.Contains("index") || portrait.Contains("null") || portrait.Contains("ink") || portrait.Contains("page")) regionalTint = new(.91f, .82f, 1f);
        else if (portrait.Contains("crown") || portrait.Contains("scar") || portrait.Contains("royal") || portrait.Contains("rift")) regionalTint = new(1f, .9f, .75f);
    }
    public void SetFrame(FrameId frame) => atlas = PixelAtlas.Load(frame.ToString().ToLowerInvariant());
    public void BeginTick() => previousPosition = Position;
    public void TeleportVisual() { previousPosition = visualPosition = Position; stride = 0; deathTime = 0; playerDead = false; hitTime = 0; playerAction = null; }
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
            else stride += Velocity.Length() * dt / 18;
        }
        var fraction = (float)Engine.GetPhysicsInterpolationFraction();
        visualPosition = previousPosition.DistanceSquaredTo(Position) > 48 * 48 ? Position : previousPosition.Lerp(Position, fraction);
        QueueRedraw();
    }
    public void Move(Vector2 velocity) { Velocity = velocity; MoveAndSlide(); }
    public override void _Draw()
    {
        if (atlas is null || deathTime >= .65f) return;
        var offset = visualPosition.Round() - Position;
        var opacity = 1 - deathTime / .65f;
        DrawSetTransform(offset + new Vector2(0, 3), 0, new(1, .35f));
        DrawCircle(Vector2.Zero, Radius + 5, new Color(0, 0, 0, .48f * opacity));
        DrawSetTransform(offset);
        if (IsPlayer && !playerDead)
        {
            DrawArc(Vector2.Zero, 10, 0, Mathf.Tau, 24, new Color("78c9ba", .3f), 1);
            DrawLine(Facing * 12, Facing * 16, new Color("b9f4dd", .7f));
        }
        var facing = playerAction is not null ? new Vector2(playerAction.Aim.X, playerAction.Aim.Y) : AttackAge >= 0 ? AttackAim : Facing;
        var octant = ((int)MathF.Round(facing.Angle() / (Mathf.Tau / 8)) + 8) % 8;
        var row = atlas.Idle;
        var column = octant;
        var bodyLean = 0f;
        var bodyOffset = Vector2.Zero;
        if (Velocity.LengthSquared() > 8) bodyOffset.Y = -MathF.Abs(MathF.Sin(stride * Mathf.Pi)) * 1.5f;
        else bodyOffset.Y = MathF.Sin(poseTime * 2.4f + ActorId) * .6f;
        if (IsPlayer)
        {
            if (Velocity.LengthSquared() > 8) row = atlas.Walk((int)stride);
            if (playerAction is { } action)
            {
                row = action.Phase(tick) switch { ActionPhase.Windup => atlas.Windup, ActionPhase.Active => atlas.Strike, _ => action.Age(tick) < action.Definition.Windup + action.Definition.Active + 6 ? atlas.Strike : atlas.Idle };
                if (action.Definition.Id == SkillId.Traverse)
                { row = atlas.Walk((int)(stride * 1.5f)); bodyOffset += facing * 2; }
                else if (action.Phase(tick) == ActionPhase.Windup)
                { var anticipation = action.Age(tick) / (float)Math.Max(1,action.Definition.Windup); bodyOffset -= facing * (2 * anticipation * anticipation); }
                else if (action.Phase(tick) == ActionPhase.Active) bodyOffset += facing * 3;
                else
                { var recoveryAge = action.Age(tick) - action.Definition.Windup - action.Definition.Active; bodyOffset += facing * (3 * MathF.Exp(-recoveryAge / 5f)); }
            }
            if (hitTime > 0) row = atlas.Hit;
        }
        else if (Enemy!.Definition.Role == EnemyRole.Bellkeeper)
        {
            var portrait = Enemy.Definition.Portrait ?? "bellkeeper";
            row = portrait switch { "regional-forgeheart" => 1, "regional-mirrorregent" => 2, "regional-reedwidow" => 3, "regional-indexer" => 4, "regional-nullabbot" => 5, "regional-scarmarshal" => 6, "regional-firstpattern" => 7, _ => 0 };
            column = Math.Abs(facing.X) > .35f ? facing.X >= 0 ? 0 : 3 : facing.Y >= 0 ? 6 : 7;
            if (AttackAge >= 0)
            { column = (facing.X >= 0 ? 0 : 3) + (AttackAge < TellTicks ? 1 : AttackAge < TellTicks + 12 ? 2 : 0); bodyOffset += facing * (AttackAge < TellTicks ? -2 * AttackAge / Math.Max(1f,TellTicks) : 4 * MathF.Exp(-(AttackAge - TellTicks) / 8f)); }
        }
        else
        {
            var roleRow = Enemy!.Definition.Role switch { EnemyRole.Brute => 1, EnemyRole.Caster => 2, EnemyRole.Bellkeeper => 3, _ => 0 };
            row = roleRow;
            var cardinal = ((int)MathF.Round(facing.Angle() / (Mathf.Pi / 2)) + 4) % 4;
            column = cardinal * 2 + (Velocity.LengthSquared() > 8 ? (int)stride % 2 : 0);
            if (AttackAge >= 0)
            { if (AttackAge < TellTicks + 12) row += 4; column = cardinal * 2 + (AttackAge >= TellTicks ? 1 : 0); bodyOffset += facing * (AttackAge < TellTicks ? -2 * AttackAge / Math.Max(1f,TellTicks) : 3 * MathF.Exp(-(AttackAge - TellTicks) / 6f)); }
        }
        if (deathTime > 0) bodyOffset += new Vector2(Facing.X * deathTime * 5, deathTime * 9);
        var tint = regionalTint;
        if (hitTime > .065f && !ReducedFlash) tint = new(1.35f, 1.3f, 1.15f);
        else if (Evasion) tint = new(.65f, 1, .93f);
        tint.A = opacity;
        DrawSetTransform(offset + (bodyOffset - recoil * (hitTime / .22f)).Round(), bodyLean);
        atlas.Draw(this, column, row, Vector2.Zero, tint);
        DrawSetTransform(offset);
        if (spawnTime > 0) DrawArc(Vector2.Zero, 8 + (1 - spawnTime / .24f) * 12, 0, Mathf.Tau, 24, new Color("b7d4c2", spawnTime), 1);
        if (IsPlayer && AnchorHalo) DrawArc(new(0, -24), 13, 0, Mathf.Tau, 24, new Color("c7b3ef"), 1);
        if (!IsPlayer && Stunned && Enemy is { Dead: false })
        {
            for (var i=0;i<3;i++)
            { var angle=poseTime*5+i*Mathf.Tau/3; DrawRect(new(new Vector2(MathF.Cos(angle)*8,-50+MathF.Sin(angle)*2).Round(),new(2,2)),new Color("ffe4a6")); }
        }
        if (Enemy is { Dead: false } enemy && enemy.Life < enemy.MaximumLife && enemy.Definition.Role != EnemyRole.Bellkeeper)
        {
            DrawRect(new(-16, -49, 32, 4), new Color("10151b"));
            DrawRect(new(-15, -48, 30 * CombatMath.BarBasisPoints(enemy.Life, enemy.MaximumLife) / 10000f, 2), new Color("d59b83"));
            if (enemy.Stagger > 0) DrawRect(new(-15, -44, 30 * enemy.Stagger / (float)enemy.Definition.StaggerThreshold, 1), new Color("d2c392"));
        }
        if (DebugFootprint) DrawArc(Vector2.Zero, Radius, 0, Mathf.Tau, 24, Colors.White, 1);
    }
}
