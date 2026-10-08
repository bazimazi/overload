using Godot;
using Overload.Domain;
using BigInteger = System.Numerics.BigInteger;

namespace Overload.Game;

public partial class CombatEffects : Node2D
{
    private sealed class Projectile
    {
        public Vector2 Position, Direction, Origin;
        public float Speed, Radius, Remaining;
        public BigInteger Damage;
        public bool Friendly;
        public bool Interceptable;
        public int MaxVictims, Stagger;
        public long RootActionId;
        public EffectProvenance Provenance;
        public long HostileExpiresAt;
        public SkillId? Skill;
        public HashSet<int> Victims = [];
    }
    private sealed class FloatingText(Vector2 position, string text, Color color, int size = 11)
    { public Vector2 Position = position; public string Text = text; public Color Color = color; public int Size = size; public int Ticks = 45; }
    private sealed record Sweep(Vector2 Origin, Vector2 Aim, float Reach, float Degrees, bool Friendly, bool Lane = false)
    { public int Ticks = 8; }
    private sealed record StrikePayload(SkillDefinition Skill, AttackRecipe Recipe, Vector2 Origin, Vector2 Aim,
        BigInteger Damage, EffectProvenance Provenance, bool FollowPlayer, bool AfterPull = false);
    private sealed class Strike(StrikePayload payload, long expiresAt)
    {
        public StrikePayload Payload = payload;
        public long ExpiresAt = expiresAt;
        public HashSet<int> Victims = [];
    }
    private sealed class Trail(Vector2 from, Vector2 to, bool crossing)
    { public Vector2 From = from, To = to; public bool Crossing = crossing; public int Ticks = 10; }
    private readonly List<Projectile> projectiles = [];
    private readonly List<FloatingText> numbers = [];
    private readonly Dictionary<(string Text,int Size),TextLine> numberLayouts=[];
    private readonly Queue<(string Text,int Size)> numberLayoutOrder=[];
    private readonly List<Sweep> sweeps = [];
    private readonly List<Strike> strikes = [];
    private readonly EffectTimeline<StrikePayload> delayed = new();
    private readonly Queue<CombatEvent> events = new();
    private readonly List<Trail> trails = [];
    private sealed class ShelterField(Vector2 origin, long expires) { public Vector2 Origin = origin; public long Expires = expires; public int Charges = 2; }
    private readonly List<ShelterField> shelters = [];
    public int ShelterCharges => shelters.Sum(s => s.Charges);
    private readonly Queue<string> log = new();
    private Random combatRandom = new(42);
    private readonly HashSet<long> criticalRoots = [];
    private readonly Queue<long> criticalHistory = [];
    private Arena arena = null!;
    private long? lastMomentum;
    private long? lastEcho;
    public IEnumerable<string> Log => log;
    private long? lastStillness,lastRupture;
    public int ProjectileCount => projectiles.Count;
    public int PendingEffectCount => delayed.Count;
    public IEnumerable<CombatEvent> RecentEvents => events;
    public int CriticalRolls { get; private set; }
    public int HitsDealt { get; private set; }
    public int HitsTaken { get; private set; }
    public void Initialize(Arena owner) => arena = owner;
    private void ClearNumberLayouts()
    {
        foreach(var layout in numberLayouts.Values)layout.Dispose();
        numberLayouts.Clear();numberLayoutOrder.Clear();
    }
    public override void _ExitTree(){ClearNumberLayouts();FreeProjectileDrawing();}
    private TextLine NumberLayout(string text,int size=11)
    {
        var key=(text,size);
        if(numberLayouts.TryGetValue(key,out var layout))return layout;
        if(numberLayouts.Count>=128){var expired=numberLayoutOrder.Dequeue();numberLayouts[expired].Dispose();numberLayouts.Remove(expired);}
        layout=new TextLine();layout.AddString(text,ThemeDB.FallbackFont,size);numberLayouts.Add(key,layout);numberLayoutOrder.Enqueue(key);return layout;
    }
    private void WriteEffect(Vector2 baseline,string text,int size,Color? color=null)
        => NumberLayout(text,size).Draw(GetCanvasItem(),baseline-new Vector2(0,size),color??Colors.White);
    public void Reset()
    {
        groundCasts.Clear();commandedEcho=null; sparks.Clear(); ghosts.Clear(); runeFlashes.Clear(); lastJuiceAction=-1;
        projectiles.Clear(); shelters.Clear(); numbers.Clear(); sweeps.Clear(); strikes.Clear(); delayed.Clear(); events.Clear(); trails.Clear(); log.Clear(); combatRandom = new(42);
        HitsDealt = 0; HitsTaken = 0;
        CriticalRolls = 0;
        criticalRoots.Clear(); criticalHistory.Clear();
        lastMomentum = null; lastEcho = null;lastStillness=null;lastRupture=null;
    }
    public void Record(string value) { log.Enqueue(value); while (log.Count > 6) log.Dequeue(); }
    public void TraceMovement(Vector2 from, Vector2 to, bool crossing)
    { if (from.DistanceTo(to) > 0.1f && trails.Count < 64) trails.Add(new(from, to, crossing)); }
    public void RecordMemories()
    {
        var memories = arena.PlayerState.Memories;
        if (memories.Momentum?.Id != lastMomentum)
            Record(memories.Momentum is null ? "Momentum spent / expired / cleared" : lastMomentum is null ? "Momentum acquired" : "Momentum refreshed");
        if (memories.Echo?.Id != lastEcho)
            Record(memories.Echo is null ? "Echo spent / expired / cleared" : $"Echo from attack #{memories.Echo.HostileAttackId}");
        if (memories.Momentum is { } m && m.Id != lastMomentum || memories.Echo is { } e && e.Id != lastEcho) arena.Audio.Play("memory", "Player");
        if (memories.Momentum is { } newMomentum && newMomentum.Id != lastMomentum) Burst(arena.Player.Position + new Vector2(0, -20), new Color("8fe3d2"), 12, 25);
        if (memories.Echo is { } newEcho && newEcho.Id != lastEcho) Burst(arena.Player.Position + new Vector2(0, -20), new Color("c4a6ee"), 12, 25);
        if(memories.Stillness is { } still&&still.Id!=lastStillness)Record("Stillness acquired: base hit after standing");
        if(memories.Rupture is { } rupture&&rupture.Id!=lastRupture)Record("Rupture acquired: base stagger break");
        lastStillness=memories.Stillness?.Id;lastRupture=memories.Rupture?.Id;
        lastMomentum = memories.Momentum?.Id; lastEcho = memories.Echo?.Id;
    }
    public void Launch(Vector2 origin, Vector2 direction, float speed, float radius, float range, BigInteger damage,
        bool friendly, int maxVictims, int stagger, long rootId, long hostileExpiresAt = 0, EffectProvenance? provenance = null, bool interceptable = true, SkillId? skill = null, HashSet<int>? sharedVictims = null)
    {
        if (projectiles.Count >= 200) { Record("Projectile limit reached"); return; }
        projectiles.Add(new Projectile { Position = origin, Origin=origin, Direction = direction, Speed = speed, Radius = radius,
            Remaining = range, Damage = damage, Friendly = friendly, Interceptable = interceptable, MaxVictims = maxVictims, Stagger = stagger, RootActionId = rootId, HostileExpiresAt = hostileExpiresAt,
            Victims=sharedVictims??[], Skill=skill, Provenance = provenance ?? new(rootId, 0, null, friendly ? SourceKind.BasePlayerAction : SourceKind.EnemyAction, 0) });
    }
    public void PlayerAction()
    {
        var action = arena.PlayerState.Action;
        if (action is { Emitted: false, Implementation: ActionImplementation.Shelter })
        { action.Emitted = true; shelters.Add(new(new(action.Origin.X, action.Origin.Y), arena.PlayerState.Tick + 120)); }
        if (action is null || action.Emitted || action.Definition.Family != ActionFamily.Assault || action.Phase(arena.PlayerState.Tick) != ActionPhase.Active) return;
        var skill = action.Definition;
        var aim = new Vector2(action.Aim.X, action.Aim.Y);
        action.Emitted = true;
        arena.ObserveTrialAction(skill.Id,action.Implementation,action.RootActionId);
        arena.Audio.Play(arena.Character?.State.Frame == FrameId.Threadseer ? "cast" : "strike", "Player");
        if(skill.Damage==0) { FrameAction(action,aim,0);return; }
        var budget = arena.PlayerState.AttackBudget(skill); CriticalRolls++;
        if (combatRandom.Next(100) < arena.Balance.Hero.CriticalPercent)
        {
            budget = budget * arena.Balance.Hero.CriticalMultiplierPercent / 100;
            criticalRoots.Add(action.RootActionId); criticalHistory.Enqueue(action.RootActionId);
            if (criticalHistory.Count > 128) criticalRoots.Remove(criticalHistory.Dequeue());
        }
        if(FrameAction(action,aim,budget))return;
        var damage = PatternExecution.Damage(budget, action.Implementation);
        var recipe = PatternExecution.Recipe(skill, action.Implementation);
        if (action.Implementation == ActionImplementation.Convergence)
            foreach (var target in WorldQueries.SectorHits(this, arena.Player.Position, aim, 128, 110, 4))
                if (target.Enemy is { Dead: false } enemy && enemy.Definition.Role != EnemyRole.Bellkeeper)
                { var delta = arena.Player.Position - target.Position; target.MoveAndCollide(delta.Normalized() * Math.Min(32, Math.Max(0, delta.Length() - 24))); }
        var root = new EffectRoot(action.RootActionId, action.Source);
        var payload = new StrikePayload(skill, recipe, arena.Player.Position, aim, damage.First, root.Primary,
            action.Implementation == ActionImplementation.Base, action.Implementation == ActionImplementation.Convergence);
        Record($"#{action.RootActionId} {skill.Id} / {action.Implementation}");
        Emit(payload);
        if (action.Implementation == ActionImplementation.Afterstrike)
        {
            if (!root.TryChild(root.Primary, out var child) || !delayed.TryEnqueue(arena.PlayerState.Tick + PatternExecution.AfterstrikeDelay,
                payload with { Damage = damage.Repeat, Provenance = child, FollowPlayer = false })) EffectFailure();
        }
    }
    private void Emit(StrikePayload payload)
    {
        var r = payload.Recipe;
        if(r.Geometry!=AttackGeometry.Projectile)arena.StrikeWorldCover(payload.Origin,payload.Aim,r);
        RecordEvent(payload.Provenance, -1, "emitted");
        if (r.Geometry == AttackGeometry.Projectile)
            Launch(payload.Origin, payload.Aim, payload.Skill.ProjectileSpeed, payload.Skill.ProjectileRadius, r.Reach,
                payload.Damage, true, r.MaxVictims, r.Stagger, payload.Provenance.RootActionId, provenance: payload.Provenance, skill:payload.Skill.Id);
        else
        {
            if (strikes.Count >= 256) { EffectFailure(); return; }
            strikes.Add(new(payload, arena.PlayerState.Tick + payload.Skill.Active));
            sweeps.Add(new(payload.Origin, payload.Aim, r.Reach, r.ArcDegrees, true, r.Geometry == AttackGeometry.Lane));
        }
    }
    private void EffectFailure() { Record("Effect budget exceeded"); GD.PushError("Authored effect budget exceeded; secondary chain stopped"); }
    private void RecordEvent(EffectProvenance provenance, int target, string kind)
    {
        events.Enqueue(new(provenance.RootActionId, provenance.EffectId, provenance.Source, target, kind, provenance.ParentEffectId));
        while (events.Count > 64) events.Dequeue();
    }
    private void HitEnemy(ActorBody target, BigInteger damage, int stagger, EffectProvenance provenance,Vector2? hitOrigin=null,Vector2? hitAim=null, SkillId? skill=null)
    {
        if (target.Enemy is not { Dead: false } state) return;
        if(skill==SkillId.Tether&&provenance.Source==SourceKind.BasePlayerAction) { if(state.Definition.Role==EnemyRole.Bellkeeper)stagger+=10;else target.SlowedUntil=arena.PlayerState.Tick+arena.Balance.Skills.Single(s=>s.Id==skill).SlowTicks; }
        var broken = state.ReceiveHit(damage, stagger, arena.PlayerState.Tick);
        arena.ObserveTrialHit(provenance.RootActionId);
        var aim=hitAim??arena.Player.Facing;var origin=hitOrigin??arena.Player.Position;
        arena.PlayerState.Memories.ObserveBaseAssaultHit(new(arena.Player.Position.X,arena.Player.Position.Y),new(aim.X,aim.Y),target.ActorId,broken,provenance.Source,new(origin.X,origin.Y),new(target.Position.X,target.Position.Y));
        arena.ObserveMastery(provenance.Source,broken,state.Definition.Role);
        HitsDealt++;
        arena.Audio.Play("hit", "Enemy");
        RecordEvent(provenance, target.ActorId, "hit");
        target.Flash = true;
        target.HitReaction(aim, broken);
        Burst(target.Position + new Vector2(0, -18), broken ? new Color("ffe2a1") : new Color("c5e5d6"), broken ? 16 : 8);
        arena.Impact(broken ? 1.5f : .6f);
        var critical = criticalRoots.Contains(provenance.RootActionId);
        numbers.Add(new(target.Position + new Vector2((target.ActorId % 3 - 1) * 6, -28), CounterText.Short(damage / 1000) + (critical ? "!" : ""), critical ? new Color("ffe3a1") : new Color("c8fff0"), critical ? 14 : 11));
        if (broken && !state.Dead) numbers.Add(new(target.Position + new Vector2(-22,-54),"STAGGER",new Color("ffe3a1"),9));
        if (broken) Record($"#{provenance.RootActionId}/{provenance.EffectId} staggered {state.Definition.Role}");
        if (state.Dead)
        {
            target.CollisionLayer = 0; target.CollisionMask = 0; target.Velocity = Vector2.Zero;
            Burst(target.Position + new Vector2(0, -18), new Color("d9c49b"), 20, 70);
            arena.Audio.Play("defeat", "Enemy");
            Record($"{state.Definition.Role} defeated");
        }
    }
    public void EnemySweep(Vector2 origin, Vector2 aim, float reach, float degrees, BigInteger damage, long rootId, long expiresAt)
    {
        arena.Audio.Play("strike", "Enemy");
        sweeps.Add(new(origin, aim, reach, degrees, false));
        if (WorldQueries.SectorHits(this, origin, aim, reach, degrees, 2).Contains(arena.Player))
            HitPlayer(damage, rootId, expiresAt, (arena.Player.Position - origin).Normalized());
    }
    public void MarkPulse(Vector2 point,BigInteger damage,long rootId,long expiresAt)
    { if(arena.Player.Position.DistanceTo(point)<=34+arena.Player.Radius)HitPlayer(damage,rootId,expiresAt,(arena.Player.Position-point).Normalized()); }
    private void HitPlayer(BigInteger damage, long rootId, long expiresAt, Vector2 incoming)
    {
        if (arena.PlayerState.Dead) return;
        var before = arena.PlayerState.Life;
        if (arena.PlayerState.ReceiveHit(damage, true, hostile: new HostileAttackEvidence(rootId, expiresAt, new(incoming.X, incoming.Y))))
        {
            HitsTaken++; arena.Player.Flash = true;
            arena.Player.HitReaction(incoming, true); arena.Impact(2);
            Burst(arena.Player.Position + new Vector2(0, -20), new Color("e7a68b"), 10);
            arena.Audio.Play("hit", "Player");
            numbers.Add(new(arena.Player.Position + new Vector2(0, -30), $"−{CounterText.Short((before - arena.PlayerState.Life) / 1000)}", new Color("ffa0a5")));
        }
        else { numbers.Add(new(arena.Player.Position + new Vector2(0, -30), "EVADE", new Color("8affec"))); arena.Audio.Play("evade", "Player"); }
    }
    public void Advance()
    {
        AdvanceFrames();
        shelters.RemoveAll(s => s.Expires <= arena.PlayerState.Tick || s.Charges == 0 || arena.PlayerState.Dead);
        if (arena.PlayerState.Dead)
        {
            delayed.Clear(); strikes.Clear(); projectiles.RemoveAll(p => p.Friendly);
        }
        else
        {
            foreach (var payload in delayed.TakeDue(arena.PlayerState.Tick)) Emit(payload);
            strikes.RemoveAll(s => s.ExpiresAt <= arena.PlayerState.Tick
                || s.Payload.FollowPlayer && arena.PlayerState.Action?.RootActionId != s.Payload.Provenance.RootActionId);
            foreach (var strike in strikes)
            {
                var p = strike.Payload; var r = p.Recipe;
                var origin = p.FollowPlayer ? arena.Player.Position : p.Origin;
                // Godot's broadphase still has pre-pull transforms until the next physics sync.
                // Evaluate the authored cone against current footprints after the immediate pull.
                var hits = p.AfterPull ? arena.Enemies.Where(e => WorldQueries.SectorOverlaps(origin, p.Aim, r.Reach, r.ArcDegrees, e.Position, e.Radius)
                    && WorldQueries.ClearRay(this, origin, e.Position)).OrderBy(e => e.ActorId).ToArray()
                    : r.Geometry == AttackGeometry.Lane ? WorldQueries.LaneHits(this, origin, p.Aim, r.Reach, r.HalfWidth)
                    : WorldQueries.SectorHits(this, origin, p.Aim, r.Reach, r.ArcDegrees, 4);
                foreach (var target in hits)
                {
                    if (strike.Victims.Count >= r.MaxVictims) break;
                    if (target.Enemy is not { Dead: false } || !strike.Victims.Add(target.ActorId)) continue;
                    HitEnemy(target, p.Damage, r.Stagger, p.Provenance,p.Origin,p.Aim,p.Skill.Id);
                    if (r.Push > 0 && target.Enemy.Definition.Role != EnemyRole.Bellkeeper)
                        target.MoveAndCollide((target.Position - origin).Normalized() * r.Push);
                }
            }
        }
        foreach (var projectile in projectiles.ToArray())
        {
            // A hostile projectile earlier in this same batch may have killed the actor.
            if (projectile.Friendly && arena.PlayerState.Dead) { projectiles.Remove(projectile); continue; }
            var step = Math.Min(projectile.Speed / 60, projectile.Remaining);
            var end = WorldQueries.ClipProjectile(this, projectile.Position, projectile.Position + projectile.Direction * step, projectile.Radius, out var wall);
            if(arena.WorldCoverCollision(projectile.Position,end,projectile.Friendly,!projectile.Friendly&&!projectile.Interceptable)) {projectiles.Remove(projectile);continue;}
            var shield = !projectile.Friendly && projectile.Interceptable ? shelters.Where(s => s.Charges > 0)
                .FirstOrDefault(s => WorldQueries.SegmentCircle(projectile.Position, end, s.Origin, 64 + projectile.Radius) is not null) : null;
            if (shield is not null) { shield.Charges--; projectiles.Remove(projectile); Record("Shelter intercepted a bolt"); continue; }
            var candidates = projectile.Friendly ? arena.Enemies.Where(e => !e.Enemy!.Dead) : [arena.Player];
            var hits = candidates.Where(e => !projectile.Victims.Contains(e.ActorId))
                .Select(e => (Body: e, Fraction: WorldQueries.SegmentCircle(projectile.Position, end, e.Position, projectile.Radius + e.Radius)))
                .Where(e => e.Fraction is not null).OrderBy(e => e.Fraction).ThenBy(e => e.Body.ActorId);
            foreach (var hit in hits)
            {
                projectile.Victims.Add(hit.Body.ActorId);
                if (projectile.Friendly) HitEnemy(hit.Body, projectile.Damage, projectile.Stagger, projectile.Provenance,projectile.Origin,projectile.Direction,projectile.Skill);
                else HitPlayer(projectile.Damage, projectile.RootActionId, projectile.HostileExpiresAt, projectile.Direction);
                if (projectile.Victims.Count >= projectile.MaxVictims) break;
            }
            projectile.Position = end; projectile.Remaining -= step;
            if (wall || projectile.Remaining <= 0 || projectile.Victims.Count >= projectile.MaxVictims) projectiles.Remove(projectile);
        }
        if (arena.PlayerState.Dead) { delayed.Clear(); strikes.Clear(); projectiles.RemoveAll(p => p.Friendly); }
        foreach (var number in numbers) { number.Position += new Vector2(0, number.Ticks > 30 ? -.5f : -.18f); number.Ticks--; }
        numbers.RemoveAll(n => n.Ticks <= 0);
        foreach (var sweep in sweeps) sweep.Ticks--;
        sweeps.RemoveAll(s => s.Ticks <= 0);
        foreach (var trail in trails) trail.Ticks--;
        trails.RemoveAll(t => t.Ticks <= 0);
        if (numbers.Count > 100) numbers.RemoveRange(0, numbers.Count - 100);
    }
    public override void _Draw()
    {
        if (arena is null) return;
        var qualityStart=arena.QualityTimestamp;
        DrawFrames(); DrawJuice();
        foreach (var field in shelters)
        { DrawCircle(field.Origin, 64, new Color(0.4f, 0.8f, 0.9f, 0.10f)); DrawArc(field.Origin, 64, 0, Mathf.Tau, 48, new Color("70bfd1"), 1); WriteEffect(field.Origin + new Vector2(-25, -10), $"SHELTER {field.Charges}", 10); }
        if (arena.PlayerState.Barrier > 0) DrawArc(arena.Player.Position, 16, 0, Mathf.Tau, 32, new Color("c7b3ef"), 2);
        if (arena.PlayerState.Anchor is { } anchor)
        {
            var point = new Vector2(anchor.Position.X, anchor.Position.Y);
            DrawArc(point, 12, 0, Mathf.Tau, 32, new Color("e4bf7d"), 2);
            WriteEffect(point + new Vector2(-24, -17), $"RETURN {(anchor.ExpiresAt - arena.PlayerState.Tick) / 60f:0.0}s", 9, new Color("e4bf7d"));
        }
        foreach (var trail in trails)
            DrawLine(trail.From, trail.To, new Color(trail.Crossing ? new Color("c7b3ef") : new Color("8bedd0"), trail.Ticks / 12f), 4);
        foreach (var echo in delayed.Values)
        {
            var color = new Color(0.78f, 0.7f, 0.94f, 0.5f);
            if (echo.Recipe.Geometry == AttackGeometry.Projectile) DrawLine(echo.Origin, echo.Origin + echo.Aim * echo.Recipe.Reach, color, 1);
            else DrawPolyline([.. WorldQueries.Sector(echo.Origin, echo.Aim, echo.Recipe.Reach, echo.Recipe.ArcDegrees), echo.Origin], color, 1);
            DrawCircle(echo.Origin, 4, color);
            WriteEffect(echo.Origin + new Vector2(-26, -32), "AFTERSTRIKE", 9, color);
        }
        foreach (var enemy in arena.Enemies.Where(e => !e.Enemy!.Dead && e.AttackAge >= 0 && e.AttackAge < e.TellTicks)) DrawEnemyTelegraph(enemy);
        DrawAttackTrails();
        DrawProjectiles();
        foreach (var n in numbers) WriteEffect(n.Position.Round(),n.Text,n.Size,new Color(n.Color,Math.Min(1,n.Ticks/15f)));
        var action = arena.PlayerState.Action;
        if (action is not null && action.Phase(arena.PlayerState.Tick) == ActionPhase.Windup)
        {
            var direction = new Vector2(action.Aim.X, action.Aim.Y);
            if (action.Implementation == ActionImplementation.Convergence)
                DrawPolyline(WorldQueries.Sector(arena.Player.Position, direction, 96, 110), new Color("c7b3ef"), 2);
            DrawLine(arena.Player.Position, arena.Player.Position + direction * 22, new Color("b7fff0"), 2);
        }
        arena.RecordQualityStage("effect draw",qualityStart);
    }
}
