using Godot;
using Overload.Domain;
using System.Text.Json;
using NVector=System.Numerics.Vector2;

namespace Overload.Game;
public partial class Arena
{
    private async void RunProductionSmoke()
    {
        var checks=new List<string>();
        void Check(bool result,string message){if(!result)throw new InvalidOperationException(message);checks.Add(message);GD.Print("PRODUCTION PASS: "+message);}
        async Task Frames(int count){for(var i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);}
        void Profile(CharacterState fixture){ReturnToTitle();Check(UpdateCharacter(s=>fixture with { CharacterId=s.CharacterId,Revision=s.Revision }),"isolated profile transaction");}
        CharacterState Reference()=>EndlessFixtures.Reference(10) with { Mode=ProfileMode.Standard,Seals=[0,0,0,0],TotalXp=Progression.TotalXp(100),ValidatedLevel=100,Might=20,Resolve=20 };
        async Task Group()
        {
            await Frames(3);
            foreach(var enemy in Enemies)enemy.Enemy!.ReceiveHit(enemy.Enemy.MaximumLife,0,PlayerState.Tick);
            if(Character!.State.Fracture!.Activity==ActivityFamily.Vault) { Player.Position=new(560,192);await Frames(3); }
            Check(EndgameObjectiveReady,"activity objective permits checkpoint");
            CompleteFractureGroup();await Frames(2);
        }
        try
        {
            if(DisplayServer.GetName()!="headless")await Task.Delay(1000);
            Profile(Reference());
            foreach(var family in Enum.GetValues<ActivityFamily>())
            {
                Profile(Reference());StartRegional(10,Region.Hollow,family);await Frames(3);
                var run=Character!.State.Fracture!;var xp=Character.State.TotalXp;
                Check(run.Layout is not null&&ExpeditionGenerator.Connected(run.Layout),$"{family}: saved generated layout connected");
                Check(Enemies.Count<=3&&Enemies.All(e=>WorldQueries.Connected(Player.Position,e.Position,Player.Radius,CollisionWalls)),$"{family}: actual spawned colliders reachable");
                if(family==ActivityFamily.Hunt)
                {
                    var target=Enemies[0];target.Position=Player.Position+new Vector2(32,0);
                    target.Enemy!.ReceiveHit(0,target.Enemy.Definition.StaggerThreshold-5,PlayerState.Tick);
                    await Frames(50);PlayerState.TryStart(SkillId.Cleave,NVector.UnitX);await Frames(10);
                    Check(PlayerState.Memories.Stillness is not null&&PlayerState.Memories.Rupture is not null,"actual base hit earns Stillness and first stagger earns Rupture");
                    Check(PlayerState.Memories.Stillness!.Direction==NVector.UnitX&&PlayerState.Memories.Rupture!.TargetId==target.ActorId,"memories retain accepted hit aim and broken target identity");
                    await Frames(18);PlayerState.TryChangeBindings([]);
                    var selected=PlayerState.LastSelection;
                    Check(Effects.HitsDealt>0&&selected is not null,"base hit traveled through resolver and geometry");
                }
                for(var group=0;group<7;group++)
                {
                    await Group();Check(Character.State.Fracture!.NextGroup==group+1,$"{family} group {group+1}: checkpoint banked once");
                    if(group==2){var id=Character.State.Fracture.Id;ReturnToTitle();ReloadCharacterForCheck();StartRegional(10,Region.Hollow,family);Check(Character.State.Fracture.Id==id,$"{family}: real store reload resumes identity");}
                    else if(group<6)ContinueFracture();
                }
                Check(Character.State.TotalXp-xp==run.XpBudget&&Character.State.Seals[2]==6&&Character.State.Proofs.Contains(EndgameRules.Proof(Region.Hollow,family)),$"{family}: exact XP and eligible regional rewards");
                Check(Character.State.RunRecords.Length==1,$"{family}: compact build record saved");
            }
            Profile(Reference());var original=Character!.State;
            StartTrial();await Frames(3);
            Check(PlayerState.MaximumLife==new PlayerCombat(FoundationRules.Build(BaseBalance,EndgameRules.TrialActor(original)),character:EndgameRules.TrialActor(original),tier:10).MaximumLife&&!PlayerState.ElsewhereActive,"trial uses normalized separate actor and base contract");
            var target2=Enemies[0];target2.Position=Player.Position+new Vector2(32,0);
            PlayerState.TryStart(SkillId.Cleave,NVector.UnitX);await Frames(26);PlayerState.TryStart(SkillId.ShieldPulse,NVector.UnitX);await Frames(22);
            foreach(var enemy in Enemies)enemy.Enemy!.ReceiveHit(enemy.Enemy.MaximumLife,0,PlayerState.Tick);CompleteTrialStage();await Frames(3);
            Check(TrialStage==1,"trial signature choice stage accepts alternate base skills");
            var healthy=PlayerState.Life;Player.Position=new(300,100);await Frames(92);Check(PlayerState.Life<healthy,"trial hazard damage uses its shown rectangle");
            Player.Position=new(140,192);foreach(var enemy in Enemies)enemy.Enemy!.ReceiveHit(enemy.Enemy.MaximumLife,0,PlayerState.Tick);CompleteTrialStage();await Frames(3);
            Check(TrialStage==2,"trial spatial stage reaches boss");
            foreach(var enemy in Enemies)enemy.Enemy!.ReceiveHit(enemy.Enemy.MaximumLife,0,PlayerState.Tick);CompleteTrialStage();
            Check(Character.State.TrialCompleted&&Character.State.TotalXp==original.TotalXp&&Character.State.Inventory.SequenceEqual(original.Inventory)&&Character.State.Might==original.Might&&Character.State.AttunementGrade==original.AttunementGrade,"trial banks proof without changing persistent power or rewards");
            ReloadCharacterForCheck();Check(Character.State.TrialCompleted,"trial proof survives actual disk reload");
            var qualified=Character.State with { Seals=[120,120,120,120],Proofs=EndgameRules.AllProofs,Masteries=[..Enum.GetValues<Region>()] };
            Check(UpdateCharacter(_=>qualified),"accelerated breadth fixture applied after actual trial");
            Check(UpdateCharacter(EndgameRules.Ritual),"real ritual transaction debits qualified character");ReloadCharacterForCheck();
            Check(Character.State.ElsewhereOwned&&Character.State.Seals.All(v=>v==0),"ritual ownership and all four debits survive reload");
            Profile(Reference());Check(UpdateCharacter(s=>EndgameRules.StartChain(s,10,Region.Ash)),"chain identity and region order saved");
            for(var leg=0;leg<3;leg++)
            {
                if(!Character.State.ChapterOffers.IsEmpty)UpdateCharacter(s=>EndlessRules.ChooseRoute(s,s.ChapterOffers[0]));
                var region=Character.State.Chain!.Regions[leg];StartRegional(10,region,ActivityFamily.Hunt);await Frames(3);var before=Character.State.Gold;
                if(leg==1)
                {
                    PlayerState.ReceiveHit(PlayerState.MaximumLife*100,false);await Frames(2);
                    Check(!Character.State.Fracture!.ChainBonusEligible,"chain death forfeits only current optional bonus");RetryCurrentRoom();await Frames(3);
                    ReturnToTitle();ReloadCharacterForCheck();StartRegional(10,region,ActivityFamily.Hunt);await Frames(3);
                    Check(!Character.State.Fracture!.ChainBonusEligible,"spent chain bonus eligibility survives disk reload");
                }
                for(var group=0;group<7;group++){await Group();if(group<6)ContinueFracture();}
                Check(Character.State.Chain.CompletedLegs==leg+1,$"chain leg {leg+1} committed with reward");
                Check(Character.State.Gold-before==(leg==2?2400:2000),$"chain leg {leg+1} bonus terms honored once");
                ReturnToTitle();ReloadCharacterForCheck();
                Check(Character.State.Chain.CompletedLegs==leg+1,$"chain leg {leg+1} survives disk reload");
            }
            Check(Character.State.Chain!.Closed,"third leg closes chain");
            foreach(var mutation in WorldLaws.Mutations)
            {
                Profile(Reference());StartRegional(10,Region.Glass,ActivityFamily.Hunt,mutation,true);await Frames(3);
                Check(Character!.State.Fracture!.Mutation==mutation&&Character.State.Fracture.Anomaly,$"Anomaly discloses and persists {mutation}");
                Check(WorldCoverCollision(new(300,120),new(370,120),false),"breakable cover intercepts hostile projectile");
                Check(WorldCoverCollision(new(300,120),new(370,120),true),"base attack breaks cover");
                for(var i=0;i<6;i++) { await Group();ContinueFracture(); }
                Player.Position=new(360,192);bool sawMark=false;for(var i=0;i<36;i++){await Frames(10);sawMark|=Enemies[0].ReturnMark is not null;}
                Check(Enemies.Count==1&&Enemies[0].TellTicks>=24&&Effects.ProjectileCount<=7,$"{mutation}: bounded boss and fixed warning floor");
                if(mutation=="mutation.anchor")Check(sawMark,"remembering boss actually marks or returns");
            }
            Profile(Reference());StartSovereign(10);await Frames(3);foreach(var enemy in Enemies)enemy.Enemy!.ReceiveHit(enemy.Enemy.MaximumLife,0,PlayerState.Tick);CompleteSovereign();ReloadCharacterForCheck();
            Check(Character!.State.SovereignRewards.Contains("echo.anchor")&&Character.State.Seals.All(s=>s==0)&&Character.State.TotalXp==Reference().TotalXp,"Sovereign awards cosmetic/Codex/record without ritual or power rewards");
            System.IO.File.WriteAllText(ProjectSettings.GlobalizePath("user://tests/production-smoke.json"),JsonSerializer.Serialize(new { success=true,checks }));
            GD.Print("OVERLOAD_PRODUCTION_SMOKE_OK "+checks.Count);QuitGame();
        }
        catch(Exception e){GD.PushError("PRODUCTION FAILED: "+e);QuitGame(1);}
    }
}
