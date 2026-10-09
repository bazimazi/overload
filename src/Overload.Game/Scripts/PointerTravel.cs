using Godot;
using Overload.Domain;

namespace Overload.Game;

public partial class Arena
{
    private System.Numerics.Vector2[] pointerPath = [];
    private int pointerStep;
    private Vector2 pointerLastPosition;
    private int pointerStillTicks;
    public Vector2? PointerDestination { get; private set; }
    public Vector2 CursorWorldPosition => (Controls.PointerPosition - worldContainer.Position) / worldContainer.Scale + CameraOrigin;
    private void BeginPointerTravel()
    {
        if(!Playing||Paused||LocalMapVisible||PlayerState.Dead)return;
        var point=CursorWorldPosition;var from=new System.Numerics.Vector2(Player.Position.X,Player.Position.Y);
        if(!world.Navigation.Clear(new(point.X,point.Y),new(point.X,point.Y),Player.Radius))return;
        pointerPath=world.Navigation.FindPath(from,new(point.X,point.Y),Player.Radius);
        pointerStep=0;pointerStillTicks=0;pointerLastPosition=Player.Position;PointerDestination=pointerPath.Length>0?point:null;
    }
    private void ResetPointerTravel(){pointerPath=[];pointerStep=0;pointerStillTicks=0;PointerDestination=null;}
    private PlayerIntent PointerIntent(PlayerIntent intent)
    {
        if(intent.Move.LengthSquared()>.01f||Controls.Controller){ResetPointerTravel();return intent;}
        if(PointerDestination is not null)
        {
            pointerStillTicks=Player.Position.DistanceSquaredTo(pointerLastPosition)<.01f&&PlayerState.Action is null?pointerStillTicks+1:0;
            pointerLastPosition=Player.Position;
            if(pointerStillTicks>90){ResetPointerTravel();return intent;}
        }
        while(pointerStep<pointerPath.Length&&Player.Position.DistanceTo(new(pointerPath[pointerStep].X,pointerPath[pointerStep].Y))<5)pointerStep++;
        if(pointerStep>=pointerPath.Length){ResetPointerTravel();return intent;}
        var target=pointerPath[pointerStep];var direction=new Vector2(target.X,target.Y)-Player.Position;
        return intent with {Move=direction.Normalized()*Math.Min(1,direction.Length()*60/Balance.Hero.Speed)};
    }
}
