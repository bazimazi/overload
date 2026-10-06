using Godot;
using Overload.Domain;

namespace Overload.Game;
public partial class Arena
{
    private async void CaptureProduction()
    {
        if(DisplayServer.GetName()=="headless"){QuitGame(1);return;}
        var directory=System.IO.Path.Combine(ProjectSettings.GlobalizePath("res://"),"../../artifacts/screenshots-production");System.IO.Directory.CreateDirectory(directory);Audio.StartLoops();
        async Task Shot(string name){for(var i=0;i<3;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory,name+".png"));}
        void Profile(CharacterState fixture){ReturnToTitle();UpdateCharacter(s=>fixture with { CharacterId=s.CharacterId });}
        var reference=EndlessFixtures.Reference(21) with { Mode=ProfileMode.Standard,Seals=[0,0,0,0] };
        Profile(reference);Hud.CaptureFractureView("board",21);await Shot("board");Hud.Oaths();await Shot("trial-entry");
        foreach(var family in Enum.GetValues<ActivityFamily>()) {Profile(reference);StartRegional(21,Region.Glass,family);await Shot(family.ToString().ToLowerInvariant());}
        Profile(reference);StartTrial();await Shot("trial");
        Profile(reference);UpdateCharacter(s=>EndgameRules.StartChain(s,21,Region.Ash));Hud.CaptureFractureView("board",21);await Shot("chain");
        Profile(reference);StartSovereign(21);Player.Position=new(360,192);for(var i=0;i<300;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);await Shot("sovereign");
        GD.Print("OVERLOAD_PRODUCTION_CAPTURE_OK");QuitGame();
    }
}
