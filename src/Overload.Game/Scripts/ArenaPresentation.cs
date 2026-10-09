using Godot;

namespace Overload.Game;

public partial class Arena
{
    private Camera2D viewCamera = null!;
    private float shakeTime, shakePower, presentationTime;
    private string presentedRoom = "";
    private int cameraTeleport = -1;
    private string cameraRoom = "";
    private Vector2 cameraFollow, cameraLook;
    public float ArrivalTime { get; private set; }
    public string ArrivalTitle => WorldActive ? WorldZone.Name : JourneyActive ? Character!.State.RegionalCampaign && JourneyRoom % 4 == 0 ? RegionName((Overload.Domain.Region)(JourneyRoom/4)) : JourneyName : FractureActive ? RegionName(Character!.State.Fracture!.Region) + " / FRACTURE " + Overload.Domain.CounterText.Short(EncounterTier) : Wave == 3 ? EncounterBossName : "The Broken Court";
    public void Impact(float power) { if (Audio.ReducedFlash) return; shakeTime = .14f; shakePower = Math.Min(2.5f, Math.Max(shakePower, power)); }
    public void PresentHearth()
    {
        if (Playing) return;
        world.SetHearth(); world.Configure([]);
        Audio.SetHearth();
        Player.SetFrame(Character?.State.Frame ?? Overload.Domain.FrameId.Warden);
        Player.Position = new(430,224); Player.Velocity = Vector2.Zero; Player.Facing = Vector2.Down; Player.TeleportVisual();
    }
    private void RenderPresentation(double delta)
    {
        var dt = (float)Math.Min(delta, .05);
        var presentationPaused=Paused||LocalMapVisible;
        foreach (var body in Enemies.Append(Player)) body.Render(delta, presentationPaused);
        if(WorldActive)
        {
            var snap = cameraTeleport != Player.TeleportVersion || cameraRoom != RoomId;
            if(snap)
            { cameraFollow=Player.VisualPosition;cameraLook=Vector2.Zero;cameraTeleport=Player.TeleportVersion;cameraRoom=RoomId; }
            else if(!presentationPaused)
            {
                cameraLook=cameraLook.Lerp((Player.ActualVelocity*.06f).LimitLength(14),1-MathF.Exp(-10*dt));
                cameraFollow=cameraFollow.Lerp(Player.VisualPosition+cameraLook,1-MathF.Exp(-20*dt));
            }
            var b=WorldZone.Geometry.Bounds;
            viewCamera.Position=new(Math.Clamp(cameraFollow.X,b.X+320,b.X+b.Width-320),Math.Clamp(cameraFollow.Y,b.Y+180,b.Y+b.Height-180));
            world.Viewer=viewCamera.Position;
        }
        else { viewCamera.Position=new(320,180);cameraRoom=""; }
        if (!presentationPaused) presentationTime += dt;
        if (RoomId != presentedRoom && Playing) { presentedRoom = RoomId; ArrivalTime = 2.6f; }
        if (!presentationPaused) ArrivalTime = Math.Max(0, ArrivalTime - dt);
        if (!presentationPaused) shakeTime = Math.Max(0, shakeTime - dt);
        if (shakeTime <= 0 || presentationPaused || Audio.ReducedFlash) { shakePower = 0; viewCamera.Offset = Vector2.Zero; }
        else viewCamera.Offset = new Vector2(MathF.Sin(presentationTime * 110), MathF.Cos(presentationTime * 135)).Round() * MathF.Ceiling(shakePower * shakeTime / .14f);
        viewCamera.ForceUpdateScroll();
        world.Render(delta, presentationPaused); Effects.RenderJuice(delta, presentationPaused);
        Audio.SetEncounter(Playing && HasEngagedEnemies,Enemies.Any(e=>EngagedEnemy(e)&&e.Enemy is { Dead:false,Definition.Role:Overload.Domain.EnemyRole.Bellkeeper }));
    }
}
