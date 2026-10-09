using System.Text.Json;
using Godot;
using Overload.Domain;

namespace Overload.Game;

public partial class Arena
{
    private async void ReviewGraphics()
    {
        var rendered=OS.GetCmdlineUserArgs().Contains("--graphics-review");var checks=0;var captures=new List<string>();
        var output=System.IO.Path.Combine(ProjectSettings.GlobalizePath("res://"),"../../artifacts/graphics");Directory.CreateDirectory(output);
        void Check(bool valid,string label){if(!valid)throw new InvalidOperationException(label);checks++;GD.Print("GRAPHICS PASS: "+label);}
        async Task Frames(int n){for(var i=0;i<n;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);}
        async Task Capture(string name)
        {
            if(!rendered)return;
            await Frames(3);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            using var image=GetViewport().GetTexture().GetImage();Check(image.SavePng(System.IO.Path.Combine(output,name+".png"))==Error.Ok,"capture "+name);captures.Add(name+".png");
        }
        async Task Enter(string id,WorldPoint arrival)
        {
            Check(WorldTransaction("graphics:"+Guid.NewGuid(),s=>WorldRules.Enter(s,id,arrival)),"stage "+id);
            EnterWorldZone(true);PlayerState.ChangeMaximumLife(System.Numerics.BigInteger.Pow(10,24));PlayerState.Reset();await Frames(40);
        }
        try
        {
            if(rendered)Check(DisplayServer.GetName()!="headless","native graphics renderer");
            Check(IsSmoke,"isolated visual review profile");
            ReturnToTitle();Check(UpdateCharacter(s=>WorldRules.Enroll(FrameRules.Create(FrameId.Warden)) with {CharacterId=s.CharacterId}),"fresh world fixture");
            if(Audio.TextPercent!=100)Audio.ToggleText();StartWorld();await Frames(40);await Capture("hearth");
            Player.Position=new(370,410);Player.TeleportVisual();await Frames(30);await Capture("hearth-mara");
            foreach(var region in Enum.GetValues<Region>())
            {
                var z=WorldContent.Zone(WorldContent.Id(region,0));var b=z.Geometry.Blocks[0];
                await Enter(z.Id,new(b.X+b.Width/2,b.Y-60));
                var structure=world.GetChildren().OfType<WorldStructure>().First(s=>s.Footprint.Position==new Vector2(b.X,b.Y));
                Check(structure.SelfModulate.A<.46f,"solid canopy reveals the player "+region);await Capture(region+"-canopy");
                Player.Position=new(b.X+b.Width/2,b.Y+b.Height+90);Player.TeleportVisual();await Frames(50);
                Check(structure.SelfModulate.A>.98f,"solid canopy returns opaque "+region);await Capture(region+"-wilderness");
                Check(world.PavedRoads.Any(p=>p.Length>1)&&world.PavedRoads.All(p=>Enumerable.Range(1,Math.Max(0,p.Length-1)).All(i=>WorldNavigation.Clear(new(p[i-1].X,p[i-1].Y),new(p[i].X,p[i].Y),36))),"textured paths stay on clear navigation "+region);
                z=WorldContent.Zone(WorldContent.Id(region,2));b=z.Geometry.Blocks[0];
                await Enter(z.Id,new(Math.Max(35,b.X-70),b.Y+b.Height/2));await Capture(region+"-dungeon");
                z=WorldContent.Zone(WorldContent.Id(region,1));await Enter(z.Id,new(470,384));await Capture(region+"-boss-tell");
            }
            await Enter("hearth",new(640,384));Check(WorldTransaction("graphics.frontier:"+Guid.NewGuid(),s=>WorldRules.Travel(s,"frontier")),"frontier entry");
            EnterWorldZone(true);PlayerState.ChangeMaximumLife(System.Numerics.BigInteger.Pow(10,24));PlayerState.Reset();Player.Position=new(2900,1920);Player.TeleportVisual();await Frames(40);await Capture("frontier");
            if(rendered)
            {
                foreach(var size in new[]{new Vector2I(960,540),new(1280,720),new(1920,1080)})
                {
                    DisplayServer.WindowSetSize(size);await Frames(8);await Capture("world-"+size.X);
                    var viewport=(SubViewport)world.GetViewport();
                    var actual=GetViewport().GetScreenTransform()*(worldContainer.GetGlobalTransform()*(viewport.CanvasTransform*Player.VisualPosition));
                    Check(actual.DistanceTo(WorldToWindow(Player.VisualPosition))<1,"camera and pointer projection agree at "+size.X);
                    var aim=Player.VisualPosition+new Vector2(33,-21);
                    Controls.Observe(new InputEventMouseMotion {Position=worldContainer.Position+(aim-CameraOrigin)*WorldScale});
                    Check(CursorWorldPosition.DistanceTo(aim)<.01f,"higher resolution preserves mouse aim at "+size.X);
                    LocalMapVisible=true;await Capture("map-"+size.X);LocalMapVisible=false;
                }
            }
            File.WriteAllText(System.IO.Path.Combine(output,rendered?"review.json":"smoke.json"),JsonSerializer.Serialize(new{build=BuildId,checks,rendered,captures,fixture="Isolated staging with protected life for captures. No gameplay or human acceptance claim."},new JsonSerializerOptions{WriteIndented=true}));
            GD.Print("OVERLOAD_GRAPHICS_OK checks="+checks);QuitGame();
        }
        catch(Exception e){GD.PushError(e.ToString());QuitGame(1);}
    }
}
