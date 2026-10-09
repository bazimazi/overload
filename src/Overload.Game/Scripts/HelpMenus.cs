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
            ("Your first adventure", "You begin in Hearth, a safe town. Speak to Mara (gold marker, G to interact), then follow the west road to Cinderroad. Defeat its first patrol, pick up the glowing weapon with G, and equip it in your inventory [I]. Your first level grants skill and talent points [K].\n\nYour quest tracker gives the next step. J opens the full journal. Tab shows local terrain; M shows the world. Gold routes guide you around walls. Town services are available in Hearth.\n\nPress P outside town to open a portal. Stand still for 3 seconds; attacks, movement and damage interrupt it. Press P in Hearth to return to the same place. Loot stays on the ground across travel and saves. Story difficulty is available in the Hearth quest journal."),
            ("Move. Aim. Commit.", "Left click ground to walk, an enemy to approach and attack, or a landmark to interact. Hold left mouse to keep attacking nearby enemies; drag over another enemy to switch targets. Alt + left mouse attacks toward your cursor while standing your ground. WASD / left stick also moves; keyboard or stick movement cancels mouse travel. Right click walks and steers without selecting targets. RB uses your Frame's free basic attack on a controller.\n\nAmber shapes mark a locked attack. Step out, then strike during recovery. Space / A and your healing flask can interrupt a basic attack immediately.\nHold Q, E and R / X, Y and B to repeat your equipped skills when ready. Basic hits restore 8 Focus and build Surge. Three basic hits empower the next damaging skill by 50%; land another basic hit or use it within 6 seconds. Skills spend Focus, or reserve Life with Red Covenant. Focus also returns automatically. Life and flasks refill at Hearth and safe refuges."),
            ("A memory of motion", "Travel 3 meters within 2 seconds while engaged to store Momentum. It lasts 5 seconds.\n\nWith Pursuit bound, your next attack becomes a lunge. With Crossing bound, Traverse spends the same memory on a long passage through enemies. Walls still stop you.\n\nHold Shift / left trigger while acting to preserve your memories and use the base action. Release it when you want to spend them. Override still applies. Try both in Learn to rewrite at Hearth."),
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
            var label = new Label { Text = channel, CustomMinimumSize = new Vector2(140, 35) };row.AddChild(label);
            var slider=new HSlider {MinValue=0,MaxValue=100,Step=5,Value=arena.Audio.Volume(channel),SizeFlagsHorizontal=SizeFlags.ExpandFill,FocusMode=FocusModeEnum.All,CustomMinimumSize=new(240,35),AccessibilityName=channel+" volume"};row.AddChild(slider);
            var value=new Label {Text=arena.Audio.Volume(channel)+"%",CustomMinimumSize=new(55,35)};row.AddChild(value);
            slider.ValueChanged+=volume=>{arena.Audio.SetVolume(channel,(int)volume);value.Text=(int)volume+"%";};
            if(channel=="Master")slider.GrabFocus();
        }
        AddButton("Toggle fullscreen / windowed", () => DisplayServer.WindowSetMode(DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Fullscreen ? DisplayServer.WindowMode.Windowed : DisplayServer.WindowMode.Fullscreen));
        AddButton(arena.Audio.ReducedFlash ? "Hit flashes: reduced" : "Hit flashes: normal", () => { arena.Audio.ToggleFlash(); Settings(); });
        AddButton($"Text size: {arena.Audio.TextPercent}%", () => { arena.Audio.ToggleText(); Settings(); });
        AddButton(arena.Audio.HighContrast ? "Contrast: high" : "Contrast: standard", () => { arena.Audio.ToggleContrast(); Settings(); });
        AddButton(arena.Audio.ShowTutorialHints ? "Room guidance: shown" : "Room guidance: hidden", () => { arena.Audio.ToggleHints(); Settings(); });
        AddButton("Back", () => { if (arena.Playing) Pause(); else Title(); });
    }
}
