using Godot;
using Overload.Domain;

namespace Overload.Game;

public partial class ArenaHud
{
    private string fieldSheet = "";
    private HBoxContainer gameMenuDock = null!;
    private void InitializeGameDock()
    {
        gameMenuDock = new HBoxContainer { Visible = false }; AddChild(gameMenuDock);
        foreach (var entry in new[] { ("C  Character", "character"), ("I  Inventory", "inventory"), ("K  Skills", "skills"), ("J  Quests", "journal") })
        {
            var button = PanelButton(gameMenuDock, entry.Item1, () => OpenFieldPanel(entry.Item2));
            button.TooltipText = entry.Item1 + " [" + arena.Controls.Glyph(entry.Item2) + "]";
            button.AddThemeFontSizeOverride("font_size", 11); button.CustomMinimumSize = new(0, 30);
            var style = ButtonStyle(new("25211b"), new("67543a")); style.ContentMarginLeft = 6; style.ContentMarginRight = 6;
            button.AddThemeStyleboxOverride("normal", style);
        }
    }
    private void UpdateGameDock()
    {
        gameMenuDock.Visible = arena.Playing && !MenuVisible && !arena.LocalMapVisible;
        gameMenuDock.Position = new(18, 110); gameMenuDock.Size = new(Math.Min(370, Size.X * .43f), 30);
    }
    private bool HandleFieldPanelInput(InputEvent input)
    {
        if (capture is not null || input.IsEcho()) return false;
        var sheet = input.IsActionPressed("character") ? "character" : input.IsActionPressed("inventory") ? "inventory"
            : input.IsActionPressed("skills") ? "skills" : input.IsActionPressed("journal") ? "journal" : null;
        if (sheet is null) return false;
        if (arena.Playing) OpenFieldPanel(sheet);
        else if (!arena.PlayerState.Dead)
        { if (sheet == "character") CharacterMenu(); else if (sheet == "skills") SkillsMenu(); else if (sheet == "journal") AdventureJournal(); else Inventory(); }
        arena.Controls.ClearBuffer(); GetViewport().SetInputAsHandled(); return true;
    }
    private void OpenFieldPanel(string sheet)
    {
        if (arena.PlayerState.Dead) return;
        if (MenuVisible && fieldSheet == sheet) { ReturnFromBuild(); return; }
        arena.PauseForInspection(); fieldSheet = sheet;
        if (sheet == "character") CombatCharacterSheet(); else if (sheet == "skills") SkillsMenu();
        else if (sheet == "journal") AdventureJournal(); else CombatGearSheet();
    }
    private void CombatCharacterSheet() => CharacterMenu();
    private void CombatGearSheet() => Inventory();
    private void CombatInspectItem(Guid id) => InspectItem(id);
    private void ReturnFromBuild() { fieldSheet = ""; if (arena.Playing) arena.CloseWorldMenu(); else Title(); }
}

public partial class Arena
{
    public void PauseForInspection() { ResetPointerTravel(); LocalMapVisible = false; Paused = true; Controls.ClearBuffer(); Audio.SetPaused(true); }
}
