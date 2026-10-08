using Godot;
using Overload.Domain;

namespace Overload.Game;

/// <summary>Output-resolution interface observes exact domain state without spending it.</summary>
public partial class ArenaHud
{
    private float shownLife = 1, shownFocus = 1;
    public override void _Process(double delta)
    {
        if (arena.PlayerState is null) return;
        var state = arena.PlayerState;
        var ease = 1 - MathF.Exp(-(float)delta * 12);
        shownLife = Mathf.Lerp(shownLife, CombatMath.BarBasisPoints(state.Life, state.MaximumLife) / 10000f, ease);
        shownFocus = Mathf.Lerp(shownFocus, state.Focus / (float)state.MaximumFocus, ease);
        UpdateSlotHints();
        UpdateCheckpointButtons();
        QueueRedraw();
    }
    private void CenterWrite(Vector2 baseline, string text, int size, Color color)
    {
        var width = ThemeDB.FallbackFont.GetStringSize(text, fontSize: FontSize(size)).X;
        Write(baseline - new Vector2(width / 2, 0), text, size, color);
    }
    private string Fit(string text, float width, int size)
    {
        if (ThemeDB.FallbackFont.GetStringSize(text, fontSize: FontSize(size)).X <= width) return text;
        while (text.Length > 1 && ThemeDB.FallbackFont.GetStringSize(text + "…", fontSize: FontSize(size)).X > width) text = text[..^1];
        return text + "…";
    }
    private void Surface(Rect2 rect, Color? border = null, float opacity = .96f)
    {
        DrawRect(rect, new Color("101a20", opacity));
        DrawRect(rect, border ?? new Color("48534b"), false, 1);
        DrawLine(rect.Position + new Vector2(1, 1), rect.Position + new Vector2(rect.Size.X - 1, 1), new Color("a38b61", .45f));
    }
    private static string SkillName(SkillId id) => System.Text.RegularExpressions.Regex.Replace(id.ToString(), "([a-z])([A-Z])", "$1 $2");
    public override void _Draw()
    {
        if (arena.PlayerState is null) return;
        if (home && MenuVisible) DrawHearth();
        else { if (arena.CheckpointRest) DrawCheckpointHud(); else DrawFightHud(); if (MenuVisible) DrawRect(new(Vector2.Zero, Size), new Color("050b10", .72f)); }
    }
    private void DrawHearth()
    {
        DrawRect(new(Vector2.Zero, Size), new Color("071015", .22f));
        if (Size.X < 1100) { DrawRect(new(Vector2.Zero, Size), new Color("071015", .4f)); return; }
        var x = Size.X * .56f;
        Write(new(x, 84), "A WORLD WRITTEN OVER ITSELF", 12, gold);
        Write(new(x, 120), "THE PALIMPSEST", 29, ink);
        DrawLine(new(x, 140), new(Size.X - 60, 140), new Color("b18e58", .55f));
        if (arena.Character?.State is { } character)
        {
            var completed=character.RegionalCampaign ? Math.Min(4,character.CheckpointRoom/4) : character.FractureUnlocked?4:0;
            Write(new(x,164),$"HEARTH / {completed} OF 4 REGIONS RECLAIMED",11,muted);
            for(var i=0;i<4;i++)
            { var p=new Vector2(x+i*28,185); DrawPolyline([p+new Vector2(-4,0),p+new Vector2(0,-5),p+new Vector2(4,0),p+new Vector2(0,5),p+new Vector2(-4,0)],i<completed?gold:muted.Darkened(.5f),1); }
        }
        var y = Size.Y - 156;
        Surface(new(x - 16, y - 38, Size.X - x - 38, 128), opacity: .83f);
        Write(new(x, y - 12), "YOU ARE MANYBORN", 12, gold);
        Write(new(x, y + 16), "Carry the memories of lives you never lived.", 16, ink);
        Write(new(x, y + 43), "Earn the power to rewrite your own laws.", 16, muted);
        Write(new(x, y + 71), "Three Frames / Nine signatures / Endless Fractures", 12, gold);
        Write(new(48, Size.Y - 26), "OVERLOAD / THE PALIMPSEST", 11, muted);
    }
    private void DrawFightHud()
    {
        var width = Size.X; var height = Size.Y; var s = arena.PlayerState;
        Surface(new(18, 16, 260, 68), new Color("5a6254"), .94f);
        Write(new(30, 35), (arena.Character?.State.Frame.ToString() ?? "WARDEN").ToUpperInvariant(), 12, gold);
        Write(new(130, 35), Fit($"{CounterText.Short(s.Life / 1000)} / {CounterText.Short(s.MaximumLife / 1000)}", 138, 12), 12, ink);
        Bar(new(30, 43, 236, 8), shownLife, new("b16d68"));
        DrawRect(new(30, 43, 236 * CombatMath.BarBasisPoints(s.Life, s.MaximumLife) / 10000f, 2), new Color("efc1a4"));
        Write(new(30, 69), s.RedCovenantActive ? $"LIFE RESERVED {s.ReservedPermille / 10f:0.#}%" : $"FOCUS {s.Focus / 1000} / {s.MaximumFocus / 1000}", 11, muted);
        Bar(new(30, 75, 236, 3), s.RedCovenantActive ? s.ReservedPermille / 400f : shownFocus, s.RedCovenantActive ? new("bc7378") : new("82bfbf"));
        DrawCombatMemories(width / 2 - 184);
        Surface(new(width - 188, 16, 170, 68), opacity: .92f);
        Write(new(width - 174, 37), $"{arena.Controls.Glyph("pause")} / PAUSE", 12, muted);
        Write(new(width - 174, 61), arena.Playing ? $"{arena.Enemies.Count(e => !e.Enemy!.Dead)} HOSTILES" : "HEARTH", 15, gold);
        if (arena.Playing)
        {
            CenterWrite(new(width / 2, 106), Fit(arena.Objective, width - 80, 13), 13, ink);
            if (arena.JourneyActive && arena.Audio.ShowTutorialHints) CenterWrite(new(width / 2, 127), Fit(arena.CombatLesson, width - 100, 12), 12, gold);
            else if (arena.OathPractice) CenterWrite(new(width / 2, 127), "ELSEWHERE / Place an anchor, then return. Placement has no evasion.", 12, gold);
            else if (arena.PracticeTier is not null) CenterWrite(new(width / 2, 127), "ISOLATED FRACTURE PRACTICE", 12, gold);
            var bossBody = arena.Enemies.FirstOrDefault(e => e.Enemy!.Definition.Role == EnemyRole.Bellkeeper);
            var boss = bossBody?.Enemy;
            if (boss is not null)
            {
                var rect = new Rect2(width / 2 - 220, 144, 440, 37);
                Surface(rect, new Color("796343"), .86f);
                CenterWrite(new(width / 2, 158), (boss.Definition.Name ?? "Bellkeeper").ToUpperInvariant() + (boss.Enraged ? " / ENRAGED" : ""), 11, gold);
                Bar(new(rect.Position.X + 10, 165, 420, 5), CombatMath.BarBasisPoints(boss.Life, boss.MaximumLife) / 10000f, new("b99a60"));
                Bar(new(rect.Position.X + 10, 174, 420, 2), Math.Min(1, boss.Stagger / (float)boss.Definition.StaggerThreshold), new("bec2b4"));
                var tell = bossBody!.Stunned ? "STAGGERED / STRIKE NOW" : bossBody.Recovering ? "RECOVERING / TAKE THE OPENING" : bossBody.AttackAge >= 0 ? bossBody.Volley ? "VOLLEY / FIND A CLEAR LANE" : "HEAVY STRIKE / LEAVE THE ARC" : "WATCH ITS NEXT MOVE";
                CenterWrite(new(width / 2,197),tell,10,bossBody.Stunned||bossBody.Recovering ? new("a0e8cd") : muted);
            }
            if (arena.ArrivalTime > 0 && boss is null && !MenuVisible)
            {
                var alpha = Math.Min(1, arena.ArrivalTime / .6f) * Math.Min(1, (2.6f - arena.ArrivalTime) / .25f);
                CenterWrite(new(width / 2, height * .32f), arena.ArrivalTitle.ToUpperInvariant(), 25, new Color(gold, alpha));
                if (arena.JourneyActive) CenterWrite(new(width / 2,height * .32f + 30),Fit(arena.RoomNarrative,width - 140,12),12,new Color(ink,alpha));
            }
            if (!string.IsNullOrEmpty(arena.EndgameHint)) CenterWrite(new(width / 2, height - 154), Fit(arena.EndgameHint, width - 100, 12), 12, gold);
            if (arena.ActiveMutation is { } mutation) CenterWrite(new(width / 2, height - 134), Fit(WorldLaws.MutationDescription(mutation), width - 100, 11), 11, muted);
            var reason = arena.Predictions.Values.SelectMany(p => p.Selection.Rejections).FirstOrDefault();
            if (reason is not null && arena.Audio.ShowTutorialHints) CenterWrite(new(width / 2, height - 117), Fit(reason.Reason, width - 100, 11), 11, muted);
        }
        DrawActionBar();
        if (arena.Playing && !MenuVisible && arena.CombatNoticeTime > 0) CenterWrite(new(width/2,height-121),Fit(arena.CombatNotice,width-80,13),13,new Color(gold,Math.Min(1,arena.CombatNoticeTime*3)));
        if (arena.Playing && !MenuVisible && s.Life * 4 < s.MaximumLife)
        {
            var alpha=arena.Audio.ReducedFlash ? .14f : .2f;
            var edge=new Color("b65f65",alpha);
            DrawRect(new(0,0,5,height),edge);DrawRect(new(width-5,0,5,height),edge);
            if (s.FlaskCharges > 0) CenterWrite(new(width/2,height-147),$"LOW LIFE / {arena.Controls.Glyph("flask")} to heal",12,new("efb29b"));
        }
        if (arena.Debug) DrawDiagnostics();
    }
    private void DrawCombatMemories(float left)
    {
        var s = arena.PlayerState;
        var names = new[] { "MOMENTUM", "ECHO", "STILLNESS", "RUPTURE" };
        var hints = new[] { "Travel 3m", "Evade a hit", "Stand + hit", "Stagger break" };
        for (var i = 0; i < 4; i++)
        {
            var type = (MemoryType)i; var token = s.Memories.Get(type); var x = left + i * 92;
            var color = token is null ? muted.Darkened(.25f) : i == 0 ? new Color("8fe3d2") : i == 1 ? new Color("c7adeb") : new Color("dfc98e");
            Surface(new(x, 16, 86, 51), token is null ? new("3e4a48") : color, .92f);
            var c = new Vector2(x + 12, 31);
            DrawPolyline([c + new Vector2(-4,0), c + new Vector2(0,-5), c + new Vector2(4,0), c + new Vector2(0,5), c + new Vector2(-4,0)], color, 1);
            if (token is not null) DrawRect(new(c - Vector2.One, new(2,2)), color);
            Write(new(x + 19, 35), names[i], i==0?8:9, token is null ? muted : color);
            Write(new(x + 8, 52), token is null ? hints[i] : $"{s.Memories.RemainingTicks(type) / 60f:0.0}s stored", 9, token is null ? muted : ink);
            var ratio = token is not null ? s.Memories.RemainingTicks(type) / (float)arena.Balance.Memories.LifetimeTicks : i == 0 ? (float)Math.Min(1, s.Memories.MomentumDistance / arena.Balance.Memories.MomentumDistancePixels) : 0;
            Bar(new(x + 8, 59, 70, 2), ratio, color);
        }
        Write(new(left, 81), $"STRAIN {s.Strain / 1000f:0.#}", 10, muted);
        Bar(new(left + 92, 74, 270, 3), s.Strain / (float)s.MaximumStrain, new("c3a16d"));
    }
}
