using Godot;
using Overload.Domain;

namespace Overload.Game;

/// <summary>Physics footprint and authored Warden/court vector sprites; no resource authority.</summary>
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
    public Vector2 AttackAim { get; set; }
    public float Radius => IsPlayer ? 7 : Enemy!.Definition.Role == EnemyRole.Bellkeeper ? 17 : 9;
    private Texture2D portrait = null!;
    public bool AnchorHalo { get; set; }
    public bool ReducedFlash { get; set; }
    public override void _Ready()
    {
        portrait = GD.Load<Texture2D>($"res://Assets/{(IsPlayer ? "warden" : Enemy!.Definition.Portrait ?? Enemy.Definition.Role.ToString().ToLowerInvariant())}.svg");
        CollisionLayer = IsPlayer ? 2u : 4u;
        CollisionMask = 1u | (IsPlayer ? 4u : 2u | 4u);
        MotionMode = MotionModeEnum.Floating;
        AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = Radius } });
    }
    public void SetFrame(FrameId frame) { portrait=GD.Load<Texture2D>($"res://Assets/{frame.ToString().ToLowerInvariant()}.svg"); }
    public long SlowedUntil { get; set; }
    public void Move(Vector2 velocity)
    {
        Velocity = velocity;
        MoveAndSlide();
    }
    public override void _Draw()
    {
        var offset = Position.Round() - Position;
        DrawSetTransform(offset);
        var role = Enemy?.Definition.Role;
        var color = IsPlayer ? new Color("7ee6d4") : role switch
        {
            EnemyRole.Caster => new Color("d9a4ee"), EnemyRole.Brute => new Color("d49968"),
            EnemyRole.Bellkeeper => new Color("e7b767"), _ => new Color("e47d86")
        };
        DrawCircle(new Vector2(0, 3), Radius + 3, new Color(0, 0, 0, 0.4f));
        var boss = role == EnemyRole.Bellkeeper;
        var size = boss ? new Vector2(48,54) : new Vector2(32,43);
        var bob = Velocity.LengthSquared() > 1 ? (int)(Time.GetTicksMsec() / 120 % 2) : 0;
        var tint = Flash && !ReducedFlash ? new Color(1.5f,1.5f,1.5f) : Evasion ? new Color(.7f,1,1) : Colors.White;
        DrawTextureRect(portrait, new Rect2(-size.X / 2, -size.Y + 5 - bob, size.X, size.Y), false, tint);
        if(IsPlayer&&AnchorHalo)DrawArc(new(0,-24),13,0,Mathf.Tau,24,new Color("c7b3ef"),1);
        if (IsPlayer) DrawLine(Facing * 14, Facing * 19, new Color("b8f0df"), 1);
        if (Enemy is { } enemy && enemy.Life < enemy.MaximumLife)
        {
            DrawRect(new Rect2(-15, -35, 30, 3), new Color("19202a"));
            DrawRect(new Rect2(-15, -35, 30 * CombatMath.BarBasisPoints(enemy.Life, enemy.MaximumLife) / 10000f, 3), color);
        }
        if (DebugFootprint) DrawArc(Vector2.Zero, Radius, 0, Mathf.Tau, 24, Colors.White, 1);
    }
}
