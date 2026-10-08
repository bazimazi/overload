using Godot;
using Overload.Domain;
using System.Diagnostics;
using System.Text.Json;

namespace Overload.Game;

public partial class Arena
{
    private async void RunWorldSmoke()
    {
        var checks=0;var watch=Stopwatch.StartNew();var captures=OS.GetCmdlineUserArgs().Contains("--world-review");
        var output=System.IO.Path.Combine(ProjectSettings.GlobalizePath("res://"),"../../artifacts/world");Directory.CreateDirectory(output);
        void Check(bool value,string label){if(!value)throw new InvalidOperationException(label);checks++;GD.Print("WORLD PASS: "+label);}
        async Task Frames(int count){for(var i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);}
        void ClearDefenders(){foreach(var e in Enemies.ToArray())e.Enemy!.ReceiveHit(e.Enemy.MaximumLife,0,PlayerState.Tick);}
        async Task Capture(string name)
        {if(!captures)return;await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);using var image=GetViewport().GetTexture().GetImage();image.SavePng(System.IO.Path.Combine(output,name+".png"));}
        async Task Walk(WorldPoint destination)
        {
            var path=world.Navigation.FindPath(new(Player.Position.X,Player.Position.Y),destination.Vector,Player.Radius);
            Check(path.Length>0,"physical path to "+destination);
            foreach(var point in path)
            {
                var target=V(point);var guard=0;
                while(Player.Position.DistanceTo(target)>2&&guard++<1200)
                {
                    ClearDefenders();Player.MoveAndCollide(Player.Position.DirectionTo(target)*Math.Min(12,Player.Position.DistanceTo(target)));Player.TeleportVisual();
                    await Frames(1);
                    if(PlayerState.Dead)throw new InvalidOperationException("Route fixture died");
                }
                Check(Player.Position.DistanceTo(target)<=2,"collision body reached navigation waypoint");
            }
            ClearDefenders();await Frames(2);Check(Player.Position.DistanceTo(V(destination))<3,"walk reached actual anchor");
        }
        async Task Exit(string id)
        {
            var previous=WorldZone.Id;var e=WorldZone.Exits.Single(e=>e.Id==id);await Walk(e.Position);InteractWorld();await Frames(3);
            Check(WorldZone.Id==e.Destination,"physical exit "+previous+" -> "+e.Destination);PlayerState.ChangeMaximumLife(System.Numerics.BigInteger.Pow(10,20));PlayerState.Reset();
        }
        try
        {
            foreach(var frame in Enum.GetValues<FrameId>())
            {
                ReturnToTitle();Check(UpdateCharacter(s=>WorldRules.Enroll(FrameRules.Create(frame)) with {CharacterId=s.CharacterId,Revision=s.Revision}),"isolated world profile "+frame);
                StartWorld();await Frames(10);Check(WorldActive&&WorldZone.Id=="hearth","walkable Hearth "+frame);
                Input.ParseInputEvent(new InputEventKey {Pressed=true,Keycode=Key.Tab,PhysicalKeycode=Key.Tab});await Frames(2);
                Check(LocalMapVisible,"Tab opens the actual local overlay");Input.ParseInputEvent(new InputEventKey {Pressed=false,Keycode=Key.Tab,PhysicalKeycode=Key.Tab});LocalMapVisible=false;
                Input.ParseInputEvent(new InputEventKey {Pressed=true,Keycode=Key.M,PhysicalKeycode=Key.M});await Frames(2);
                Check(Paused&&Hud.MenuVisible,"M opens the regional travel menu");Input.ParseInputEvent(new InputEventKey {Pressed=false,Keycode=Key.M,PhysicalKeycode=Key.M});
                var pausedTick=PlayerState.Tick;await Frames(3);Check(PlayerState.Tick==pausedTick,"map menu freezes the encounter clock");
                Input.ParseInputEvent(new InputEventJoypadButton {Pressed=true,ButtonIndex=JoyButton.B,Device=0});await Frames(2);
                Check(!Paused&&!Hud.MenuVisible,"controller Back returns to exploration");Check(Controls.Glyph("local_map")=="VIEW"&&Controls.Glyph("region_map")=="D-PAD RIGHT","controller map hints use the actual bindings");Input.ParseInputEvent(new InputEventJoypadButton {Pressed=false,ButtonIndex=JoyButton.B,Device=0});Controls.Disconnected();
                Check(Controls.Glyph("local_map")!="LMB"&&InputMap.ActionGetEvents("region_map").OfType<InputEventJoypadButton>().Any(),"keyboard and controller map bindings");
                foreach(var region in Enum.GetValues<Region>())
                {
                    if(WorldZone.Id!="hearth"){ReturnToTitle();StartWorld();await Frames(3);}
                    // Activated approach checkpoints may be outside Hearth; the safe travel API returns home.
                    if(WorldZone.Id!="hearth"){TravelWaypoint("hearth.waypoint");await Frames(3);}
                    await Exit("road."+(int)region);
                    if(captures){await Walk(new(1190,550));ArrivalTime=0;await Capture(frame+"-"+region+"-landmark");}
                    Check(Enemies.Count(e=>!e.Enemy!.Dead)<=12,"bounded world pack activation");
                    if(region==Region.Ash)
                    {
                        await Walk(new(610,290));await Walk(new(1260,290));await Capture(frame+"-gantry");
                        await Walk(new(620,850));await Walk(new(1210,920));await Walk(new(980,940));await Walk(new(940,1020));InteractWorld();await Frames(3);
                        Check(Character!.State.World!.Waypoints.Contains("ash.0.refuge"),"atomic refuge, waypoint and shortcut "+frame);await Capture(frame+"-refuge");
                    }
                    LocalMapVisible=true;await Capture(frame+"-"+region+"-local-map");LocalMapVisible=false;
                    await Exit("enforcer");await Walk(new(650,384));await Frames(3);ClearDefenders();await Frames(3);
                    Check(Character!.State.World!.Claims.Contains(WorldContent.Id(region,1)+".boss"),"regional enforcer receipt "+frame+" "+region);
                    await Exit("return");await Exit("dungeon");
                    foreach(var site in WorldZone.Sites.Where(s=>s.Kind=="device").ToArray())
                    {await Walk(WorldZone.Encounters.Single(e=>e.Id==site.Requires).Position);await Walk(site.Position);InteractWorld();await Frames(3);Check(Character.State.World!.Claims.Contains(site.Id),"physical conduit interaction "+site.Id);}
                    await Capture(frame+"-"+region+"-dungeon");await Exit("ruler");await Walk(new(650,384));ClearDefenders();await Frames(3);
                    Check(Character.State.World!.Resolved.Contains(region),"ruler changes regional world phase "+frame+" "+region);
                    if(Paused)
                    {
                        Check(Hud.MenuVisible,"ending choice is presented");TogglePause();Check(Paused&&Hud.MenuVisible,"pause cannot discard the pending ending");
                        ReturnToTitle();StartWorld();Check(Paused&&Hud.MenuVisible,"unselected ending is restored at the safe checkpoint");ChooseEnding("preserve");
                    }
                    await Capture(frame+"-"+region+"-resolution");ReturnToTitle();StartWorld();TravelWaypoint("hearth.waypoint");await Frames(3);
                }
                Check(Character!.State.FractureUnlocked&&Character.State.World!.Resolved.Count==4,"complete connected campaign "+frame);
                Check(Character.State.Proofs.Count==0&&Character.State.Masteries.Count==0&&Character.State.Seals.All(v=>v==0),"campaign preserves earned oath authority "+frame);
                ReturnToTitle();ReloadCharacterForCheck();Check(Character.State.World!.Ending=="preserve"&&Character.State.World.Fog.Count>4,"world, fog and ending survive disk reload "+frame);
            }
            foreach(var family in Enum.GetValues<ActivityFamily>())
            {
                ReturnToTitle();StartRegional(1,Region.Ash,family);await Frames(3);Check(GraphFracture&&Character!.State.Fracture!.Map is not null,"new physical Fracture "+family);
                var map=Character!.State.Fracture!.Map!;Check(EndgameHint==map.Zone.Objective,"live hint matches the activity objectives "+family);
                foreach(var site in map.Zone.Sites.Where(s=>s.Kind=="cache").ToArray())
                {await Walk(map.Zone.Encounters.Single(e=>e.Id==site.Requires).Position);await Walk(site.Position);InteractWorld();await Frames(2);}
                foreach(var e in map.Zone.Encounters.Where(e=>e.Id!="map.boss").ToArray())await Walk(e.Position);
                foreach(var site in map.Zone.Sites.Where(s=>s.Kind=="device"&&s.Id!="map.keystone").ToArray())
                {await Walk(site.Position);InteractWorld();await Frames(2);}
                LocalMapVisible=true;await Capture("fracture-"+family+"-map");LocalMapVisible=false;
                await Walk(map.Zone.Encounters.Single(e=>e.Id=="map.boss").Position);ClearDefenders();await Frames(3);
                if(family==ActivityFamily.Vault){await Walk(map.Zone.Sites.Single(s=>s.Id=="map.keystone").Position);InteractWorld();await Frames(2);}
                Check(Character.State.Fracture!.Completed,"objective graph completes "+family);await Capture("fracture-"+family+"-complete");ReturnToTitle();ReloadCharacterForCheck();Check(Character.State.Fracture!.Completed,"map completion survives disk reload "+family);
            }
            StartWorld();await Frames(3);OpenRegionalMap();await Capture("regional-map");CloseWorldMenu();
            if(captures)
            {
                foreach(var size in new[]{new Vector2I(800,600),new(1280,720),new(1920,1080)})
                {
                    DisplayServer.WindowSetSize(size);
                    foreach(var text in new[]{100,125})
                    {
                        if(Audio.TextPercent!=text)Audio.ToggleText();OpenRegionalMap();await Frames(4);
                        Check(GetViewport().GuiGetFocusOwner() is Button,"map has a keyboard/controller focus target "+size+" "+text);
                        await Capture($"regional-{size.X}x{size.Y}-{text}");CloseWorldMenu();
                    }
                }
                DisplayServer.WindowSetSize(new(1280,720));if(Audio.TextPercent!=100)Audio.ToggleText();
            }
            ReturnToTitle();
            File.WriteAllText(System.IO.Path.Combine(output,captures?"engine-review.json":"engine-smoke.json"),JsonSerializer.Serialize(new {checks,seconds=watch.Elapsed.TotalSeconds,frames=3,zones=17,activities=3,captures,note="Physical collision walks and direct fixture defeats verify traversal and receipts; they do not establish human combat, comprehension or enjoyment."},new JsonSerializerOptions{WriteIndented=true}));
            GD.Print($"OVERLOAD_WORLD_SMOKE_OK checks={checks} seconds={watch.Elapsed.TotalSeconds:0.0}");QuitGame();
        }
        catch(Exception e){GD.PushError("WORLD FAIL: "+e);QuitGame(1);}
    }
}
