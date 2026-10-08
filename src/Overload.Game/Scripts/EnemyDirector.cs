using Godot;
using Overload.Domain;

namespace Overload.Game;

public sealed class EnemyDirector
{
    private sealed class Schedule
    {
        public long ReadyAt, RepathAt; public long BeganAt = -1; public int Attacks; public bool Heavy;
        public System.Numerics.Vector2[] Path = []; public int Waypoint; public System.Numerics.Vector2 Goal;
    }
    private readonly Dictionary<int, Schedule> schedules = [];
    public void Reset() => schedules.Clear();
    public void Retire(int actorId)=>schedules.Remove(actorId);
    public void Advance(ActorBody enemy, ActorBody player, long tick, CombatEffects effects,string? mutation=null, IReadOnlyList<ActorBody>? allies=null, TacticalNavigation? navigation=null)
    {
        var state = enemy.Enemy!;
        var definition = state.Definition;
        if (!schedules.TryGetValue(enemy.ActorId, out var schedule))
            schedules[enemy.ActorId] = schedule = new Schedule { ReadyAt = tick + 45 + enemy.ActorId * 7 % 40,Heavy=definition.Role is EnemyRole.Brute or EnemyRole.Bellkeeper };
        enemy.Stunned = tick < state.StunnedUntil;
        enemy.Recovering = false;
        if (state.Dead) {schedule.BeganAt=-1;enemy.AttackAge=-1;enemy.CollisionLayer=enemy.CollisionMask=0;enemy.Velocity=Vector2.Zero;return;}
        if (tick < state.StunnedUntil)
        {
            schedule.BeganAt = -1; schedule.ReadyAt = state.StunnedUntil + 30; enemy.AttackAge = -1;enemy.ReturnMark=null; enemy.Velocity=Vector2.Zero; return;
        }
        if (schedule.BeganAt >= 0)
        {
            enemy.Velocity = Vector2.Zero;
            enemy.AttackAge = (int)(tick - schedule.BeganAt);
            enemy.Recovering = enemy.AttackAge >= enemy.TellTicks;
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
        var sight = WorldQueries.ClearRay(enemy, enemy.Position, player.Position);
        var committed = allies?.Count(a => a != enemy && !a.Enemy!.Dead && a.AttackAge >= 0 && a.AttackAge < a.TellTicks) ?? 0;
        if (distance <= range && tick >= schedule.ReadyAt && committed < 3 && (!schedule.Heavy||schedules.Values.Count(s=>s.Heavy&&s.BeganAt>=0)<2) && sight)
        {
            schedule.BeganAt = tick; schedule.Attacks++;
            if (schedule.Heavy || ranged) effects.WarnHeavyAttack();
            enemy.AttackAge = 0; enemy.TellTicks = definition.Windup+(mutation=="mutation.patience"?24:0);
            enemy.Velocity = Vector2.Zero;
            if(boss&&(mutation=="mutation.anchor"||definition.Style==EnemyStyle.FirstPattern)&&ranged) { enemy.ReturnMark=enemy.Position;enemy.MoveAndCollide(enemy.Facing.Orthogonal()*64); }
            if(enemy.ReturnMark is null && (definition.Style==EnemyStyle.Mark || definition.Style==EnemyStyle.Sentinel&&boss))enemy.ReturnMark=player.Position;
            if(definition.Style==EnemyStyle.Mirror)enemy.MoveAndCollide(enemy.Facing.Orthogonal()*32);
            enemy.AttackAim = enemy.Facing; enemy.Volley = ranged;
        }
        else
        {
            var direction = ranged && distance < 140 ? -enemy.Facing : distance > (ranged ? 195 : range * .85f) || !sight ? enemy.Facing : Vector2.Zero;
            if (ranged && sight && distance is >= 140 and <= 195 && tick < schedule.ReadyAt)
                direction = enemy.Facing.Orthogonal() * (enemy.ActorId % 2 == 0 ? .45f : -.45f);
            if (navigation is not null && direction != Vector2.Zero)
            {
                var from = new System.Numerics.Vector2(enemy.Position.X,enemy.Position.Y);
                var goal = new System.Numerics.Vector2(player.Position.X,player.Position.Y);
                var next = from + new System.Numerics.Vector2(direction.X,direction.Y) * 24;
                if (!sight || !navigation.Clear(from,next,enemy.Radius))
                {
                    if (tick >= schedule.RepathAt || System.Numerics.Vector2.DistanceSquared(schedule.Goal,goal) > 32*32)
                    {
                        schedule.Path = navigation.FindPath(from,goal,enemy.Radius); schedule.Goal=goal; schedule.Waypoint=0;
                        schedule.RepathAt=tick+18+enemy.ActorId%5;
                    }
                    while (schedule.Waypoint < schedule.Path.Length && System.Numerics.Vector2.DistanceSquared(from,schedule.Path[schedule.Waypoint]) < 2.5f*2.5f) schedule.Waypoint++;
                    direction = schedule.Waypoint < schedule.Path.Length ? enemy.Position.DirectionTo(new(schedule.Path[schedule.Waypoint].X,schedule.Path[schedule.Waypoint].Y)) : Vector2.Zero;
                }
            }
            else if (direction != Vector2.Zero && !WorldQueries.ClearRay(enemy, enemy.Position, enemy.Position + direction * 26))
                direction = direction.Orthogonal() * (enemy.ActorId % 2 == 0 ? 1 : -1);
            if(boss&&(mutation=="mutation.procession"||definition.Style==EnemyStyle.Procession)&&direction!=Vector2.Zero)direction=(direction+direction.Orthogonal()*0.6f).Normalized();
            if(definition.Style==EnemyStyle.Skirmisher&&distance<range*1.4f)direction=(direction+enemy.Facing.Orthogonal()*(enemy.ActorId%2==0?1:-1)).Normalized();
            if (allies is not null && direction != Vector2.Zero)
            {
                var separation = Vector2.Zero;
                foreach (var ally in allies)
                {
                    if (ally == enemy || ally.Enemy!.Dead) continue;
                    var away = enemy.Position - ally.Position; var gap = away.Length(); var spacing = enemy.Radius + ally.Radius + 10;
                    if (gap > .1f && gap < spacing) separation += away / gap * (1 - gap / spacing);
                }
                direction = (direction + separation * .7f).Normalized();
            }
            enemy.Move(direction * definition.Speed*(tick < enemy.SlowedUntil ? 0.6f : 1));
        }
    }
}
