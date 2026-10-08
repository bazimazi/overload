using Godot;
using Overload.Domain;
using BigInteger = System.Numerics.BigInteger;

namespace Overload.Game;

/// <summary>Isolated rendered review: ordinary profiles and bindings are never written.</summary>
public partial class Arena
{
    private string PolishDirectory => System.IO.Path.Combine(ProjectSettings.GlobalizePath("res://"), "../../artifacts/polish");
    private async Task CapturePolishFrame(string name)
    {
        for (var i=0;i<6;i++) await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        System.IO.Directory.CreateDirectory(PolishDirectory);
        using var capture=GetViewport().GetTexture().GetImage();
        capture.SavePng(System.IO.Path.Combine(PolishDirectory,name+".png"));
    }
    private async void ReviewPolish()
    {
        try
        {
            if(DisplayServer.GetName()=="headless")throw new InvalidOperationException("Polish capture requires a renderer");
            UpdateCharacter(s=>FrameRules.BuildFixture(FrameId.Warden,0) with { CharacterId=s.CharacterId }); ReturnToTitle();
            await CapturePolishFrame("hearth"); Hud.CheckQualityLayout();
            foreach(var region in Enum.GetValues<Region>())
            {
                StartEncounter(3); ClearEncounterEnemies();
                world.SetRegion(region); world.Configure([new(300,110,28,44),new(400,245,42,28)]);
                Player.Position=new(270,195);Player.TeleportVisual();
                var boss=Spawn(RegionalContent.Boss(region,1),new(455,195));
                for(var i=0;i<72;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
                await CapturePolishFrame(region.ToString().ToLowerInvariant()+"-boss");
                ReturnToTitle();
            }
            foreach(var frame in Enum.GetValues<FrameId>())
            {
                UpdateCharacter(s=>FrameRules.BuildFixture(frame,0) with { CharacterId=s.CharacterId }); StartEncounter(2);
                world.SetRegion(frame==FrameId.Threadseer?Region.Hollow:frame==FrameId.Revenant?Region.Glass:Region.Ash);
                Player.Position=new(320,205);Player.TeleportVisual();PlayerState.ChangeMaximumLife(BigInteger.Pow(10,10));PlayerState.Reset();
                var id=frame==FrameId.Threadseer?SkillId.StormLoom:frame==FrameId.Revenant?SkillId.EchoOrder:SkillId.ShieldPulse;
                PlayerState.TryStart(id,new(1,0));
                for(var i=0;i<28;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
                await CapturePolishFrame(frame.ToString().ToLowerInvariant()+"-combat");
                ReturnToTitle();
            }
            foreach(var size in new[]{new Vector2I(960,540),new Vector2I(1280,720),new Vector2I(1920,1080),new Vector2I(2560,1440),new Vector2I(1440,900)})
            {
                DisplayServer.WindowSetSize(size);
                foreach(var text in new[]{100,125})
                {
                    if(Audio.TextPercent!=text)Audio.ToggleText();
                    ReturnToTitle(); await CapturePolishFrame($"hearth-{size.X}x{size.Y}-{text}"); Hud.CheckQualityLayout();
                    StartEncounter(2); PlayerState.ChangeMaximumLife(BigInteger.Pow(10,30)); PlayerState.Reset();
                    await CapturePolishFrame($"combat-{size.X}x{size.Y}-{text}");
                    TogglePause(); await CapturePolishFrame($"pause-{size.X}x{size.Y}-{text}");Hud.CheckQualityLayout();ReturnToTitle();
                }
            }
            GD.Print("OVERLOAD_POLISH_CAPTURE_OK 38 rendered scenes; isolated profile"); QuitGame();
        }
        catch(Exception e){GD.PushError(e.ToString());QuitGame(1);}
    }
}
