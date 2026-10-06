using Godot;
using Overload.Domain;
using NVector=System.Numerics.Vector2;
namespace Overload.Game;
public partial class Arena
{
    private async void RunExpansionSmoke()
    {
        var checks=0;
        void Check(bool value,string label) { if(!value)throw new InvalidOperationException(label);checks++;GD.Print("EXPANSION PASS: "+label); }
        async Task Frames(int count) { for(var i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame); }
        void Profile(CharacterState fixture) { ReturnToTitle();Check(UpdateCharacter(s=>fixture with { CharacterId=s.CharacterId,Revision=s.Revision }),"saved "+fixture.Frame+" profile"); }
        var captures=OS.GetCmdlineUserArgs().Contains("--capture-expansion");
        var output=System.IO.Path.Combine(ProjectSettings.GlobalizePath("res://"),"../../artifacts/screenshots-expansion");
        async Task Capture(string name) { if(!captures)return;System.IO.Directory.CreateDirectory(output);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(output,name+".png")); }
        try
        {
            Hud.NewFrameMenu();await Capture("frames");Hud.Title();
            foreach(var frame in Enum.GetValues<FrameId>())
            {
                for(var build=0;build<3;build++)
                {
                    Profile(FrameRules.BuildFixture(frame,build));ReloadCharacterForCheck();Check(Character!.State.Frame==frame&&Character.State.EquippedSkills.SequenceEqual(FrameRules.BuildFixture(frame,build).EquippedSkills),$"{frame} build {build+1} survives disk reload");
                    if(build==0) { Hud.InspectItem(Character.State.Inventory[0].Id);Check(Hud.MenuVisible,frame+" equipment comparison uses its basic action");await Capture(frame.ToString().ToLowerInvariant()+"-gear");Hud.Title(); }
                    Check(Balance.Skills.Length==10&&EquippedAction(SkillId.Cleave)==FrameRules.Basic(frame),"eight Frame skills plus Traverse/Flask and basic input mapping");
                }
                foreach(var id in FrameRules.Skills(frame))
                {
                    Profile(FrameRules.BuildFixture(frame,0));StartEncounter(3);world.Configure([]);ClearEncounterEnemies();
                    var target=Spawn(BaseBalance.Enemies.Single(e=>e.Role==EnemyRole.Brute) with { Life=10000,Speed=0,Windup=600,Recovery=600 },Player.Position+new Vector2(32,0));
                    var before=target.Enemy!.Life;Check(PlayerState.TryStart(id,NVector.UnitX),"resolver accepted "+id);
                    await Frames(20);
                    if(id is SkillId.EmberWell or SkillId.StormLoom or SkillId.EchoOrder) { target.Position=Player.Position+new Vector2(Balance.Skills.Single(s=>s.Id==id).Reach,0);await Frames(3);await Capture(id.ToString().ToLowerInvariant()); }
                    await Frames(150);
                    if(id is SkillId.Bulwark or SkillId.Veil or SkillId.RecallThread)Check(PlayerState.Barrier>0&&target.Enemy.Life==before,id+" supplies barrier without a fake direct hit");
                    else Check(target.Enemy.Life<before,id+" lands through live geometry");
                    if(id==SkillId.StormLoom||id==SkillId.EchoOrder)Check(Effects.RecentEvents.Any(e=>e.Source==SourceKind.SecondaryEffect&&e.Kind=="hit"),id+" secondary provenance is explicit");
                    Check(Effects.GroundCastCount<=4&&Effects.CommandedEchoCount<=1&&Effects.ProjectileCount<=200,"bounded Frame effects");
                }
            }
            foreach(var region in Enum.GetValues<Region>())
            {
                var fixture=FrameRules.BuildFixture(FrameId.Threadseer,1) with { FractureUnlocked=true,HighestClearedTier=10,HighestUnlockedTier=11,Chapter=2 };
                Profile(fixture);StartRegional(10,region,ActivityFamily.Hunt);await Frames(3);
                Check(Character!.State.Fracture!.ContentVersion==RegionalContent.ExpeditionVersion,"new regional version: "+region);
                Check(Enemies.All(e=>e.Enemy!.Definition.ContentId.StartsWith("enemy.region.",StringComparison.Ordinal)),"regional roster spawned: "+region);
                Check(Enemies.All(e=>WorldQueries.Connected(Player.Position,e.Position,Player.Radius,CollisionWalls)),"actual regional sockets are reachable");await Capture(region.ToString().ToLowerInvariant());
                for(var boss=0;boss<2;boss++)
                {
                    StartEncounter(3);ClearEncounterEnemies();world.Configure([]);world.SetRegion(region);var body=Spawn(RegionalContent.Boss(region,boss),new(400,192));Player.Position=new(240,192);
                    await Frames(180);Check(body.TellTicks>=24&&Effects.ProjectileCount<=9,"boss tell and projectile budget: "+body.Enemy!.Definition.Name);await Capture(body.Enemy!.Definition.Name!.ToLowerInvariant());
                }
            }
            Profile(FrameRules.BuildFixture(FrameId.Warden,0));
            foreach(var room in RegionalContent.Rooms)
            {
                StartEncounter(3);ClearEncounterEnemies();world.SetRegion(room.Region);world.Configure([..room.Obstacles.Select(b=>new Rect2(b.X,b.Y,b.Width,b.Height))]);
                PlayerState.TryChangeOaths(["oath.elsewhere"]);await Frames(3);
                Check(PlayerState.TryStart(SkillId.Traverse,NVector.UnitX),"room "+room.Id+": Elsewhere placement accepted");await Frames(10);
                Player.Position=new(320,192);await Frames(15);
                Check(PlayerState.TryStart(SkillId.Traverse,NVector.UnitX),"room "+room.Id+": connected return accepted");motion.Move(Vector2.Zero);await Frames(8);
                Check(Player.Position.DistanceTo(new(140,190))<3,"room "+room.Id+": return used live terrain and footprint");
            }
            Profile(FrameRules.BuildFixture(FrameId.Revenant,0));StartCovenantChallenge();await Frames(3);
            Check(CovenantChallengeActive&&PlayerState.RedCovenantActive&&!PlayerState.ElsewhereActive,"normalized challenge supplies one temporary oath");
            Check(PlayerState.TryStart(SkillId.Reap,NVector.UnitX),"challenge reserves Life through authority");await Frames(45);Check(PlayerState.TryStart(SkillId.EchoOrder,NVector.UnitX),"challenge reaches twenty percent reservation");await Frames(40);
            foreach(var enemy in Enemies)enemy.Enemy!.ReceiveHit(enemy.Enemy.MaximumLife,0,PlayerState.Tick);CompleteTrialStage();await Frames(3);Check(TrialStage==1,"reservation mastery stage banked in attempt");
            PlayerState.TryStart(SkillId.Reap,NVector.UnitX);await Frames(40);PlayerState.ReceiveHit(PlayerState.MaximumLife/3,false);Check(PlayerState.TryStart(SkillId.Flask,NVector.UnitX),"challenge Flask accepted at reduced capacity");await Frames(60);await Capture("red-covenant");
            foreach(var enemy in Enemies)enemy.Enemy!.ReceiveHit(enemy.Enemy.MaximumLife,0,PlayerState.Tick);CompleteTrialStage();await Frames(3);Check(TrialStage==2,"healing mastery stage recognized");
            foreach(var enemy in Enemies)enemy.Enemy!.ReceiveHit(enemy.Enemy.MaximumLife,0,PlayerState.Tick);CompleteTrialStage();ReloadCharacterForCheck();
            Check(Character!.State.CovenantMastery&&!Character.State.RedCovenantOwned,"mastery persists separately from ritual ownership");
            Check(UpdateCharacter(s=>s with { Mode=ProfileMode.Standard,ElsewhereOwned=true,Seals=[30,30,30,30] }),"isolated earned-first-oath wallet fixture");
            Check(UpdateCharacter(CovenantRules.Unlock),"real Covenant ritual commits ownership with all four debits");
            Check(UpdateCharacter(s=>CovenantRules.Select(s,"oath.red-covenant")),"earned Covenant equipped at Hearth");ReloadCharacterForCheck();
            Check(Character.State.RedCovenantOwned&&Character.State.Seals.All(v=>v==0)&&PlayerState.RedCovenantActive&&!PlayerState.ElsewhereActive,"ownership payment and single selection survive disk reload");
            var campaign=FrameRules.Create(FrameId.Revenant,false);Profile(campaign);StartJourney();await Frames(3);
            for(var room=0;room<16;room++)
            {
                Check(JourneyRoom==room&&Enemies.All(e=>WorldQueries.Connected(Player.Position,e.Position,Player.Radius,CollisionWalls)),"campaign checkpoint reachable: "+room);
                foreach(var enemy in Enemies)enemy.Enemy!.ReceiveHit(enemy.Enemy.MaximumLife,0,PlayerState.Tick);CompleteJourneyRoom();if(room<15)ContinueJourney();await Frames(3);
            }
            Check(Character!.State.CheckpointRoom==16&&Character.State.FractureUnlocked,"First Pattern campaign opens uncapped Atlas");await Capture("ending");
            GD.Print($"OVERLOAD_EXPANSION_SMOKE_OK checks={checks}");QuitGame();
        }
        catch(Exception e) { GD.PushError("EXPANSION FAIL: "+e);QuitGame(1); }
    }
}
