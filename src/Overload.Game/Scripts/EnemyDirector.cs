using Godot;
using Overload.Domain;

namespace Overload.Game;

public sealed class EnemyDirector
{
    private sealed class Schedule { public long ReadyAt; public long BeganAt = -1; public int Attacks; public bool Heavy; }
    private readonly Dictionary<int, Schedule> schedules = [];
    public void Reset() => schedules.Clear();
    public void Advance(ActorBody enemy, ActorBody player, long tick, CombatEffects effects,string? mutation=null)
    {
        var state = enemy.Enemy!;
        var definition = state.Definition;
        if (!schedules.TryGetValue(enemy.ActorId, out var schedule))
            schedules[enemy.ActorId] = schedule = new Schedule { ReadyAt = tick + 45 + enemy.ActorId * 7 % 40,Heavy=definition.Role is EnemyRole.Brute or EnemyRole.Bellkeeper };
        if (state.Dead) {schedule.BeganAt=-1;return;}
        if (tick < state.StunnedUntil)
        {
            schedule.BeganAt = -1; schedule.ReadyAt = state.StunnedUntil + 30; enemy.AttackAge = -1;enemy.ReturnMark=null; return;
        }
        if (schedule.BeganAt >= 0)
        {
            enemy.AttackAge = (int)(tick - schedule.BeganAt);
            if (enemy.AttackAge == enemy.TellTicks)
            {
                if (enemy.Volley)
                {
                    var rootId = ((long)enemy.ActorId << 32) | (uint)schedule.Attacks;
                    var speed=definition.ProjectileSpeed*(mutation=="mutation.split"?0.8f:1);
                    var expiresAt = tick + (long)Math.Ceiling(700 * 60 / speed) + 1;
                    var count = definition.Role == EnemyRole.Bellkeeper ? mutation=="mutation.split"?5:definition.VolleyCount==1?7:definition.VolleyCount : definition.VolleyCount;
                    for (var i = 0; i < count; i++)
                    {
                        var direction = enemy.AttackAim.Rotated((i - (count - 1) / 2f) * (mutation=="mutation.split"?0.27f:0.18f));
                        effects.Launch(enemy.Position, direction, speed, definition.ProjectileRadius,
                            700, state.Damage, false, 1, 0, rootId, expiresAt, interceptable: definition.Role != EnemyRole.Bellkeeper);
                    }
                }
                else
                {
                    effects.EnemySweep(enemy.Position, enemy.AttackAim, definition.Reach, definition.ArcDegrees, state.Damage, ((long)enemy.ActorId << 32) | (uint)schedule.Attacks, tick + 1);
                }
            }
            if(enemy.AttackAge==enemy.TellTicks && enemy.ReturnMark is { } pulseMark)effects.MarkPulse(pulseMark,state.Damage,((long)enemy.ActorId<<32)|(uint)schedule.Attacks,tick+1);
            var recovery = state.Enraged ? Math.Max(48, definition.Recovery * 2 / 3) : definition.Recovery;
            if (enemy.AttackAge >= enemy.TellTicks + recovery)
            {
                if(enemy.ReturnMark is { } mark) { if(mutation=="mutation.anchor"||definition.Style==EnemyStyle.FirstPattern)enemy.Position=mark;enemy.ReturnMark=null; }
                schedule.BeganAt = -1; enemy.AttackAge = -1; schedule.ReadyAt = tick + 18;
            }
            return;
        }

        var delta = player.Position - enemy.Position;
        var distance = delta.Length();
        if (distance > 1) enemy.Facing = delta.Normalized();
        var boss = definition.Role == EnemyRole.Bellkeeper;
        var ranged = definition.Role == EnemyRole.Caster || boss && (definition.Style==EnemyStyle.Sentinel&&distance>definition.Reach || (definition.Style==EnemyStyle.Fan?schedule.Attacks%3!=0:schedule.Attacks%2==1));
        var range = ranged ? definition.Style==EnemyStyle.Sentinel?400:260 : definition.Reach * 0.85f;
        if (distance <= range && tick >= schedule.ReadyAt && (!schedule.Heavy||schedules.Values.Count(s=>s.Heavy&&s.BeganAt>=0)<2) && WorldQueries.ClearRay(enemy, enemy.Position, player.Position))
        {
            schedule.BeganAt = tick; schedule.Attacks++;
            enemy.AttackAge = 0; enemy.TellTicks = definition.Windup+(mutation=="mutation.patience"?24:0);
            if(boss&&(mutation=="mutation.anchor"||definition.Style==EnemyStyle.FirstPattern)&&ranged) { enemy.ReturnMark=enemy.Position;enemy.MoveAndCollide(new Vector2(0,enemy.Position.Y<185?64:-64)); }
            if(enemy.ReturnMark is null && (definition.Style==EnemyStyle.Mark || definition.Style==EnemyStyle.Sentinel&&boss))enemy.ReturnMark=player.Position;
            if(definition.Style==EnemyStyle.Mirror)enemy.MoveAndCollide(enemy.Facing.Orthogonal()*32);
            enemy.AttackAim = enemy.Facing; enemy.Volley = ranged;
        }
        else
        {
            var direction = ranged && distance < 110 ? -enemy.Facing : distance > range * 0.85f ? enemy.Facing : Vector2.Zero;
            // A small deterministic side-step allows the authored pillars to be routed around.
            if (direction != Vector2.Zero && !WorldQueries.ClearRay(enemy, enemy.Position, enemy.Position + direction * 26))
                direction = direction.Orthogonal() * (enemy.ActorId % 2 == 0 ? 1 : -1);
            if(boss&&(mutation=="mutation.procession"||definition.Style==EnemyStyle.Procession)&&direction!=Vector2.Zero)direction=(direction+direction.Orthogonal()*0.6f).Normalized();
            if(definition.Style==EnemyStyle.Skirmisher&&distance<range*1.4f)direction=(direction+enemy.Facing.Orthogonal()*(enemy.ActorId%2==0?1:-1)).Normalized();
            enemy.Move(direction * definition.Speed*(tick < enemy.SlowedUntil ? 0.6f : 1));
        }
    }
}
