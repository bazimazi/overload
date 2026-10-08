using Godot;
using Overload.Domain;

namespace Overload.Game;

public partial class Arena
{
    private async void ReviewWorldSeeds()
    {
        if(DisplayServer.GetName()=="headless"){GD.PushError("Seed review requires a renderer");QuitGame(1);return;}
        try
        {
            UpdateCharacter(s=>WorldRules.Enroll(FrameRules.Create(FrameId.Warden)) with {CharacterId=s.CharacterId,Revision=s.Revision});
            var output=System.IO.Path.Combine(ProjectSettings.GlobalizePath("res://"),"../../artifacts/world");Directory.CreateDirectory(output);
            foreach(var seed in new ulong[]{30,69})
            {
                WorldActive=false;StartEncounter(0);ClearEncounterEnemies();
                var map=FractureMapGenerator.Generate(seed,(Region)(seed%4),(ActivityFamily)(seed%3));world.Configure(map.Zone);
                WorldActive=true;Paused=true;world.Progress=null;world.MapClaimed=_=>false;world.MapRequired=map.Required.Contains;
                liveWorldFog=[..Enumerable.Range(0,280)];LocalMapVisible=true;Player.Position=V(map.Zone.Arrival);Player.TeleportVisual();ArrivalTime=0;Hud.HideMenu();
                for(var frame=0;frame<3;frame++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
                GD.Print($"WORLD SEED SNAPSHOT seed={seed} fog={liveWorldFog.Count} blocks={map.Zone.Geometry.Blocks.Length} paused={Paused}");
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                using(var image=GetViewport().GetTexture().GetImage())
                {
                    var b=map.Zone.Geometry.Bounds;var scale=Math.Min((Hud.Size.X-148)/b.Width,(Hud.Size.Y-320)/b.Height);
                    var origin=new Vector2(Hud.Size.X/2,(Hud.Size.Y-30)/2)-new Vector2(b.Width,b.Height)*scale/2+new Vector2(0,12);
                    for(var node=0;node<9;node++)
                    {
                        var center=FractureMapGenerator.Center(node);var pixel=origin+V(center)*scale;
                        if(!world.Navigation.Clear(center.Vector,center.Vector,20)||image.GetPixel((int)pixel.X,(int)pixel.Y).G<.2f)
                            throw new InvalidOperationException($"Map raster hides walkable room {node} for seed {seed}");
                    }
                    image.SavePng(System.IO.Path.Combine(output,$"seed-{seed}-map.png"));
                }
                LocalMapVisible=false;var boss=map.Zone.Encounters.Single(e=>e.Boss>=0);Player.Position=V(boss.Position)-new Vector2(160,0);Player.TeleportVisual();Spawn(RegionalContent.Boss(map.Zone.Region,boss.Boss),V(boss.Position));
                await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                using(var image=GetViewport().GetTexture().GetImage())image.SavePng(System.IO.Path.Combine(output,$"seed-{seed}-boss.png"));
            }
            WorldActive=false;GD.Print("OVERLOAD_WORLD_SEED_REVIEW_OK seeds=30,69 shortest/longest physical layouts");QuitGame();
        }
        catch(Exception e){GD.PushError("WORLD SEED REVIEW FAIL: "+e);QuitGame(1);}
    }
}
