using Godot;
using Overload.Domain;

namespace Overload.Game;

public partial class Arena
{
    private async void ReviewIdentity()
    {
        var rendered=OS.GetCmdlineUserArgs().Contains("--identity-review");var checks=0;
        var output=System.IO.Path.Combine(ProjectSettings.GlobalizePath("res://"),"../../artifacts/identity");Directory.CreateDirectory(output);
        void Check(bool valid,string label){if(!valid)throw new InvalidOperationException(label);checks++;GD.Print("IDENTITY PASS: "+label);}
        async Task Frames(int n){for(var i=0;i<n;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);}
        void KeyEvent(Key key,bool down){Input.ParseInputEvent(new InputEventKey{Keycode=key,PhysicalKeycode=key,Pressed=down});Input.FlushBufferedEvents();}
        async Task Capture(string name)
        {if(!rendered)return;await Frames(3);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);using var image=GetViewport().GetTexture().GetImage();image.SavePng(System.IO.Path.Combine(output,name+".png"));}
        try
        {
            if(Audio.TextPercent!=100)Audio.ToggleText();
            if(rendered){Check(DisplayServer.GetName()!="headless","identity review has a renderer");await Task.Delay(200);}
            foreach(var frame in Enum.GetValues<FrameId>())
            {
                ReturnToTitle();Check(UpdateCharacter(s=>WorldRules.Enroll(FrameRules.Create(frame)) with{CharacterId=s.CharacterId}),"Standard "+frame+" fixture");
                var saved=Overload.Content.CharacterStore.Encode(Character!.State);
                StartRewriteLesson();await Frames(2);Player.Position=new(330,200);Player.TeleportVisual();
                Check(PlayerState.TryStart(FrameRules.Basic(frame),System.Numerics.Vector2.UnitX),"base action accepted "+frame);await Frames(55);
                Check(RewriteLessonStep==1,"real hit advances lesson "+frame);
                KeyEvent(Key.W,true);await Frames(52);KeyEvent(Key.W,false);await Frames(2);
                Check(RewriteLessonStep==2&&PlayerState.Memories.Momentum is not null,"real walking earns Momentum "+frame);
                PublishPredictions(Vector2.Right,Vector2.Zero);await Capture(frame+"-pursuit-ready");
                Check(PlayerState.TryStart(FrameRules.Basic(frame),System.Numerics.Vector2.UnitX),"Pursuit accepted "+frame);await Frames(40);
                Check(RewriteLessonStep==3,"actual overload advances lesson "+frame);
                await Frames(85);KeyEvent(Key.S,true);KeyEvent(Key.D,true);await Frames(65);KeyEvent(Key.S,false);KeyEvent(Key.D,false);await Frames(2);
                Check(PlayerState.Memories.Momentum is not null,"second memory earned "+frame);
                var token=PlayerState.Memories.Momentum;
                KeyEvent(Key.Shift,true);Input.ActionPress("cleave");await Frames(4);Input.ActionRelease("cleave");
                Check(PlayerState.Action?.Implementation==ActionImplementation.Base&&PlayerState.Memories.Momentum==token,"real preserve input keeps memory "+frame);
                await Capture(frame+"-memories-held");KeyEvent(Key.Shift,false);await Frames(40);
                var clipped=WorldQueries.CrossingLanding(Player,Player.Position,Player.Position+Vector2.Right*160,Player.Radius);
                Check(clipped.X>Player.Position.X+20&&clipped.X<612,"Crossing clips at terrain instead of rejecting a clear start "+frame);
                KeyEvent(Key.Space,true);await Frames(2);KeyEvent(Key.Space,false);await Frames(16);
                Check(RewriteLessonStep==4,"Crossing competes for same memory "+frame);
                KeyEvent(Key.G,true);await Frames(2);KeyEvent(Key.G,false);await Frames(2);
                Check(RewriteLessonStep==5&&PlayerState.ElsewhereActive,"interact replaces Traverse "+frame);
                KeyEvent(Key.Space,true);await Frames(2);KeyEvent(Key.Space,false);await Frames(12);
                Check(PlayerState.Anchor is not null&&!PlayerState.Evading,"placement makes a real anchor without evasion "+frame);
                await Capture(frame+"-override-anchor");
                Input.ActionPress("move_left");await Frames(30);Input.ActionRelease("move_left");
                KeyEvent(Key.Space,true);await Frames(2);KeyEvent(Key.Space,false);await Frames(2);
                Check(RewriteLessonStep==6&&Paused&&Hud.MenuVisible,"actual return completes lesson "+frame);
                await Capture(frame+"-lesson-complete");ReturnToTitle();
                Check(Overload.Content.CharacterStore.Encode(Character.State)==saved,"lesson leaves entire save unchanged "+frame);
                Check(!PlayerState.ElsewhereActive&&!RewriteLessonActive,"leaving restores original contract "+frame);
            }
            StartWorld();await Frames(2);
            Check(WorldDestination?.Name=="Ash Foundry","fresh Hearth guides to the unlocked road");
            var known=KnownWorldTargets().ToArray();Check(known.All(t=>WorldCellKnown(t.Position)),"tracking excludes unrevealed landmarks");
            TrackWorldTarget("exit:road.3");Check(WorldDestination?.Name=="Ash Foundry","unknown destination cannot be manually tracked");
            var ticks=PlayerState.Tick;KeyEvent(Key.Tab,true);await Frames(3);KeyEvent(Key.Tab,false);await Frames(10);
            Check(LocalMapVisible&&PlayerState.Tick==ticks,"tactical map freezes combat clocks");await Capture("hearth-tactical-map");
            TogglePause();Check(!LocalMapVisible&&!Paused,"back closes map without opening a pause menu");
            WorldTransaction("identity.ash",s=>WorldRules.Enter(s,"ash.0",new(140,560)));EnterWorldZone(true);await Frames(3);
            Check(!PlayerState.Memories.InCombat&&Enemies.Any(e=>!e.Enemy!.Dead),"distant world defenders wait instead of pursuing immediately");
            var guard=Enemies.First(e=>!e.Enemy!.Dead);Player.Position=guard.Position-new Vector2(90,0);Player.TeleportVisual();await Frames(3);
            Check(PlayerState.Memories.InCombat,"visible nearby defenders engage as a pack");await Capture("ash-engaged-pack");
            LocalMapVisible=true;await Capture("ash-discovery-map");LocalMapVisible=false;
            if(rendered)
            {
                foreach(var size in new[]{new Vector2I(960,540),new(1280,720),new(1920,1080)})
                {
                    DisplayServer.WindowSetSize(size);await Frames(8);
                    foreach(var text in new[]{100,125})
                    {
                        if(Audio.TextPercent!=text)Audio.ToggleText();await Frames(2);
                        await Capture($"combat-{size.X}-{text}");LocalMapVisible=true;await Capture($"map-{size.X}-{text}");LocalMapVisible=false;
                    }
                }
                DisplayServer.WindowSetSize(new(1280,720));if(Audio.TextPercent!=100)Audio.ToggleText();await Frames(8);
                foreach(var region in Enum.GetValues<Region>())
                {
                    WorldTransaction("identity.scenery:"+Guid.NewGuid(),s=>WorldRules.Enter(s,WorldContent.Id(region,0),new(1260,290)));EnterWorldZone(true);
                    PlayerState.ChangeMaximumLife(System.Numerics.BigInteger.Pow(10,24));PlayerState.Reset();await Capture("world-"+region+"-gantry");
                    Check(world.PavedRoads.Any(p=>p.Length>1)&&world.PavedRoads.All(p=>Enumerable.Range(1,Math.Max(0,p.Length-1)).All(i=>WorldNavigation.Clear(new(p[i-1].X,p[i-1].Y),new(p[i].X,p[i].Y),36))),"painted roads have navigable width and real segments "+region);
                    Player.Position=new(940,1020);Player.TeleportVisual();await Frames(4);
                    foreach(var defender in Enemies)defender.Enemy!.ReceiveHit(defender.Enemy.MaximumLife,0,PlayerState.Tick);
                    await Frames(3);InteractWorld();await Frames(2);
                    Check(WorldClaims.Contains(WorldContent.Id(region,0)+".refuge"),"refuge scenery follows an actual reclamation "+region);await Capture("world-"+region+"-refuge");
                    WorldTransaction("identity.ruler:"+Guid.NewGuid(),s=>WorldRules.Enter(s,WorldContent.Id(region,1),new(470,384)));EnterWorldZone(true);
                    PlayerState.ChangeMaximumLife(System.Numerics.BigInteger.Pow(10,24));PlayerState.Reset();await Frames(80);await Capture("world-"+region+"-boss");
                }
            }
            GD.Print("OVERLOAD_IDENTITY_OK checks="+checks);QuitGame();
        }
        catch(Exception e){GD.PushError(e.ToString());QuitGame(1);}
    }
}
