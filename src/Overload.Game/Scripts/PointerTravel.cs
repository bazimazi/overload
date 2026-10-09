using Godot;
using Overload.Domain;

namespace Overload.Game;

public partial class Arena
{
    private System.Numerics.Vector2[] pointerPath = [];
    private int pointerStep;
    private Vector2 pointerLastPosition;
    private int pointerStillTicks;
    private bool pointerSteering;
    private long pointerRepathAt;
    public Vector2? PointerDestination { get; private set; }
    public Vector2 CursorWorldPosition => (Controls.PointerPosition - worldContainer.Position) / WorldScale + CameraOrigin;
    private void BeginPointerTravel()
    {
        if(!Playing||Paused||LocalMapVisible||PlayerState.Dead)return;
        pointerSteering=true;
        RequestWorldWalk(CursorWorldPosition);
    }
    public bool RequestWorldWalk(Vector2 point)
    {
        if(!Playing||Paused||PlayerState.Dead)return false;
        ClearPrimaryOrder();
        pendingLootPickup=null;
        var from=new System.Numerics.Vector2(Player.Position.X,Player.Position.Y);
        if(!world.Navigation.Clear(new(point.X,point.Y),new(point.X,point.Y),Player.Radius))return false;
        pointerPath=world.Navigation.FindPath(from,new(point.X,point.Y),Player.Radius+1.5f);
        if(pointerPath.Length==0)pointerPath=world.Navigation.FindPath(from,new(point.X,point.Y),Player.Radius);
        pointerStep=0;pointerStillTicks=0;pointerLastPosition=Player.Position;PointerDestination=pointerPath.Length>0?point:null;
        pointerRepathAt=PlayerState.Tick+8;
        return PointerDestination is not null;
    }
    private void ResetPointerTravel(){ClearPrimaryOrder();pendingLootPickup=null;pointerPath=[];pointerStep=0;pointerStillTicks=0;PointerDestination=null;pointerSteering=false;}
    private PlayerIntent PointerIntent(PlayerIntent intent)
    {
        if(intent.Move.LengthSquared()>.01f||Controls.Controller){ResetPointerTravel();return intent;}
        if(pointerSteering&&PlayerState.Tick>=pointerRepathAt&&(PointerDestination is null||CursorWorldPosition.DistanceSquaredTo(PointerDestination.Value)>10*10))
        { RequestWorldWalk(CursorWorldPosition);pointerRepathAt=PlayerState.Tick+8; }
        if(PointerDestination is not null)
        {
            pointerStillTicks=Player.Position.DistanceSquaredTo(pointerLastPosition)<.01f&&PlayerState.Action is null?pointerStillTicks+1:0;
            pointerLastPosition=Player.Position;
            if(pointerStillTicks>90){ResetPointerTravel();return intent;}
        }
        var from=new System.Numerics.Vector2(Player.Position.X,Player.Position.Y);
        // Skip redundant bends only when the whole footprint can pass the shortcut.
        while(pointerStep+1<pointerPath.Length&&world.Navigation.Clear(from,pointerPath[pointerStep+1],Player.Radius+1))pointerStep++;
        while(pointerStep<pointerPath.Length&&Player.Position.DistanceTo(new(pointerPath[pointerStep].X,pointerPath[pointerStep].Y))<(pointerStep==pointerPath.Length-1?.7f:2))pointerStep++;
        if(pointerStep>=pointerPath.Length){pointerPath=[];PointerDestination=null;return intent;}
        var target=pointerPath[pointerStep];var direction=new Vector2(target.X,target.Y)-Player.Position;
        var speed=LocomotionRules.ArrivalSpeed(direction.Length(),CurrentWalkSpeed,1f/60);
        return intent with {Move=direction.Normalized()*(speed/CurrentWalkSpeed)};
    }
}
