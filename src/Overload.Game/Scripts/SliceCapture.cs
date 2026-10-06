using Godot;

namespace Overload.Game;
public partial class Arena
{
    private async void CaptureSlice()
    {
        if (DisplayServer.GetName() == "headless") { GetTree().Quit(1); return; }
        Audio.StartLoops();
        var directory = System.IO.Path.Combine(ProjectSettings.GlobalizePath("res://"), "../../artifacts/screenshots");
        System.IO.Directory.CreateDirectory(directory);
        async Task Capture(string name)
        {
            for (var i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory, "slice-" + name + ".png"));
        }
        await Capture("hearth");
        Hud.Bindings(); await Capture("bindings");
        Hud.CaptureMenu("inventory"); await Capture("inventory");
        Hud.CaptureMenu("character"); await Capture("character");
        Hud.CaptureMenu("settings"); await Capture("settings");
        Hud.CaptureMenu("tutorial"); await Capture("tutorial");
        Hud.Title(); StartJourney(); await Capture("threshold");
        ReturnToTitle(); StartEncounter(3); await Capture("boss");
        TogglePause(); await Capture("pause");
        GD.Print("OVERLOAD_SLICE_CAPTURE_OK"); QuitGame();
    }
}
public partial class ArenaHud
{
    public void CaptureMenu(string name)
    {
        switch (name) { case "inventory": Inventory(); break; case "character": CharacterMenu(); break; case "settings": Settings(); break; case "tutorial": Tutorial(); break; }
    }
}
