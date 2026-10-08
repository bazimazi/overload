using Godot;
using Overload.Domain;
using BigInteger=System.Numerics.BigInteger;

namespace Overload.Game;
public partial class Arena
{
    private bool covenantChallenge;
    public bool CovenantChallengeActive=>TrialActive&&covenantChallenge;
    public void StartCovenantChallenge()
    {
        if(Playing||Character is null||Character.State.ElsewhereSelected||Character.State.RedCovenantSelected)return;
        covenantChallenge=true;specialAttempt=Guid.NewGuid();TrialStage=0;EnterTrialStage();
    }
    public bool TrialActive { get; private set; }
    public int TrialStage { get; private set; }
    public bool SovereignActive { get; private set; }
    private Guid specialAttempt;
    private Guid deathReceipt;
    private readonly HashSet<SkillId> trialSkills=[];
    private readonly HashSet<ActionImplementation> trialSignatures=[];
    private readonly Dictionary<long,(SkillId Skill,ActionImplementation Implementation)> trialActions=[];
    private bool masteryEarned;
    private EndgameWorld laws=null!;
    public string? ActiveMutation => SovereignActive?"mutation.anchor":FractureActive&&(FractureGroup==6||GraphFracture&&Enemies.Any(e=>e.Enemy is {Dead:false,Definition.Role:EnemyRole.Bellkeeper}))?Character?.State.Fracture?.Mutation:TrialActive&&TrialStage==2?"mutation.anchor":null;
    public bool HazardAssisted=>laws?.Assisted??false;
    public void SetHazardAssisted(bool value)=>laws.SetAssisted(value);
    public void StartRegional(BigInteger tier,Region region,ActivityFamily activity,string? mutation=null,bool anomaly=false)
    {
        if(Playing || Character is null)return;
        if(Character.State.Fracture is not { Completed:false })
            if(!UpdateCharacter(s=>!IsSmoke||OS.GetCmdlineUserArgs().Any(a=>a is "--world-smoke" or "--world-review")?FractureMapRules.Begin(s,tier,Guid.NewGuid(),region,activity,mutation,anomaly,laws?.Assisted??false):EndgameRules.Begin(s,tier,Guid.NewGuid(),region,activity,mutation,anomaly,laws?.Assisted??false,true))){Hud.Fractures();return;}
        EnterFractureGroup();
    }
    public void ObserveTrialAction(SkillId skill,ActionImplementation implementation,long root)
    { if(TrialActive) { if(trialActions.Count>=64)trialActions.Remove(trialActions.Keys.Min());trialActions[root]=(skill,implementation); } }
    public void ObserveTrialHit(long root)
    { if(TrialActive&&trialActions.TryGetValue(root,out var action)) { trialSkills.Add(action.Skill);trialSignatures.Add(action.Implementation); } }
    public void ObserveMastery(SourceKind source,bool stagger,EnemyRole role)
    {
        if(!FractureActive || !RegionalContent.IsRegional(Character!.State.Fracture!.ContentVersion) || source!=SourceKind.BasePlayerAction)return;
        var region=Character.State.Fracture.Region;
        if(region==Region.Hollow&&PlayerState.Memories.Stillness is not null || region==Region.Crown&&stagger&&role==EnemyRole.Bellkeeper)masteryEarned=true;
    }
    public void CoverBroken() { if(FractureActive&&Character!.State.Fracture!.Region==Region.Glass)masteryEarned=true; }
    public bool WorldCoverCollision(Vector2 from,Vector2 to,bool friendly,bool heavy=false)=>laws?.CoverCollision(from,to,friendly,heavy)??false;
    public void StrikeWorldCover(Vector2 origin,Vector2 aim,AttackRecipe recipe)=>laws?.StrikeCover(origin,aim,recipe);
    public void StartTrial()
    {
        if(Playing || Character is null || (Character.State.ElsewhereSelected||Character.State.RedCovenantSelected))return;
        covenantChallenge=false;specialAttempt=Guid.NewGuid();TrialStage=0;EnterTrialStage();
    }
    private void EnterTrialStage()
    {
        var stage=TrialStage;StartEncounter(0);TrialActive=true;TrialStage=stage;
        var actor=EndgameRules.TrialActor(Character!.State);
        Balance=FoundationRules.Build(BaseBalance,actor);PlayerState=new(Balance,new ArenaActionPreflight(this),actor,10);
        PlayerState.TryChangeBindings(Character.State.Bindings);PlayerState.TryChangeOaths(covenantChallenge?["oath.red-covenant"]:[]);
        ClearEncounterEnemies();world.Configure([]);EncounterTier=10;
        if(stage==2)Spawn(EnemyRole.Bellkeeper,new(448,192));
        else { Spawn(EnemyRole.Pursuer,new(448,192));Spawn(EnemyRole.Caster,new(512,128));if(stage==1)Spawn(EnemyRole.Brute,new(512,264)); }
        trialSkills.Clear();trialSignatures.Clear();trialActions.Clear();laws.Configure(stage==1?["law.vents"]:[],null,ActivityFamily.Hunt);
        Hud.HideMenu();
    }
    private void CompleteTrialStage()
    {
        if(covenantChallenge&& (TrialStage==0&&PlayerState.PeakReservation<200 || TrialStage==1&&!PlayerState.HealedUnderReservation))
        { Playing=false;Hud.CovenantRetry();return; }
        if(!covenantChallenge&&TrialStage==0&&trialSkills.Count<2&&trialSignatures.Count<2)
        { Playing=false;Hud.TrialChoiceRetry();return; }
        if(TrialStage<2) { TrialStage++;EnterTrialStage();return; }
        Playing=false;
        if(UpdateCharacter(s=>covenantChallenge?s with { CovenantMastery=true }:EndgameRules.TrialVictory(s,specialAttempt))) { ReturnToTitle();Hud.Oaths(); }
        else Hud.TrialSaveFailed();
    }
    public void RetryTrialReward()=>CompleteTrialStage();
    public void RetrySpecial() { if(TrialActive)EnterTrialStage();else if(SovereignActive)StartSovereign(EncounterTier,true); }
    private bool BankEndgameDeath()
    {
        if(!FractureActive||Character!.State.Fracture is not { Completed:false } run || !RegionalContent.IsRegional(run.ContentVersion))return true;
        try
        {
            Character.Transact(Character.State.Revision,$"death:{run.Sequence}:{deathReceipt:N}",s=> { var failed=EndgameRules.FailLeg(s);return failed with { Fracture=failed.Fracture! with { ElapsedTicks=checked(failed.Fracture.ElapsedTicks+PlayerState.Tick) } }; });
            SaveProblem="";return true;
        }
        catch(Exception e) when(e is IOException or InvalidOperationException or UnauthorizedAccessException)
        {SaveProblem=e.Message;Hud.DeathSaveFailed();return false;}
    }
    public void RetryDeathSave() { deathShown=false; }
    public void StartSovereign(BigInteger tier,bool retry=false)
    {
        if(!retry && Playing || Character is null || !Character.State.FractureUnlocked || tier<1 || tier>Character.State.HighestUnlockedTier)return;
        specialAttempt=Guid.NewGuid();StartEncounter(3);SovereignActive=true;EncounterTier=tier;ApplyCharacterBuild(true);
        ClearEncounterEnemies();world.Configure([]);Spawn(EnemyRole.Bellkeeper,new(448,192));laws.Configure([],"mutation.anchor",ActivityFamily.Hunt);Hud.HideMenu();
    }
    private void CompleteSovereign()
    {
        Playing=false;
        if(!UpdateCharacter(s=>s with { SovereignRewards=s.SovereignRewards.Add("echo.anchor"),RunRecords=[..s.RunRecords.TakeLast(63),new(specialAttempt,s.ExpeditionSequence+1,EncounterTier,Region.Crown,ActivityFamily.Hunt,PlayerState.Tick,laws.Assisted,EndgameRules.Build(s),"Sovereign Echo")]})) { Hud.SovereignSaveFailed();return; }
        ReturnToTitle();Hud.Records();
    }
    public void RetrySovereignReward()=>CompleteSovereign();
    private void ClearEncounterEnemies()
    {
        foreach(var enemy in Enemies){enemy.CollisionLayer=0;enemy.CollisionMask=0;enemy.QueueFree();}Enemies.Clear();
    }
    private void ConfigureRegionalRoom(Expedition run)
    {
        ClearEncounterEnemies();
        var blocks=run.Layout!.Obstacles[FractureGroup];world.Configure([..blocks.Select(b=>new Rect2(b.X,b.Y,b.Width,b.Height))]);
        world.SetRegion(run.Region);
        if(run.ContentVersion==RegionalContent.ExpeditionVersion)
        {
            if(FractureGroup==6)Spawn(RegionalContent.Boss(run.Region,run.Rooms[6]%12-10),new(448,192));
            else
            {
                Spawn(RegionalContent.Enemy(run.Region,(FractureGroup+(int)run.Activity*2)%6),new(448,192));
                Spawn(RegionalContent.Enemy(run.Region,(FractureGroup+1+(int)run.Activity*2)%6),new(512,128));
                if(run.Activity==ActivityFamily.Breach)Spawn(RegionalContent.Enemy(run.Region,(FractureGroup+2)%6),new(512,264));
            }
        }
        else if(FractureGroup==6)Spawn(EnemyRole.Bellkeeper,new(448,192));
        else
        {
            Spawn(run.Activity==ActivityFamily.Hunt?EnemyRole.Brute:EnemyRole.Pursuer,new(448,192));
            Spawn(EnemyRole.Caster,new(512,128));
            if(run.Activity==ActivityFamily.Breach)Spawn(EnemyRole.Brute,new(512,264));
        }
        masteryEarned=run.MasteryEarned; laws.Configure(run.WorldRules,FractureGroup==6?run.Mutation:null,run.Activity,run.Assisted);
    }
    public bool EndgameObjectiveReady => !FractureActive || !RegionalContent.IsRegional(Character!.State.Fracture!.ContentVersion) || laws.ObjectiveReady;
    public string EndgameHint => CovenantChallengeActive?$"RED COVENANT {TrialStage+1}/3 / {(TrialStage==0?"Reach 20% Life reserved, then defeat guardians.":TrialStage==1?"Use Flask while hurt with Life reserved; defeat guardians.":"Defeat the boss with the reservoir tradeoff.")}":TrialActive?$"CONTRADICTION {TrialStage+1}/3 - Normalized power; no Override. {(TrialStage==0?"Land two Assault skills or signatures.":TrialStage==1?"Follow the safe lane.":"Defeat the remembering bell.")}":SovereignActive?"SOVEREIGN ECHO - Leave its return mark before the volley.":GraphFracture?WorldZone!.Objective:FractureActive&&RegionalContent.IsRegional(Character!.State.Fracture!.ContentVersion)?laws.Hint:"";
}
