using Godot;
using Overload.Domain;
using BigInteger = System.Numerics.BigInteger;

namespace Overload.Game;
public partial class Arena
{
    public bool FractureActive { get; private set; }
    public BigInteger EncounterTier { get; private set; } = 1;
    public int FractureGroup { get; private set; }
    private Guid activeExpeditionId;
    private BigInteger activeExpeditionSequence;
    private long completedGroupTicks;
    public void StartFracture(BigInteger tier)
    {
        if (Playing || Character is null) return;
        if (Character.State.Fracture is not { Completed: false })
            if (!UpdateCharacter(s => EndlessRules.Begin(s, tier, Guid.NewGuid()))) { Hud.Fractures(); return; }
        EnterFractureGroup();
    }
    private void EnterFractureGroup()
    {
        var run = Character!.State.Fracture!;
        if (run.Completed) { ReturnToTitle(); return; }
        if(run.Map is not null){EnterFractureMap();return;}
        StartEncounter(0);
        FractureActive = true; EncounterTier = run.Tier; FractureGroup = run.NextGroup;
        completedGroupTicks=0;
        activeExpeditionId = run.Id; activeExpeditionSequence = run.Sequence;
        ApplyCharacterBuild(true);
        foreach (var enemy in Enemies) { enemy.CollisionLayer = 0; enemy.CollisionMask = 0; enemy.QueueFree(); }
        Enemies.Clear();
        var room = RoomCatalog.Rooms[run.ContentVersion==RegionalContent.ExpeditionVersion?0:run.Rooms[FractureGroup]];
        world.Configure(room.Obstacles);
        foreach (var spawn in room.Spawns) Spawn(spawn.Role, spawn.Position);
        if(RegionalContent.IsRegional(run.ContentVersion)) ConfigureRegionalRoom(run);
        if (FractureGroup == 6) Audio.Play("bell", "Enemy");
        Hud.HideMenu();
    }
    private void CompleteFractureGroup()
    {
        if(PlayerState.Tick>0)completedGroupTicks=PlayerState.Tick;
        var completedTicks=completedGroupTicks;
        Playing = false; PlayerState.Reset(); Controls.ClearBuffer(); Effects.Reset();
        try
        {
            Character!.Transact(Character.State.Revision, $"fracture:{activeExpeditionSequence}:{activeExpeditionId:N}:{FractureGroup}",
                s => EndlessRules.Claim(RegionalContent.IsRegional(s.Fracture!.ContentVersion)?s with { Fracture=s.Fracture with { MasteryEarned=s.Fracture.MasteryEarned||masteryEarned,ElapsedTicks=checked(s.Fracture.ElapsedTicks+completedTicks) } }:s, activeExpeditionId, activeExpeditionSequence, FractureGroup));
            SaveProblem = ""; Hud.FractureCleared();
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or UnauthorizedAccessException)
        { SaveProblem = e.Message; Hud.FractureSaveFailed(); }
    }
    public void RetryFractureSave() => CompleteFractureGroup();
    public void ContinueFracture() { if (Character!.State.Fracture!.Completed) ReturnToTitle(); else EnterFractureGroup(); }
}
