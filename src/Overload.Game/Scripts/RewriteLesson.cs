using Godot;
using Overload.Domain;

namespace Overload.Game;

/// <summary>Real action authority in a reward-free practice session; no character transactions.</summary>
public partial class Arena
{
    public bool RewriteLessonActive {get;private set;}
    public int RewriteLessonStep {get;private set;}
    private long lessonAction=-1;
    private bool lessonPlaced;
    private BalanceProfile? lessonReturnBalance;
    private PlayerCombat? lessonReturnCombat;
    public string RewriteLessonTitle=>RewriteLessonStep switch
    {0=>"ONE ACTION",1=>"MOVE TO REMEMBER",2=>"OVERLOAD YOUR ASSAULT",3=>"CHOOSE WHERE TO SPEND",4=>"REWRITE THE RULE",5=>"YOUR EVADE IS AN ANCHOR",_=>"YOU CHANGED HOW YOU ACT"};
    public string RewriteLessonHint=>RewriteLessonStep switch
    {
        0=>$"Approach the sentinel. Hold {Controls.Glyph("preserve")} and attack with {Controls.Glyph("cleave")} to see your base action.",
        1=>"Circle the sentinel. Travel three meters to store Momentum.",
        2=>$"Release {Controls.Glyph("preserve")}, aim at the sentinel and attack. The same input now becomes Pursuit.",
        3=>$"Move to store Momentum again. Hold {Controls.Glyph("preserve")} while attacking to keep it; release and use {Controls.Glyph("evade")} for Crossing.",
        4=>$"Press {Controls.Glyph("interact")} to try Elsewhere. Override replaces your evade's rule.",
        5=>!lessonPlaced?$"{Controls.Glyph("evade")}: mark a safe position. Placement gives no evasion.":$"Move away, then {Controls.Glyph("evade")} again: return to your anchor. The roll is gone.",
        _=>"Overload changes an action with a memory. Override changes the rule behind that action."
    };
    public void StartRewriteLesson()
    {
        if(Playing||Character is null)return;
        lessonReturnBalance=Balance;lessonReturnCombat=PlayerState;
        ApplyCharacterBuild(true);
        Balance=Balance with{Overload=Balance.Overload with{BindingSlots=3,Bindings=[new("pattern.assault.pursuit",0),new("pattern.assault.afterstrike",1),new("pattern.traverse.crossing",2)]}};
        PlayerState=new(Balance,new ArenaActionPreflight(this),Character.State);
        StartEncounter(0);ClearEncounterEnemies();world.Configure([]);
        RewriteLessonActive=true;RewriteLessonStep=0;lessonAction=-1;lessonPlaced=false;
        Player.Position=new(240,200);Player.TeleportVisual();
        Spawn(BaseBalance.Enemies.Single(e=>e.Role==EnemyRole.Pursuer) with {Name="Memory sentinel",Life=1000000,Speed=0,Damage=0,Reach=24,Windup=90},new(380,200));
        ArrivalTime=0;PublishPredictions(Vector2.Right,Vector2.Zero);
    }
    private void EndRewriteLesson()
    {
        if(!RewriteLessonActive)return;
        if(lessonReturnBalance is not null&&lessonReturnCombat is not null){Balance=lessonReturnBalance;PlayerState=lessonReturnCombat;}
        lessonReturnBalance=null;lessonReturnCombat=null;RewriteLessonActive=false;
    }
    private void AdvanceRewriteLesson()
    {
        if(!RewriteLessonActive)return;
        if(RewriteLessonStep==0&&Effects.RecentEvents.Any(e=>e.Kind=="hit"&&e.Source==SourceKind.BasePlayerAction))RewriteLessonStep=1;
        if(RewriteLessonStep==1&&PlayerState.Memories.Momentum is not null)RewriteLessonStep=2;
        var action=PlayerState.Action;
        if(action is not null&&lessonAction!=action.RootActionId)
        {
            lessonAction=action.RootActionId;
            if(RewriteLessonStep==2&&action.Implementation==ActionImplementation.Pursuit)RewriteLessonStep=3;
            else if(RewriteLessonStep==3&&action.Implementation==ActionImplementation.Crossing)RewriteLessonStep=4;
            else if(RewriteLessonStep==5&&action.Traversal==TraversePhase.AnchorPlace)lessonPlaced=true;
            else if(RewriteLessonStep==5&&action.Traversal==TraversePhase.AnchorSwap){RewriteLessonStep=6;Paused=true;Controls.ClearBuffer();Audio.SetPaused(true);Hud.RewriteLessonComplete();}
        }
        if(RewriteLessonStep==4&&Input.IsActionJustPressed("interact"))ActivateLessonOverride();
    }
    public void ActivateLessonOverride()
    {
        if(!RewriteLessonActive||RewriteLessonStep!=4)return;
        PlayerState.Reset();PlayerState.TryChangeOaths(["oath.elsewhere"]);RewriteLessonStep=5;lessonAction=-1;
        Effects.Record("OVERRIDE / Traverse now places and returns to an anchor");
    }
}

public partial class ArenaHud
{
    public void RewriteLessonComplete()
    {
        ClearMenu("MEMORY CHAMBER / PRACTICE COMPLETE","You rewrote the rule","One attack became a lunge. The same memory could power movement instead. Then your evade became placement and return.");
        Text("This chamber grants no XP, equipment or oath ownership. Your saved build returns when you leave.",16,muted);
        AddButton("Explore the world",()=>{arena.ReturnToTitle();arena.StartWorld();},true);
        AddButton("Practice again",()=>{arena.ReturnToTitle();arena.StartRewriteLesson();});
        AddButton("Return to Hearth",arena.ReturnToTitle);goBack=arena.ReturnToTitle;
    }
}
