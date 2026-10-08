using Godot;

namespace Overload.Game;
public partial class ArenaHud
{
    private Action? goBack;
    private int tutorialPage;
    private void Tutorial()
    {
        var pages = new (string Title, string Body)[]
        {
            ("Move. Aim. Commit.", "WASD / left stick moves. Mouse / right stick aims. Hold left click / RB for your Frame's free basic attack.\n\nAmber shapes mark a locked attack. Step out, then strike during recovery.\nQ, E and R / X, Y and B use your three selected active skills. These spend Focus, or reserve Life with Red Covenant. Control wells and the commanded echo retain their authored behavior; direct Assault patterns need a compatible skill."),
            ("A memory of motion", "Travel 3 meters within 2 seconds while hostiles remain to store Momentum. It lasts 5 seconds.\n\nWith the starting bindings, your next attack advances as Pursuit. Using Traverse instead spends it on Crossing, a long passage through enemies. Walls still stop you."),
            ("A memory of danger", "Space / A starts Traverse. Its first seven ticks evade hostile contact. Dodging an actual hit earns Echo; an empty dodge does not.\n\nWith Afterstrike bound, your next attack repeats from its original location. Watch the Next prediction and your Strain. At high Strain, the base action remains available."),
            ("Change the contract", "At Hearth, equip up to three patterns. Convergence spends both memories on a pull and a stronger cone. Shelter uses Echo to leave a field that stops two ordinary bolts. Reprieve uses Echo and one flask for 25% healing plus a 15% barrier.\n\nElsewhere replaces your dodge: mark your feet, wait, then return to the anchor. Placement has NO evasion. Practice the oath before equipping it.\n\nRed Covenant replaces Focus costs with 4-second maximum-Life reservations (0.4% per base Focus point, capped at 40%). Expiry restores capacity without healing; Focus regeneration is inactive. Only one oath can be active."),
            ("Return stronger", "Room clears save XP, gold, Alloy and one item. A full bag converts its drop to 25 gold and 5 Alloy. Retry restores Life and flasks without repeating rewards.\n\nAfter a clear, walk into the gate or use Continue. Equip reward & continue saves your new item before the next room. Pause opens the reward comparison or a return to Hearth.\n\nVisit Hearth to forge, train skills or respec freely. Your next-room checkpoint survives quitting. Standard characters start at level 1; choose Warden, Threadseer or Revenant. The four-region campaign ends with the First Pattern. Older court checkpoints remain resumable. Training profiles use separate sandbox resources.")
        };
        var page = pages[tutorialPage];
        ClearMenu($"FIELD GUIDE / {tutorialPage + 1} OF {pages.Length}", page.Title, "Learn at your pace. Room hints repeat the essentials.");
        Text(page.Body, 18, ink);
        if (tutorialPage < pages.Length - 1) AddButton("Next", () => { tutorialPage++; Tutorial(); }, true);
        else AddButton("Return to Hearth", arena.ReturnToTitle, true);
        if (tutorialPage > 0) AddButton("Previous", () => { tutorialPage--; Tutorial(); });
        AddButton("Back", () => { if (arena.Playing) Pause(); else Title(); });
    }
    private void Settings()
    {
        ClearMenu("DISPLAY / AUDIO / ACCESSIBILITY", "Settings", "Changes apply immediately. Audio and readability choices are saved.");
        foreach (var channel in SliceAudio.Channels)
        {
            var row = new HBoxContainer(); options.AddChild(row);
            var label = new Label { Text = $"{channel}  {arena.Audio.Volume(channel)}%", CustomMinimumSize = new Vector2(330, 35) }; row.AddChild(label);
            foreach (var step in new[] { -10, 10 })
            {
                var button = new Button { Text = step < 0 ? "−10" : "+10", CustomMinimumSize = new Vector2(130, 38) };
                button.Pressed += () => { arena.Audio.SetVolume(channel, Math.Clamp(arena.Audio.Volume(channel) + step, 0, 100)); label.Text = $"{channel}  {arena.Audio.Volume(channel)}%"; }; row.AddChild(button);
                if (channel == "Master" && step == -10) button.GrabFocus();
            }
        }
        AddButton("Toggle fullscreen / windowed", () => DisplayServer.WindowSetMode(DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Fullscreen ? DisplayServer.WindowMode.Windowed : DisplayServer.WindowMode.Fullscreen));
        AddButton(arena.Audio.ReducedFlash ? "Hit flashes: reduced" : "Hit flashes: normal", () => { arena.Audio.ToggleFlash(); Settings(); });
        AddButton($"Text size: {arena.Audio.TextPercent}%", () => { arena.Audio.ToggleText(); Settings(); });
        AddButton(arena.Audio.HighContrast ? "Contrast: high" : "Contrast: standard", () => { arena.Audio.ToggleContrast(); Settings(); });
        AddButton(arena.Audio.ShowTutorialHints ? "Room guidance: shown" : "Room guidance: hidden", () => { arena.Audio.ToggleHints(); Settings(); });
        AddButton("Back", () => { if (arena.Playing) Pause(); else Title(); });
    }
}
