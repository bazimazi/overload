using Godot;
using Overload.Domain;

namespace Overload.Game;

public partial class Arena
{
    private ActorBody? primaryEnemy;
    private bool primaryWalkHeld, primaryAttackQueued;
    private string? primaryInteraction;
    public bool PrimaryTargeting => primaryEnemy is not null;
    public ActorBody? PrimaryTarget => primaryEnemy is {Enemy.Dead:false}?primaryEnemy:null;
    private ActorBody? MouseEnemy(Vector2 cursor, bool forgiving = false)
    {
        return Enemies.Where(e=>!e.Enemy!.Dead&&WorldCellKnown(e.Position)
            && new Rect2(CameraOrigin,new(640,360)).HasPoint(e.Position)
            && (new Rect2(e.Position-new Vector2(23,57),new(46,73)).HasPoint(cursor)
                || forgiving&&EngagedEnemy(e)&&e.Position.DistanceTo(cursor)<96)
            && (!forgiving||world.Navigation.Clear(new(Player.Position.X,Player.Position.Y),new(e.Position.X,e.Position.Y),0)))
            .OrderBy(e=>e.Position.DistanceSquaredTo(cursor)).ThenBy(e=>e.ActorId).FirstOrDefault();
    }
    private void ClearPrimaryOrder()
    { primaryEnemy=null;primaryWalkHeld=primaryAttackQueued=false;primaryInteraction=null; }
    private bool TryPrimaryMouse()
    {
        if(!AdventureActive||Paused||LocalMapVisible||Input.IsActionPressed("stand_ground"))return false;
        var cursor=CursorWorldPosition;
        var enemy=MouseEnemy(cursor);
        ResetPointerTravel();Controls.SuppressPrimaryUntilRelease(false);
        if(enemy is not null){primaryEnemy=enemy;primaryAttackQueued=true;pointerRepathAt=0;return true;}
        var target=KnownWorldTargets().Where(t=>t.Kind is "npc" or "waypoint" or "refuge" or "device" or "cache" or "exit")
            .Where(t=>new Rect2(t.Position-new Vector2(55,62),new(110,80)).HasPoint(cursor))
            .OrderBy(t=>t.Position.DistanceSquaredTo(cursor)).FirstOrDefault();
        if(target is not null&&RequestWorldWalk(target.Position)){primaryInteraction=target.Id;return true;}
        if(RequestPrimaryGround(cursor))primaryWalkHeld=true;
        return true;
    }
    private bool RequestPrimaryGround(Vector2 cursor)
    {
        if(RequestWorldWalk(cursor))return true;
        // Clicking scenery still gives a useful order: stop on nearby reachable ground.
        for(var distance=16;distance<=96;distance+=16)
            for(var side=0;side<8;side++)
                if(RequestWorldWalk(cursor+Vector2.FromAngle(side*Mathf.Tau/8)*distance))return true;
        WorldNotice("That ground is blocked. Choose a clear path.");return false;
    }
    private PlayerIntent PrimaryIntent(PlayerIntent intent)
    {
        if(!AdventureActive)return intent;
        if(Input.IsActionPressed("stand_ground")){ResetPointerTravel();return intent;}
        if(intent.Move.LengthSquared()>.01f||Controls.Controller){ClearPrimaryOrder();return intent;}
        if(primaryWalkHeld&&PlayerState.Tick>=pointerRepathAt)
        {
            var hovered=MouseEnemy(CursorWorldPosition);
            if(hovered is not null){primaryEnemy=hovered;primaryAttackQueued=true;primaryWalkHeld=false;}
            else {RequestPrimaryGround(CursorWorldPosition);primaryWalkHeld=Input.IsActionPressed("cleave");pointerRepathAt=PlayerState.Tick+8;}
        }
        if(primaryInteraction is { } id)
        {
            var target=KnownWorldTargets().FirstOrDefault(t=>t.Id==id);
            if(target is null){ResetPointerTravel();return intent;}
            if(Player.Position.DistanceTo(target.Position)<56)
            {ResetPointerTravel();InteractWorld();return intent with {Action=null,Move=Vector2.Zero};}
        }
        if(primaryEnemy is not null&&Input.IsActionPressed("cleave")&&MouseEnemy(CursorWorldPosition) is { } pointed&&pointed!=primaryEnemy)
        {primaryEnemy=pointed;primaryAttackQueued=true;pointerRepathAt=0;}
        if(primaryEnemy is not { } enemy)return intent;
        if(enemy.Enemy!.Dead)
        {
            var next=Input.IsActionPressed("cleave")?MouseEnemy(CursorWorldPosition,true):null;
            ResetPointerTravel();
            if(next is null)return intent with {Action=intent.Action==SkillId.Cleave?null:intent.Action};
            primaryEnemy=enemy=next;primaryAttackQueued=true;pointerRepathAt=0;
        }
        var delta=enemy.Position-Player.Position;var aim=delta.Normalized();
        var actionAim=intent.Action is null or SkillId.Cleave?aim:intent.Aim;
        var basic=Balance.Skills.Single(s=>s.Id==FrameRules.Basic(Character!.State.Frame));
        var reach=basic.Reach*.78f;
        var sight=world.Navigation.Clear(new(Player.Position.X,Player.Position.Y),new(enemy.Position.X,enemy.Position.Y),0);
        if(delta.Length()<=reach&&sight)
        {
            pointerPath=[];PointerDestination=null;primaryWalkHeld=false;
            var requested=intent.Action;
            if(requested is SkillId.ShieldPulse or SkillId.ChainLance or SkillId.Faultline&&Input.IsActionPressed("cleave"))
            {
                var equipped=EquippedAction(requested.Value);var skill=Balance.Skills.Single(s=>s.Id==equipped);
                if(PlayerState.Cooldown(equipped)>0||!PlayerState.RedCovenantActive&&PlayerState.Focus<skill.FocusCost*1000)
                {requested=SkillId.Cleave;actionAim=aim;}
            }
            return intent with {Aim=actionAim,Action=requested??(primaryAttackQueued||Input.IsActionPressed("cleave")?SkillId.Cleave:null)};
        }
        if(PlayerState.Tick>=pointerRepathAt)
        {
            var order=primaryAttackQueued;
            // Pick a clear approach point rather than trying to occupy an enemy's collision body.
            var approach=enemy.Position-aim*(reach*.88f);
            var found=RequestWorldWalk(approach);
            if(!found)for(var i=0;i<12&&!found;i++)found=RequestWorldWalk(enemy.Position+Vector2.FromAngle(i*Mathf.Tau/12)*(reach*.85f));
            primaryEnemy=enemy;primaryAttackQueued=order;pointerRepathAt=PlayerState.Tick+10;
            if(!found){ClearPrimaryOrder();WorldNotice("No clear approach. Move around the obstacle.");}
        }
        return intent with {Aim=actionAim,Action=intent.Action==SkillId.Cleave?null:intent.Action};
    }
    private void PrimaryActionStarted(SkillId skill)
    {
        if(skill!=FrameRules.Basic(Character?.State.Frame??FrameId.Warden))return;
        primaryAttackQueued=false;
        if(!Input.IsActionPressed("cleave"))primaryEnemy=null;
    }
}
