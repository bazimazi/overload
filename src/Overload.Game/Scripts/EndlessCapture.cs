using Godot;
using Overload.Domain;
using BigInteger = System.Numerics.BigInteger;

namespace Overload.Game;
public partial class Arena
{
    private async void CaptureEndless()
    {
        if(DisplayServer.GetName()=="headless") { QuitGame(1);return; }
        var directory=System.IO.Path.Combine(ProjectSettings.GlobalizePath("res://"),"../../artifacts/screenshots");System.IO.Directory.CreateDirectory(directory);
        Audio.StartLoops();
        async Task Shot(string name)
        {
            for(var i=0;i<3;i++) await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory,"endless-"+name+".png"));
        }
        void Profile(CharacterState profile) { ReturnToTitle();UpdateCharacter(s=>profile with { CharacterId=s.CharacterId }); }
        Profile(EndlessFixtures.Reference(10));Hud.CaptureFractureView("board",10);await Shot("board");
        var chapter=EndlessRules.Begin(EndlessFixtures.Reference(10),10,Guid.NewGuid());
        for(var i=0;i<7;i++) chapter=EndlessRules.Claim(chapter,chapter.Fracture!.Id,chapter.Fracture.Sequence,i);
        Profile(chapter);Hud.CaptureFractureView("board",10);await Shot("chapter");
        Hud.CaptureFractureView("forge",10);await Shot("forge");
        foreach(var tier in new[] {9,10,11})
        {
            var s=EndlessRules.Begin(EndlessFixtures.Reference(tier),tier,Guid.NewGuid());
            for(var i=0;i<6;i++) s=EndlessRules.Claim(s,s.Fracture!.Id,s.Fracture.Sequence,i);
            Profile(s);StartFracture(tier);await Shot("tier-"+tier);
        }
        var large=BigInteger.Pow(10,30);Profile(EndlessFixtures.Reference(large));Hud.CaptureFractureView("board",large);await Shot("large-tier");
        GD.Print("OVERLOAD_ENDLESS_CAPTURE_OK");QuitGame();
    }
}
public partial class ArenaHud
{
    public void CaptureFractureView(string view,BigInteger tier)
    { selectedTier=tier;if(view=="forge") Attunement();else Fractures(); }
}
