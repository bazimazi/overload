using Godot;
using Overload.Domain;
using System.Text.Json;

namespace Overload.Game;

public partial class Arena
{
    private async void ReviewOpenWorld()
    {
        var rendered=OS.GetCmdlineUserArgs().Contains("--open-world-review");var checks=0;
        var output=System.IO.Path.Combine(ProjectSettings.GlobalizePath("res://"),"../../artifacts/open-world");Directory.CreateDirectory(output);
        void Check(bool value,string label){if(!value)throw new InvalidOperationException(label);checks++;GD.Print("OPEN WORLD PASS: "+label);}
        async Task Frames(int n){for(var i=0;i<n;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);}
        void KeyEvent(Key key,bool down){Input.ParseInputEvent(new InputEventKey{Pressed=down,Keycode=key,PhysicalKeycode=key});Input.FlushBufferedEvents();}
        async Task Shot(string name)
        {
            await Frames(8);if(!rendered)return;
            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            using var image=GetViewport().GetTexture().GetImage();image.SavePng(System.IO.Path.Combine(output,name+".png"));
        }
        System.Numerics.BigInteger? routeFixtureXp=null;
        void ClearDefenders(){
            // This fixture verifies routes and persistence; AdventureReview separately measures normal combat.
            if(routeFixtureXp!=Character!.State.TotalXp){routeFixtureXp=Character.State.TotalXp;PlayerState.ChangeMaximumLife(System.Numerics.BigInteger.Pow(10,24));PlayerState.Reset();}
            foreach(var actor in Enemies)if(!actor.Enemy!.Dead)actor.Enemy.ReceiveHit(actor.Enemy.MaximumLife,0,PlayerState.Tick);}
        async Task Walk(WorldPoint destination)
        {
            var from=new System.Numerics.Vector2(Player.Position.X,Player.Position.Y);var path=WorldNavigation.FindPath(from,destination.Vector,Player.Radius);
            Check(path.Length>0,"physical route exists to "+destination);
            foreach(var p in path)
            {
                var target=new Vector2(p.X,p.Y);var limit=0;
                while(Player.Position.DistanceTo(target)>2&&limit++<1400)
                {ClearDefenders();Player.MoveAndCollide(Player.Position.DirectionTo(target)*Math.Min(18,Player.Position.DistanceTo(target)));Player.TeleportVisual();await Frames(1);}
                Check(Player.Position.DistanceTo(target)<=2,"collision body traversed outdoor route");
            }
            ClearDefenders();await Frames(4);
        }
        async Task Exit(string id)
        {
            var link=WorldZone.Exits.Single(e=>e.Id==id);await Walk(link.Position);InteractWorld();await Frames(3);
            Check(WorldZone.Id==link.Destination,"connected road enters "+link.Destination);
            PlayerState.ChangeMaximumLife(System.Numerics.BigInteger.Pow(10,24));PlayerState.Reset();
        }
        try
        {
            if(!rendered){GetWindow().Size=new(1280,720);await Frames(3);}
            ReturnToTitle();Check(UpdateCharacter(s=>WorldRules.Enroll(FrameRules.Create(FrameId.Warden)) with {CharacterId=s.CharacterId}),"isolated fresh level-1 world character");
            StartWorld();await Frames(5);Check(WorldActive&&WorldZone.Id=="hearth"&&!Hud.MenuVisible,"play begins inside the starting zone");
            Check(WorldFog.Count==14*8,"starting junction is charted");
            Check(KnownWorldTargets().Any(t=>t.Id=="exit:frontier"),"frontier road available from level one");await Shot("starting-zone-minimap");
            KeyEvent(Key.Tab,true);await Frames(2);KeyEvent(Key.Tab,false);
            var tick=PlayerState.Tick;await Frames(10);Check(LocalMapVisible&&PlayerState.Tick==tick,"local map freezes simulation");await Shot("starting-zone-map");
            var zoom=Hud.LocalMapZoom;
            var wheelPoint=Hud.MapPointToScreen(new(640,560));
            Input.ParseInputEvent(new InputEventMouseButton{Pressed=true,ButtonIndex=MouseButton.WheelUp,Position=wheelPoint,GlobalPosition=wheelPoint});Input.FlushBufferedEvents();await Frames(2);
            Check(Hud.LocalMapZoom>zoom,"real wheel event zooms the map");
            var panPoint=Hud.MapPointToScreen(new(640,560));
            Input.ParseInputEvent(new InputEventMouseButton{Pressed=true,ButtonIndex=MouseButton.Middle,Position=panPoint,GlobalPosition=panPoint});Input.FlushBufferedEvents();
            Input.ParseInputEvent(new InputEventMouseMotion{Position=panPoint+new Vector2(24,12),GlobalPosition=panPoint+new Vector2(24,12),Relative=new(24,12)});Input.FlushBufferedEvents();
            Input.ParseInputEvent(new InputEventMouseButton{Pressed=false,ButtonIndex=MouseButton.Middle,Position=panPoint+new Vector2(24,12)});Input.FlushBufferedEvents();await Frames(2);
            Check(Hud.MapPointToScreen(new(640,560)).DistanceTo(panPoint)>10,"real middle drag pans the map");
            var point=Hud.MapPointToScreen(new(640,560));
            Input.ParseInputEvent(new InputEventMouseButton{Pressed=true,ButtonIndex=MouseButton.Left,Position=point});Input.FlushBufferedEvents();await Frames(2);
            Check(WorldDestination?.Id=="map.pin","real map click pins explored ground");
            Input.ParseInputEvent(new InputEventMouseButton{Pressed=false,ButtonIndex=MouseButton.Left,Position=point});Input.FlushBufferedEvents();
            point=Hud.MapPointToScreen(new(680,580));
            Input.ParseInputEvent(new InputEventMouseButton{Pressed=true,ButtonIndex=MouseButton.Right,Position=point});Input.FlushBufferedEvents();await Frames(2);
            Check(!LocalMapVisible&&PointerDestination is not null,"right click closes map and navigates the character");
            Input.ParseInputEvent(new InputEventMouseButton{Pressed=false,ButtonIndex=MouseButton.Right,Position=point});Input.FlushBufferedEvents();
            var before=Player.Position;await Frames(45);Check(Player.Position.DistanceTo(before)>40,"map navigation moves the collision body");ResetPointerTravel();
            OpenRegionalMap();await Shot("world-atlas-start");Check(Paused&&Hud.MenuVisible,"world atlas is accessible in the starting zone");
            KeyEvent(Key.M,true);await Frames(2);KeyEvent(Key.M,false);Check(!Paused&&!Hud.MenuVisible,"M toggles the atlas closed");
            var savedPosition=Player.Position;ReturnToTitle();ReloadCharacterForCheck();StartWorld();await Frames(2);
            Check(Player.Position.DistanceTo(savedPosition)<1,"world position survives real disk reload");
            await Exit("frontier");Check(FrontierActive&&WorldZone.Geometry.Bounds.Width==6144,"massive frontier is physically entered");
            Check(Enemies.Count(e=>!e.Enemy!.Dead)<=16,"outdoor actor activation stays bounded");
            Check(!PinWorldLocation(new(5300,850)),"unexplored ground cannot be pinned");await Shot("frontier-arrival");
            await Walk(WorldZone.Sites.Single(s=>s.Kind=="waypoint").Position);InteractWorld();await Frames(3);
            Check(Character!.State.World!.Frontier.LastCamp==0,"camp commits a frontier checkpoint");await Shot("frontier-camp-minimap");
            ToggleLocalMap();await Shot("frontier-route-map");ToggleLocalMap();
            OpenRegionalMap();await Shot("world-atlas-frontier");CloseWorldMenu();
            var first=WorldZone.Geometry.Blocks.ToArray();await Exit("onward");Check(Character.State.World!.Frontier.Farthest==1,"east trail extends the world");
            Check(!first.SequenceEqual(WorldZone.Geometry.Blocks),"next reach has distinct deterministic terrain");await Shot("second-reach");
            await Exit("back");Check(first.SequenceEqual(WorldZone.Geometry.Blocks),"west trail restores the same geography");
            TravelWaypoint("hearth.waypoint");await Frames(3);Check(WorldZone.Id=="hearth","world waypoint returns home");
            TravelWaypoint("frontier.0.camp");await Frames(3);Check(WorldZone.Id=="frontier.0","activated frontier camp permits return travel");
            if(rendered)
            {
                foreach(var size in new[]{new Vector2I(960,540),new(1280,720),new(1920,1080)})
                {
                    DisplayServer.WindowSetSize(size);await Frames(5);
                    foreach(var text in new[]{100,125})
                    {
                        if(Audio.TextPercent!=text)Audio.ToggleText();ToggleLocalMap();await Shot($"frontier-map-{size.X}-{text}");ToggleLocalMap();
                        OpenRegionalMap();await Shot($"world-atlas-{size.X}-{text}");Hud.CheckQualityLayout();CloseWorldMenu();
                    }
                }
                DisplayServer.WindowSetSize(new(1280,720));if(Audio.TextPercent!=100)Audio.ToggleText();
                // Full terrain is an explicitly labelled review fixture; normal gameplay retains discovery fog.
                liveWorldFog=[..Enumerable.Range(0,64*40)];ToggleLocalMap();await Shot("frontier-entire-terrain-fixture");ToggleLocalMap();
            }
            ReturnToTitle();ReloadCharacterForCheck();Check(Character.State.World!.Frontier.Farthest==1,"frontier progression survives reload");
            // A completed campaign has already earned its introductory milestones and enforcer receipt.
            Check(UpdateCharacter(s=>s with {FractureUnlocked=true,World=s.World! with {Resolved=[..Enum.GetValues<Region>()],Ending="preserve",
                Claims=s.World.Claims.Add("ash.1.boss"),Adventure=null}}),"isolated completed-campaign fixture");
            StartWorld();TravelWaypoint("hearth.waypoint");await Frames(3);
            Check(WorldDestination?.Id=="exit:frontier","completed campaign guides to continuing wilderness");ReturnToTitle();
            File.WriteAllText(System.IO.Path.Combine(output,rendered?"review.json":"smoke.json"),JsonSerializer.Serialize(new {checks,rendered,build="open-world.2026-10-09",campaignOutdoor=new[]{5760,3456},frontier=new[]{6144,3840},note="Isolated profiles, real input and collision walks; fixture defeats do not certify human combat pacing."},new JsonSerializerOptions{WriteIndented=true}));
            GD.Print($"OVERLOAD_OPEN_WORLD_OK checks={checks} rendered={rendered}");QuitGame();
        }
        catch(Exception e){GD.PushError("OPEN WORLD REVIEW: "+e);QuitGame(1);}
    }
}
