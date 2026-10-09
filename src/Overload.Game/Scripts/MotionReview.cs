using Godot;
using Overload.Domain;
using System.Text.Json;

namespace Overload.Game;

public partial class Arena
{
    private sealed record MotionSample(ulong Physics, float X, float Y, float VisualX, float VisualY, float CameraX, float CameraY, int Row);
    private readonly List<MotionSample> motionSamples = [];
    private bool collectMotionFrames;
    private void RecordMotionFrame()
    {
        if(collectMotionFrames)
            motionSamples.Add(new(Engine.GetPhysicsFrames(),Player.Position.X,Player.Position.Y,Player.VisualPosition.X,Player.VisualPosition.Y,viewCamera.Position.X,viewCamera.Position.Y,Player.AnimationRow));
    }

    // Opt-in, isolated profile. Exercises the same input, physics and presentation used by the launcher.
    private async void ReviewMotion()
    {
        var rendered=OS.GetCmdlineUserArgs().Contains("--motion-review");var checks=0;
        var output=System.IO.Path.Combine(ProjectSettings.GlobalizePath("res://"),"../../artifacts/motion");Directory.CreateDirectory(output);
        void Check(bool value,string label){if(!value)throw new InvalidOperationException(label);checks++;GD.Print("MOTION PASS: "+label);}
        async Task Frames(int count)
        {for(var i=0;i<count;i++){await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}}
        void KeyEvent(Key key,bool down)
        {Input.ParseInputEvent(new InputEventKey{Pressed=down,Keycode=key,PhysicalKeycode=key});Input.FlushBufferedEvents();}
        Vector2 Screen(Vector2 p)=>worldContainer.Position+(p-CameraOrigin)*WorldScale;
        void Pointer(Vector2 p)
        {var screen=Screen(p);Input.ParseInputEvent(new InputEventMouseMotion{Position=screen,GlobalPosition=screen,Relative=new(12,0)});Input.FlushBufferedEvents();}
        void Right(bool down)
        {Input.ParseInputEvent(new InputEventMouseButton{ButtonIndex=MouseButton.Right,Pressed=down,Position=Controls.PointerPosition});Input.FlushBufferedEvents();}
        void Reset(Vector2 position)
        {Controls.Disconnected();ResetPointerTravel();StartEncounter(0);ClearEncounterEnemies();transitionTicks=-1000000;Player.Position=position;Player.TeleportVisual();}
        async Task Shot(string name)
        {
            if(!rendered)return;
            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            using var image=GetViewport().GetTexture().GetImage();image.SavePng(System.IO.Path.Combine(output,name+".png"));
        }
        try
        {
            GetWindow().Size=new(1280,720);await Frames(4);
            foreach(var frame in Enum.GetValues<FrameId>())
            {
                ReturnToTitle();Check(UpdateCharacter(s=>FrameRules.Create(frame) with {CharacterId=s.CharacterId}),frame+" isolated profile");
                Reset(new(140,190));await Frames(2);var origin=Player.Position;
                KeyEvent(Key.D,true);await Frames(3);
                Check(Player.Velocity.X>=Balance.Hero.Speed*.99f,frame+" reaches walking speed within 50 ms");
                await Frames(20);Check(Player.Facing.X>.99f,frame+" faces travel while walking");await Shot(frame+"-walk");
                var release=Player.Position;KeyEvent(Key.D,false);await Frames(3);
                Check(Player.Velocity.Length()<.01f&&Player.Position.DistanceTo(release)<1.2f,frame+" brakes promptly without skating");
                Check(Math.Abs(Player.WalkDistance-Player.Position.DistanceTo(origin))<.05f,frame+" walk cycle matches actual travel");
            }
            ReturnToTitle();Check(UpdateCharacter(s=>FrameRules.Create(FrameId.Warden) with {CharacterId=s.CharacterId}),"restore Warden fixture");
            Reset(new(140,190));await Frames(2);KeyEvent(Key.D,true);await Frames(8);
            KeyEvent(Key.D,false);KeyEvent(Key.A,true);await Frames(3);
            Check(Player.Velocity.X < -Balance.Hero.Speed*.99f,"180-degree steering responds within three ticks");
            KeyEvent(Key.A,false);await Frames(3);
            Reset(new(36,190));await Frames(2);KeyEvent(Key.A,true);await Frames(16);var stride=Player.WalkDistance;
            await Frames(20);Check(Player.Position.X>=34&&Math.Abs(Player.WalkDistance-stride)<.02f,"blocked movement cannot run the walk cycle");
            KeyEvent(Key.S,true);var wallStart=Player.Position;await Frames(30);
            Check(Player.Position.X>=34&&Player.Position.Y-wallStart.Y>35,"diagonal input slides along a wall");
            KeyEvent(Key.A,false);KeyEvent(Key.S,false);await Frames(3);

            Reset(new(140,190));await Frames(2);Check(RequestWorldWalk(new(185,211)),"precise click destination accepted");await Frames(70);
            Check(PointerDestination is null&&Player.Position.DistanceTo(new(185,211))<1.5f&&Player.Velocity.Length()<.01f,"click travel arrives without orbiting or overshoot");
            Reset(new(180,130));await Frames(2);Check(RequestWorldWalk(new(440,130)),"click route around cover accepted");
            await Frames(300);
            Check(PointerDestination is null&&Player.Position.DistanceTo(new(440,130))<1.5f,"steering follows collision-safe bends around cover");
            Reset(new(140,190));await Frames(2);Pointer(new(240,210));Right(true);await Frames(12);
            Pointer(new(185,270));await Frames(10);
            Check(PointerDestination?.DistanceTo(new(185,270))<1,"held right mouse refreshes the steering destination");Right(false);
            KeyEvent(Key.D,true);await Frames(2);KeyEvent(Key.D,false);Check(PointerDestination is null,"manual movement cancels click travel");

            Reset(new(140,190));await Frames(2);Pointer(new(400,190));var evadeStart=Player.Position;
            KeyEvent(Key.Space,true);await Frames(1);KeyEvent(Key.Space,false);await Frames(Balance.Hero.EvadeTicks+2);
            Check(Math.Abs(Player.Position.DistanceTo(evadeStart)-Balance.Hero.EvadeDistance)<.1f,"eased evade retains full authored range in real physics");
            Reset(new(140,190));await Frames(2);Pointer(new(400,190));
            Check(PlayerState.TryStart(SkillId.Cleave,System.Numerics.Vector2.UnitX),"attack fixture starts");
            var attack=PlayerState.Action!;var releaseAge=attack.Definition.Windup+attack.Definition.Active+6;
            while(attack.Age(PlayerState.Tick)<releaseAge-8)await Frames(1);
            KeyEvent(Key.Q,true);await Frames(1);KeyEvent(Key.Q,false);await Frames(8);
            Check(PlayerState.Action?.Definition.Id==SkillId.ShieldPulse,"deliberate skill press survives the end of a committed attack");

            Reset(new(140,190));await Frames(2);KeyEvent(Key.D,true);await Frames(10);TogglePause();await Frames(2);
            var frozen=Player.VisualPosition;var frozenStride=Player.VisualStride;var frozenTick=PlayerState.Tick;
            await Frames(12);Check(Player.VisualPosition==frozen&&Player.VisualStride==frozenStride&&PlayerState.Tick==frozenTick,"pause freezes both animation and physics");
            KeyEvent(Key.D,false);TogglePause();await Frames(4);Check(Player.Velocity.Length()<.01f,"resume respects released movement keys");

            ReturnToTitle();Check(UpdateCharacter(s=>WorldRules.Enroll(FrameRules.Create(FrameId.Warden)) with {CharacterId=s.CharacterId}),"isolated open-world camera fixture");
            Check(WorldTransaction("motion.frontier:"+Guid.NewGuid(),s=>WorldRules.Travel(s,"frontier")),"frontier camera fixture enters");
            EnterWorldZone(true);ClearEncounterEnemies();worldPacks.Clear();Player.Position=new(2900,1920);Player.TeleportVisual();
            PlayerState.ChangeMaximumLife(System.Numerics.BigInteger.Pow(10,24));PlayerState.Reset();await Frames(6);
            motionSamples.Clear();collectMotionFrames=true;KeyEvent(Key.D,true);await Frames(90);KeyEvent(Key.D,false);await Frames(45);collectMotionFrames=false;
            Check(motionSamples.Count>100,"render-frame motion trace collected");
            var jumps=motionSamples.Zip(motionSamples.Skip(1)).Select(p=>Math.Abs(p.Second.CameraX-p.First.CameraX)).ToArray();
            Check(jumps.Max()<4,"camera has no physics-sized snapping during ordinary movement");
            Check(viewCamera.Position.DistanceTo(Player.VisualPosition)<1,"camera settles over the stopped player");
            if(rendered)
            {
                Check(motionSamples.GroupBy(s=>s.Physics).Count(g=>g.Select(s=>s.VisualX).Distinct().Count()>1)>20,"sprite positions interpolate between physics ticks at 120 FPS");
                await Shot("frontier-camera-settled");
            }
            Player.Position+=new Vector2(500,0);Player.TeleportVisual();await Frames(2);
            Check(viewCamera.Position.DistanceTo(Player.VisualPosition)<1,"teleport resets camera immediately");
            var probe=Player.Position+new Vector2(60,-30);Pointer(probe);
            Check(CursorWorldPosition.DistanceTo(probe)<.01f,"cursor projection agrees with the moving camera");
            Controls.Read(PlayerState.Tick,Player.Position,CursorWorldPosition);
            Controls.Observe(new InputEventJoypadMotion{Axis=JoyAxis.LeftX,AxisValue=.8f});
            Controls.Read(PlayerState.Tick,Player.Position,CursorWorldPosition+new Vector2(20,0));
            Check(Controls.Controller,"camera scrolling cannot switch controller input to mouse aiming");Controls.Disconnected();
            if(rendered)
            {
                // A short rendered sequence makes the walk/turn/attack/evade transitions reviewable.
                var framesDirectory=System.IO.Path.Combine(output,"sequence");Directory.CreateDirectory(framesDirectory);
                KeyEvent(Key.D,true);
                for(var i=0;i<90;i++)
                {
                    if(i==24){KeyEvent(Key.D,false);KeyEvent(Key.S,true);}
                    if(i==42){KeyEvent(Key.S,false);Pointer(Player.Position+new Vector2(100,0));KeyEvent(Key.Space,true);}
                    if(i==43)KeyEvent(Key.Space,false);
                    if(i==60){KeyEvent(Key.D,true);Input.ActionPress("cleave");}
                    if(i==80){KeyEvent(Key.D,false);Input.ActionRelease("cleave");}
                    await Frames(1);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                    using var image=GetViewport().GetTexture().GetImage();image.SavePng(System.IO.Path.Combine(framesDirectory,$"{i:D3}.png"));
                }
            }
            Controls.Disconnected();ResetPointerTravel();ReturnToTitle();
            File.WriteAllText(System.IO.Path.Combine(output,rendered?"review.json":"smoke.json"),JsonSerializer.Serialize(new{checks,rendered,build="movement.2026-10-09",physicsHz=60,motionSamples,maxCameraStep=jumps.Max()},new JsonSerializerOptions{WriteIndented=true}));
            GD.Print($"OVERLOAD_MOTION_OK checks={checks} rendered={rendered}");QuitGame();
        }
        catch(Exception e){GD.PushError("MOTION REVIEW: "+e);QuitGame(1);}
    }
}
