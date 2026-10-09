using Godot;
using Overload.Domain;

namespace Overload.Game;

public partial class ArenaHud
{
    private readonly List<Control> slotHints = [];
    private Rect2 ActionSlotRect(int index)
    {
        var tile=Math.Min(100,(Size.X-300)/6);
        return new((Size.X-tile*6)/2+tile*index,Size.Y-115,tile-6,98);
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
            var hint=slotHints[i];var rect=ActionSlotRect(i);hint.Position=rect.Position;hint.Size=rect.Size;hint.Visible=arena.Playing&&!MenuVisible&&!arena.LocalMapVisible;
            var skill=arena.Balance.Skills.Single(s=>s.Id==slots[i]);
            var description=slots[i]==SkillId.Traverse ? "Evade along movement or aim. Only the opening window evades contact." : slots[i]==SkillId.Flask ? "Heal over one second. Charges refill at checkpoints." : DescribeSkill(slots[i]);
            var pattern=arena.Predictions.GetValueOrDefault(slots[i])?.Selection;
            hint.TooltipText=$"{SkillName(slots[i])}\n{description}\n{skill.FocusCost} Focus / {skill.Cooldown/60f:0.#}s cooldown" + (pattern?.Accepted==true&&pattern.Implementation!=ActionImplementation.Base?$"\nNext: {pattern.Implementation}":"")+$"\nHold {arena.Controls.Glyph("preserve")} to use the base action and preserve memories.";
            if(pattern is {Rejections.Length:>0})hint.TooltipText+="\nFallback: "+string.Join("; ",pattern.Rejections.Select(r=>r.Reason));
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
