using Godot;
using Overload.Domain;

namespace Overload.Game;

public partial class ArenaHud
{
    private readonly List<Control> slotHints = [];
    private Rect2 ActionSlotRect(int index)
    {
        var tile=Math.Min(146,(Size.X-40)/6);
        return new((Size.X-tile*6)/2+tile*index,Size.Y-94,tile-8,78);
    }
    private SkillId[] ActionSlots() => [arena.EquippedAction(SkillId.Cleave),arena.EquippedAction(SkillId.ShieldPulse),arena.EquippedAction(SkillId.ChainLance),arena.EquippedAction(SkillId.Faultline),SkillId.Traverse,SkillId.Flask];
    private void InitializeSlotHints()
    {
        for(var i=0;i<6;i++)
        { var hint=new Control { MouseFilter=MouseFilterEnum.Stop,MouseDefaultCursorShape=CursorShape.Help };AddChild(hint);slotHints.Add(hint); }
    }
    private void UpdateSlotHints()
    {
        var slots=ActionSlots();
        for(var i=0;i<slotHints.Count;i++)
        {
            var hint=slotHints[i];var rect=ActionSlotRect(i);hint.Position=rect.Position;hint.Size=rect.Size;hint.Visible=arena.Playing&&!MenuVisible;
            var skill=arena.Balance.Skills.Single(s=>s.Id==slots[i]);
            var description=slots[i]==SkillId.Traverse ? "Evade along movement or aim. Only the opening window evades contact." : slots[i]==SkillId.Flask ? "Heal over one second. Charges refill at checkpoints." : FrameRules.SkillDescription(slots[i]);
            var pattern=arena.Predictions.GetValueOrDefault(slots[i])?.Selection;
            hint.TooltipText=$"{SkillName(slots[i])}\n{description}\n{skill.FocusCost} Focus / {skill.Cooldown/60f:0.#}s cooldown" + (pattern?.Accepted==true&&pattern.Implementation!=ActionImplementation.Base?$"\nNext: {pattern.Implementation}":"");
        }
    }
    private void DrawActionBar()
    {
        var s = arena.PlayerState; var width = Size.X; var height = Size.Y;
        DrawRect(new(0, height - 108, width, 108), new Color("091217", .93f));
        DrawLine(new(18, height - 108), new(width - 18, height - 108), new Color("71634c", .6f));
        var names = new[] { "cleave", "pulse", "lance", "special", "evade", "flask" };
        var ids = ActionSlots();
        var tile = Math.Min(146, (width - 40) / 6); var start = (width - tile * 6) / 2;
        for (var i = 0; i < 6; i++)
        {
            var id = ids[i]; var skill = arena.Balance.Skills.Single(k => k.Id == id); var cooldown = s.Cooldown(id);
            var rect=ActionSlotRect(i);var x=rect.Position.X;var y=rect.Position.Y;var w=rect.Size.X;
            var prediction = arena.Predictions.GetValueOrDefault(id);
            var pattern = prediction?.Selection.Accepted == true && prediction.Selection.Implementation != ActionImplementation.Base;
            var active = s.Action?.Definition.Id == id;
            var color = pattern ? new Color("8cddd0") : active ? gold : new Color("5c6458");
            Surface(new(x, y, w, 78), color, .96f);
            if (active) DrawRect(new(x + 1, y + 1, w - 2, 76), new Color(gold, .12f));
            DrawSkillIcon(new(x + 23, y + 28), i, cooldown > 0 ? muted.Darkened(.45f) : pattern ? new("8cddd0") : gold);
            Write(new(x + 44, y + 19), arena.Controls.Glyph(names[i]), 11, gold);
            Write(new(x + 44, y + 37), Fit(SkillName(id), w - 48, 11), 11, ink);
            var hint = cooldown > 0 ? $"{cooldown / 60f:0.0}s" : id == SkillId.Flask ? $"{s.FlaskCharges} charges" : id == SkillId.Traverse && s.ElsewhereActive ? s.Anchor is null ? "Place anchor" : "Return" : pattern ? prediction!.Selection.Implementation.ToString() : skill.FocusCost == 0 ? "Ready / free" : s.RedCovenantActive ? $"{skill.FocusCost * .4f:0.#}% Life" : $"{skill.FocusCost} Focus";
            Write(new(x + 10, y + 62), Fit(hint, w - 20, 11), 11, pattern ? new("8cddd0") : muted);
            if (cooldown > 0 && skill.Cooldown > 0) Bar(new(x + 8, y + 71, w - 16, 2), 1 - cooldown / (float)skill.Cooldown, gold);
            else if (pattern) DrawLine(new(x + 8, y + 71), new(x + w - 8, y + 71), new Color("8cddd0", .65f));
        }
        var c = arena.Character?.State;
        if (c is not null && width >= 1200)
        {
            Write(new(20, height - 71), "LEVEL " + CounterText.Short(c.ValidatedLevel), 12, gold);
            Write(new(20, height - 49), CounterText.Short(c.Gold) + " GOLD", 11, muted);
            var next = Progression.TotalXp(c.ValidatedLevel + 1); var current = Progression.TotalXp(c.ValidatedLevel);
            Bar(new(20, height - 31, Math.Max(50, start - 40), 3), CombatMath.BarBasisPoints(c.TotalXp - current, next - current) / 10000f, new("9fae81"));
        }
    }
    private void DrawSkillIcon(Vector2 p, int slot, Color color)
    {
        if (slot == 0) { DrawLine(p + new Vector2(-8,8), p + new Vector2(8,-8), color, 3); DrawLine(p + new Vector2(-8,2), p + new Vector2(-2,8), color, 2); DrawRect(new(p + new Vector2(7,-10), new(3,3)), new Color("e9e3c9")); }
        else if (slot == 1) { DrawPolyline([p+new Vector2(-8,-8),p+new Vector2(8,-8),p+new Vector2(7,3),p+new Vector2(0,10),p+new Vector2(-7,3),p+new Vector2(-8,-8)],color,2); DrawLine(p+new Vector2(0,-5),p+new Vector2(0,4),color); }
        else if (slot == 2) DrawPolyline([p+new Vector2(3,-11),p+new Vector2(-5,1),p+new Vector2(3,-1),p+new Vector2(-3,11)],color,3);
        else if (slot == 3) { DrawPolyline([p+new Vector2(0,-11),p+new Vector2(9,0),p+new Vector2(0,11),p+new Vector2(-9,0),p+new Vector2(0,-11)],color,2); DrawArc(p,4,0,Mathf.Tau,16,color); }
        else if (slot == 4) { DrawPolyline([p+new Vector2(-8,-8),p+new Vector2(1,0),p+new Vector2(-8,8)],color,2); DrawPolyline([p+new Vector2(0,-8),p+new Vector2(9,0),p+new Vector2(0,8)],color,2); }
        else { DrawPolyline([p+new Vector2(-4,-10),p+new Vector2(4,-10),p+new Vector2(4,-4),p+new Vector2(8,1),p+new Vector2(7,9),p+new Vector2(-7,9),p+new Vector2(-8,1),p+new Vector2(-4,-4),p+new Vector2(-4,-10)],color,2); DrawLine(p+new Vector2(-4,4),p+new Vector2(4,4),color,3); }
    }
    private void DrawDiagnostics()
    {
        var s = arena.PlayerState;
        Surface(new(24, 190, 470, 240), opacity: .96f);
        Write(new(38, 211), $"Tick {s.Tick} / {s.Action?.Definition.Id.ToString() ?? "Idle"} {s.Action?.Phase(s.Tick)}", 13, ink);
        Write(new(38, 234), $"{s.LastSelection?.Implementation.ToString() ?? "Base"} / {s.LastReason}", 12, gold);
        Write(new(38, 256), $"Strain {s.Strain / 1000f:0.0} / Projectiles {arena.Effects.ProjectileCount}", 12, muted);
        var y = 286; foreach (var line in arena.Effects.Log) { Write(new(38, y), Fit(line, 440, 11), 11, muted); y += 20; }
    }
}
