using Godot;
using Overload.Domain;

namespace Overload.Game;

/// <summary>Output-resolution interface observes exact domain state without spending it.</summary>
public partial class ArenaHud
{
    private float shownLife = 1, shownFocus = 1;
    private float hudTime;
    public override void _Process(double delta)
    {
        if (arena.PlayerState is null) return;
        var state = arena.PlayerState;
        if(!arena.Paused&&!arena.LocalMapVisible)hudTime+=(float)Math.Min(delta,.05);
        var ease = 1 - MathF.Exp(-(float)delta * 12);
        shownLife = Mathf.Lerp(shownLife, CombatMath.BarBasisPoints(state.Life, state.MaximumLife) / 10000f, ease);
        shownFocus = Mathf.Lerp(shownFocus, state.Focus / (float)state.MaximumFocus, ease);
        UpdateSlotHints();
        UpdateCheckpointButtons();
        UpdateGameDock();
        closeMenu.Visible=MenuVisible&&!home&&goBack is not null;
        ObserveDispatch(delta);
        QueueRedraw();
    }
    private void CenterWrite(Vector2 baseline, string text, int size, Color color)
    {
        var width = DisplayFont(size).GetStringSize(text, fontSize: FontSize(size)).X;
        Write(baseline - new Vector2(width / 2, 0), text, size, color);
    }
    private string Fit(string text, float width, int size)
    {
        if (DisplayFont(size).GetStringSize(text, fontSize: FontSize(size)).X <= width) return text;
        while (text.Length > 1 && DisplayFont(size).GetStringSize(text + "…", fontSize: FontSize(size)).X > width) text = text[..^1];
        return text + "…";
    }
    private void Surface(Rect2 rect, Color? border = null, float opacity = .96f)
    {
        RpgTheme.Frame(this, rect, border ?? gold, rect.Size.X > 200);
    }
    private static string SkillName(SkillId id) => System.Text.RegularExpressions.Regex.Replace(id.ToString(), "([a-z])([A-Z])", "$1 $2");
    public override void _Draw()
    {
        if (arena.PlayerState is null) return;
        DrawWorldVignette();
        if (home && MenuVisible) DrawHearth();
        else if(MenuVisible&&!arena.Playing&&!arena.CheckpointRest)DrawRect(new(Vector2.Zero,Size),new Color("050403",.68f));
        else { if (arena.CheckpointRest) DrawCheckpointHud(); else DrawFightHud(); if(arena.WorldActive&&!MenuVisible&&arena.LocalMapVisible)DrawLocalWorldMap(); if (MenuVisible) DrawRect(new(Vector2.Zero, Size), new Color("050403", .78f)); }
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
            var completed=character.World?.Resolved.Count??(character.RegionalCampaign ? Math.Min(4,character.CheckpointRoom/4) : character.FractureUnlocked?4:0);
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
}
