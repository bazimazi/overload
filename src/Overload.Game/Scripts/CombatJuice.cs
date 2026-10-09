using Godot;
using Overload.Domain;

namespace Overload.Game;

public partial class CombatEffects
{
    public void WarnHeavyAttack() => arena.Audio.Warn();
    public void CoverShards(Vector2 point) { Burst(point+new Vector2(0,-14),new("92e8d6"),24,70);arena.Audio.Play("defeat","Enemy"); }
    private sealed class Spark(Vector2 position, Vector2 velocity, Color color, float life)
    { public Vector2 Position = position, Velocity = velocity; public Color Color = color; public float Life = life, MaximumLife = life; }
    private sealed record Ghost(Vector2 Position, int Facing, float Life) { public float Remaining = Life; }
    private readonly List<Spark> sparks = [];
    private readonly List<Ghost> ghosts = [];
    private readonly Random visualRandom = new(173);
    private float ghostClock;
    private float juiceTime;
    private long lastJuiceAction = -1;
    private sealed class RuneFlash(Vector2 point,float radius,Color color) { public Vector2 Point=point;public float Radius=radius;public Color Color=color;public float Age; }
    private readonly List<RuneFlash> runeFlashes=[];
    private void RunePulse(Vector2 point,float radius,Color color) { if(runeFlashes.Count<24)runeFlashes.Add(new(point,radius,color));Burst(point,color,12,45); }
    private void Burst(Vector2 point, Color color, int count = 10, float force = 55)
    {
        for (var i = 0; i < count && sparks.Count < 256; i++)
        {
            var direction = Vector2.FromAngle((float)visualRandom.NextDouble() * Mathf.Tau);
            sparks.Add(new(point, direction * force * (.3f + (float)visualRandom.NextDouble()), color, .18f + (float)visualRandom.NextDouble() * .28f));
        }
    }
    public void RenderJuice(double delta, bool paused)
    {
        if (paused) return;
        var dt = (float)Math.Min(delta, .05);
        juiceTime += dt;
        foreach(var seal in defeatSeals)seal.Life-=dt;defeatSeals.RemoveAll(s=>s.Life<=0);
        foreach(var flash in runeFlashes)flash.Age+=dt;
        runeFlashes.RemoveAll(f=>f.Age>=.4f);
        if (arena.Playing && arena.PlayerState.Action is { } action && action.RootActionId != lastJuiceAction)
        {
            lastJuiceAction=action.RootActionId;
            if(action.DamagePercent>100)
            {numbers.Add(new(arena.Player.Position+new Vector2(-26,-66),"SURGE +50%",new Color("ffe3a1"),12));RunePulse(arena.Player.Position,36,new Color("ffe3a1"));}
            if(action.Definition.Id==SkillId.Flask)Burst(arena.Player.Position+new Vector2(0,-22),new Color("b1dbad"),14,15);
            if(action.Implementation!=ActionImplementation.Base && action.Definition.Id!=SkillId.Flask)
                numbers.Add(new(arena.Player.Position+new Vector2(-12,-54),action.Implementation.ToString().ToUpperInvariant(),new Color("b0e7d9")));
        }
        foreach (var s in sparks) { s.Position += s.Velocity * dt; s.Velocity *= MathF.Exp(-5 * dt); s.Life -= dt; }
        sparks.RemoveAll(s => s.Life <= 0);
        foreach (var ghost in ghosts) ghost.Remaining -= dt;
        ghosts.RemoveAll(g => g.Remaining <= 0);
        ghostClock -= dt;
        if (arena.Playing && arena.PlayerState.Action is { } a && (a.Definition.Id == SkillId.Traverse || a.Implementation == ActionImplementation.Pursuit) && ghostClock <= 0)
        {
            ghostClock = .04f;
            if (ghosts.Count < 8) ghosts.Add(new(arena.Player.VisualPosition.Round(), ((int)MathF.Round(arena.Player.Facing.Angle() / (Mathf.Tau / 8)) + 8) % 8, .18f));
        }
        QueueRedraw();
    }
    private void DrawJuice()
    {
        DrawHealthGlobes();
        var atlas = PixelAtlas.Load((arena.Character?.State.Frame ?? FrameId.Warden).ToString().ToLowerInvariant());
        foreach (var g in ghosts) atlas.Draw(this, g.Facing, atlas.Idle, g.Position, new Color(.4f, .9f, .85f, g.Remaining / g.Life * .24f));
        foreach (var s in sparks)
        {
            var size = s.Life / s.MaximumLife > .6f ? 2 : 1;
            DrawRect(new(s.Position.Round(), new(size, size)), new Color(s.Color, Math.Min(1, s.Life / .12f)));
        }
        foreach(var f in runeFlashes)
        {
            var progress=f.Age/.4f;var color=new Color(f.Color,(1-progress)*.65f);
            DrawArc(f.Point,f.Radius*(.4f+progress*.6f),0,Mathf.Tau,48,color,2);
            DrawArc(f.Point,f.Radius*(.25f+progress*.7f),0,Mathf.Tau,48,new Color(color,.2f),1);
        }
    }
    private void DrawAttackTrails()
    {
        foreach (var s in sweeps)
        {
            var alpha = s.Ticks / 8f;
            var color = s.Friendly ? new Color("99ecda") : new Color("f9b875");
            var points = s.Lane ? WorldQueries.Lane(s.Origin, s.Aim, s.Reach, 8) : WorldQueries.Sector(s.Origin, s.Aim, s.Reach, s.Degrees);
            DrawColoredPolygon(points, new Color(color, alpha * .14f));
            if (s.Lane)
            {
                DrawLine(s.Origin, s.Origin + s.Aim * s.Reach, new Color(color, alpha), 3);
                DrawLine(s.Origin, s.Origin + s.Aim * s.Reach, new Color("fff4d9", alpha), 1);
            }
            else
            {
                var start = s.Aim.Angle() - Mathf.DegToRad(s.Degrees) / 2;
                var end = start + Mathf.DegToRad(s.Degrees);
                DrawArc(s.Origin, s.Reach * .86f, start, end, 24, new Color(color, alpha * .45f), 5);
                DrawArc(s.Origin, s.Reach * .92f, start + (1 - alpha) * .25f, end, 24, new Color(color, alpha), 2);
                DrawArc(s.Origin, s.Reach * .94f, start + .12f, end - .06f, 24, new Color("e9fff1", alpha * .85f), 1);
            }
        }
    }
    private void DrawEnemyTelegraph(ActorBody enemy)
    {
        var d = enemy.Enemy!.Definition;
        var progress = enemy.AttackAge / (float)Math.Max(1, enemy.TellTicks);
        var color = new Color("f2ac6c");
        if (enemy.Volley)
        {
            var count = d.Role == EnemyRole.Bellkeeper ? arena.ActiveMutation == "mutation.split" ? 5 : d.VolleyCount == 1 ? 7 : d.VolleyCount : d.VolleyCount;
            for (var i = 0; i < count; i++)
            {
                var direction = enemy.AttackAim.Rotated((i - (count - 1) / 2f) * (arena.ActiveMutation == "mutation.split" ? .27f : .18f));
                var perpendicular = direction.Orthogonal() * d.ProjectileRadius;
                DrawColoredPolygon([enemy.Position + perpendicular, enemy.Position + direction * 700 + perpendicular, enemy.Position + direction * 700 - perpendicular, enemy.Position - perpendicular], new Color(color, .035f + progress * .08f));
                DrawLine(enemy.Position, enemy.Position + direction * 700, new Color(color, .22f + progress * .35f));
            }
        }
        else
        {
            var points = WorldQueries.Sector(enemy.Position, enemy.AttackAim, d.Reach, d.ArcDegrees);
            DrawColoredPolygon(points, new Color(color, .05f + progress * .13f));
            DrawPolyline([.. points, points[0]], new Color(color, .6f + progress * .4f), 1);
            var start = enemy.AttackAim.Angle() - Mathf.DegToRad(d.ArcDegrees) / 2;
            DrawArc(enemy.Position, d.Reach * progress, start, start + Mathf.DegToRad(d.ArcDegrees), 32, new Color(color, .12f + progress * .25f), 1);
            for (var i = 0; i <= 6; i++)
            {
                var direction = Vector2.FromAngle(start + Mathf.DegToRad(d.ArcDegrees) * i / 6);
                DrawLine(enemy.Position + direction * (d.Reach - 4), enemy.Position + direction * d.Reach, new Color(color, .8f));
            }
        }
        DrawArc(enemy.Position, enemy.Radius + 4, -Mathf.Pi / 2, -Mathf.Pi / 2 + Mathf.Tau * progress, 32, new Color("ffdb9f"), 1);
        // Shape marker and filling clock communicate the tell even without its color.
        var marker = enemy.Position + new Vector2(0, -57);
        DrawPolyline([marker + new Vector2(-3, 2), marker + new Vector2(0, -4), marker + new Vector2(3, 2), marker + new Vector2(-3, 2)], new Color("ffdb9f"));
    }
}
