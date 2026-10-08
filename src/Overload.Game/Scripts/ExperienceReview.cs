using Godot;
using Overload.Domain;
using BigInteger = System.Numerics.BigInteger;

namespace Overload.Game;

/// <summary>Runtime evidence for navigation, continuous checkpoints, and the shipped presentation.</summary>
public partial class Arena
{
    private async void ReviewExperience()
    {
        var checks=0;
        var rendered=OS.GetCmdlineUserArgs().Contains("--experience-review");
        var directory=System.IO.Path.Combine(ProjectSettings.GlobalizePath("res://"),"../../artifacts/experience");
        void Check(bool value,string message)
        { if(!value)throw new InvalidOperationException(message);checks++;GD.Print("EXPERIENCE PASS: "+message); }
        async Task Frames(int count)
        { for(var i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame); }
        async Task Capture(string name)
        {
            if(!rendered)return;
            await Frames(3);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            using var screenshot=GetViewport().GetTexture().GetImage();screenshot.SavePng(System.IO.Path.Combine(directory,name+".png"));
        }
        try
        {
            System.IO.Directory.CreateDirectory(directory);
            if(Audio.TextPercent!=100)Audio.ToggleText();
            if(rendered)
            { Check(DisplayServer.GetName()!="headless","review has a renderer");await Task.Delay(500); }
            ReturnToTitle();Check(UpdateCharacter(s=>FrameRules.Create(FrameId.Warden) with { CharacterId=s.CharacterId }),"fresh Standard Warden fixture");
            ReturnToTitle();await Capture("hearth");
            Hud.NewFrameMenu();await Capture("frames");Hud.Title();
            StartJourney();await Frames(15);Check(CombatLesson.Contains("MOVE TO REMEMBER"),"first combat teaches the main mechanic");await Capture("first-room");
            PlayerState.ChangeMaximumLife(BigInteger.Pow(10,30));PlayerState.Reset();
            await Frames(2);
            PlayerState.Memories.ObserveLocomotion(new(96,0),LocomotionKind.Walk);
            PublishPredictions(Vector2.Right,Vector2.Zero);
            Check(Predictions[FrameRules.Basic(FrameId.Warden)].Selection.Implementation==ActionImplementation.Pursuit,"starting signature has a live prediction");await Capture("pursuit-ready");
            Check(PlayerState.TryStart(SkillId.Cleave,System.Numerics.Vector2.UnitX),"guided signature accepts");await Frames(5);
            Check(CombatLesson.Contains("READ THE WARNING"),"coach advances after an actual selected signature");
            foreach(var enemy in Enemies)enemy.Enemy!.ReceiveHit(enemy.Enemy.MaximumLife,0,PlayerState.Tick);
            await Frames(102);
            Check(CheckpointRest&&!Playing&&!Hud.MenuVisible,"ordinary clear keeps the player in the restored room");
            var checkpoint=Character!.State;
            var reward=Spoils!.Item!;
            Check(checkpoint.CheckpointRoom==1&&checkpoint.ClaimedRooms.Count==1&&Spoils.Xp>0,"checkpoint and real reward commit once");
            await Capture("checkpoint");
            InspectCheckpoint();await Capture("reward-comparison");if(rendered)Hud.CheckQualityLayout();
            EquipSpoilsAndContinue();await Frames(3);
            Check(Playing&&JourneyRoom==1&&Character.State.Equipment[reward.Slot]==reward.Id,"equip-and-continue saves and enters the next room");
            Check(Character.State.Gold==checkpoint.Gold&&Character.State.TotalXp==checkpoint.TotalXp,"equipping does not duplicate the room reward");
            ReturnToTitle();StartJourney();await Frames(3);
            Check(JourneyRoom==1,"Hearth retains the next-room checkpoint");
            foreach(var enemy in Enemies)enemy.Enemy!.ReceiveHit(enemy.Enemy.MaximumLife,0,PlayerState.Tick);
            await Frames(104);Check(CheckpointRest,"boss checkpoint also opens an exit before the campaign finale");
            Input.ParseInputEvent(new InputEventKey { Keycode=Key.G,PhysicalKeycode=Key.G,Pressed=true });Input.FlushBufferedEvents();await Frames(2);
            Input.ParseInputEvent(new InputEventKey { Keycode=Key.G,PhysicalKeycode=Key.G,Pressed=false });Input.FlushBufferedEvents();
            Check(Playing&&JourneyRoom==2,"interact binding continues a banked checkpoint");
            foreach(var enemy in Enemies)enemy.Enemy!.ReceiveHit(enemy.Enemy.MaximumLife,0,PlayerState.Tick);
            await Frames(104);Player.Position=CheckpointExit;await Frames(2);
            Check(Playing&&JourneyRoom==3,"walking into the gate continues without a menu");
            ReturnToTitle();ReloadCharacterForCheck();StartJourney();await Frames(2);
            Check(JourneyRoom==3&&Character!.State.ClaimedRooms.Count==3,"continuous checkpoints survive disk reload without duplicated receipts");

            foreach(var role in new[]{EnemyRole.Brute,EnemyRole.Bellkeeper})
            {
                ReturnToTitle();StartEncounter(0);ClearEncounterEnemies();world.Configure([new(285,120,45,140)]);
                Player.Position=new(180,192);Player.TeleportVisual();PlayerState.ChangeMaximumLife(BigInteger.Pow(10,30));PlayerState.Reset();
                var definition=BaseBalance.Enemies.Single(e=>e.Role==role) with { Speed=96,Windup=600,Recovery=600 };
                var actor=Spawn(definition,new(455,192));
                await Frames(390);
                Check(actor.Position.DistanceTo(Player.Position)<120,$"{role} physically routes around tall cover; at {actor.Position}, attack {actor.AttackAge}, volley {actor.Volley}");
                Check(!CollisionWalls.Skip(4).Any(r=>r.Grow(actor.Radius-.5f).HasPoint(actor.Position)),role+" keeps its footprint outside cover");
                await Capture("navigation-"+role.ToString().ToLowerInvariant());
            }
            ReturnToTitle();StartEncounter(0);ClearEncounterEnemies();world.Configure([]);
            Player.Position=new(320,192);Player.TeleportVisual();PlayerState.ChangeMaximumLife(BigInteger.Pow(10,30));PlayerState.Reset();
            for(var i=0;i<6;i++)Spawn(BaseBalance.Enemies.Single(e=>e.Role==EnemyRole.Pursuer) with { Speed=0,Reach=200,Windup=60 },new(240+i*24,140));
            var maximumWindups=0;
            for(var i=0;i<160;i++)
            {
                await Frames(1);
                maximumWindups=Math.Max(maximumWindups,Enemies.Count(e=>!e.Enemy!.Dead&&e.AttackAge>=0&&e.AttackAge<e.TellTicks));
            }
            Check(maximumWindups==3,"pack never exceeds three committed windups over 160 live ticks");
            await Capture("pack-tells");
            if(rendered)
            {
                foreach(var frame in Enum.GetValues<FrameId>())
                {
                    ReturnToTitle();UpdateCharacter(s=>FrameRules.BuildFixture(frame,0) with { CharacterId=s.CharacterId });StartEncounter(2);
                    world.SetRegion(frame==FrameId.Warden?Region.Ash:frame==FrameId.Threadseer?Region.Hollow:Region.Glass);
                    PlayerState.ChangeMaximumLife(BigInteger.Pow(10,30));PlayerState.Reset();Player.Position=new(320,205);Player.TeleportVisual();
                    var id=frame==FrameId.Warden?SkillId.Faultline:frame==FrameId.Threadseer?SkillId.StormLoom:SkillId.EchoOrder;
                    PlayerState.TryStart(id,System.Numerics.Vector2.UnitX);await Frames(24);await Capture("combat-"+frame.ToString().ToLowerInvariant());
                }
                foreach(var size in new[]{new Vector2I(960,540),new Vector2I(1280,720),new Vector2I(1440,900),new Vector2I(1920,1080),new Vector2I(2560,1440)})
                {
                    DisplayServer.WindowSetSize(size);
                    foreach(var text in new[]{100,125})
                    {
                        if(Audio.TextPercent!=text)Audio.ToggleText();ReturnToTitle();await Capture($"hearth-{size.X}x{size.Y}-{text}");Hud.CheckQualityLayout();
                        UpdateCharacter(s=>FrameRules.Create(FrameId.Warden) with { CharacterId=s.CharacterId });StartJourney();
                        foreach(var enemy in Enemies)enemy.Enemy!.ReceiveHit(enemy.Enemy.MaximumLife,0,PlayerState.Tick);
                        await Frames(104);await Capture($"checkpoint-{size.X}x{size.Y}-{text}");
                    }
                }
            }
            GD.Print($"OVERLOAD_EXPERIENCE_OK checks={checks}; rendered={rendered}; isolated profiles");QuitGame();
        }
        catch(Exception error){GD.PushError(error.ToString());QuitGame(1);}
    }
}
