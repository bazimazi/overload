using Godot;
using Overload.Domain;

namespace Overload.Game;
public partial class Arena
{
    public bool JourneyActive { get; private set; }
    public int JourneyRoom { get; private set; }
    public string RegionTitle => FractureActive ? Character!.State.Fracture!.Region.ToString().ToUpperInvariant() : JourneyActive&&Character!.State.RegionalCampaign ? ((Region)(JourneyRoom/4)).ToString().ToUpperInvariant() : world.RegionName;
    public string JourneyName => Character!.State.RegionalCampaign ? RegionalContent.Rooms.Single(r=>r.Id==JourneyRoom/4*12+new[]{0,10,4,11}[JourneyRoom%4]).Name : RoomCatalog.Rooms[JourneyRoom].Name;
    public string JourneyLesson => Character!.State.RegionalCampaign ? new[]{"Read amber tells. A free basic attack always remains available.","The enforcer locks its aim. Strike during recovery.","Explore another room; preserve a clear return route.","Watch the regional ruler and its marked hazard."}[JourneyRoom%4] : RoomCatalog.Rooms[JourneyRoom].Lesson;
    public Rect2[] CollisionWalls => world.CurrentWalls;
    public void StartJourney()
    {
        if (Character is null) return;
        if (Character.State.RunId == Guid.Empty || Character.State.CheckpointRoom == JourneyRules.Length(Character.State))
            if (!UpdateCharacter(s => JourneyRules.Begin(s, Guid.NewGuid()))) { Hud.Title(); return; }
        EnterJourneyRoom(Character.State.CheckpointRoom);
    }
    private void EnterJourneyRoom(int room)
    {
        ApplyCharacterBuild();
        StartEncounter(0); JourneyActive = true; JourneyRoom = room;
        foreach (var enemy in Enemies) { enemy.CollisionLayer = 0; enemy.CollisionMask = 0; enemy.QueueFree(); }
        Enemies.Clear();
        if(Character!.State.RegionalCampaign)
        {
            var region=(Region)(room/4);var local=room%4;var template=RegionalContent.Rooms.Single(r=>r.Id==(int)region*12+new[]{0,10,4,11}[local]);
            world.SetRegion(region);world.Configure([..template.Obstacles.Select(b=>new Rect2(b.X,b.Y,b.Width,b.Height))]);
            if(local is 1 or 3)Spawn(RegionalContent.Boss(region,local==1?0:1),new(448,192));
            else for(var i=0;i<3;i++)Spawn(RegionalContent.Enemy(region,local==0?i:i+3),new[]{new Vector2(448,192),new Vector2(512,128),new Vector2(512,264)}[i]);
            laws.Configure([region switch { Region.Ash=>"law.vents",Region.Glass=>"law.glass",Region.Hollow=>"law.watch",_=>"law.crown" }],null,ActivityFamily.Hunt);
            Effects.Record(JourneyName);Hud.HideMenu();return;
        }
        var definition = RoomCatalog.Rooms[room]; world.Configure(definition.Obstacles);
        if (room == 7) Audio.Play("bell", "Enemy");
        foreach (var spawn in definition.Spawns) Spawn(spawn.Role, spawn.Position);
        Effects.Record(definition.Name); Hud.HideMenu();
    }
    public void RetryCurrentRoom() { if(TrialActive||SovereignActive) RetrySpecial();else if (FractureActive) EnterFractureGroup(); else if (JourneyActive) EnterJourneyRoom(JourneyRoom); else StartEncounter(Wave); }
    private void CompleteJourneyRoom()
    {
        Playing = false; PlayerState.Reset(); Controls.ClearBuffer(); Effects.Reset();
        try
        {
            var s = Character!.State;
            Character.Transact(s.Revision, $"room:{s.RunId:N}:{JourneyRoom}", c => JourneyRules.Clear(c, s.RunId, JourneyRoom));
            var after = Character.State;
            Spoils = new(after.TotalXp-s.TotalXp,after.Gold-s.Gold,after.Alloy-s.Alloy,s.ValidatedLevel,
                after.Inventory.FirstOrDefault(i => !s.Inventory.Any(old => old.Id == i.Id)),s.Inventory.Length >= EquipmentRules.Capacity);
            Audio.Play("reward", "UI");
            SaveProblem = "";
            if (after.CheckpointRoom < JourneyRules.Length(after))
            { CheckpointRest=true;Paused=false;world.OpenCheckpointExit();Hud.HideMenu(); }
            else Hud.RoomCleared();
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or UnauthorizedAccessException)
        { SaveProblem = e.Message; Hud.RoomSaveFailed(); }
    }
    public void RetryRoomSave() => CompleteJourneyRoom();
    public void ContinueJourney() { if (Character!.State.CheckpointRoom == JourneyRules.Length(Character.State)) ReturnToTitle(); else EnterJourneyRoom(Character.State.CheckpointRoom); }
}
public partial class ArenaHud
{
    public void RoomCleared()
    {
        var end = arena.JourneyRoom == JourneyRules.Length(arena.Character!.State)-1;
        ClearMenu(end ? arena.Character!.State.RegionalCampaign?"THE FIRST PATTERN BREAKS":"THE BELL FALLS SILENT" : "CHECKPOINT SAVED", end ? arena.Character!.State.RegionalCampaign?"The Pattern unravels":"Court restored" : arena.JourneyName + " cleared",
            end ? arena.Character!.State.RegionalCampaign?"Ash cools. Glass clears. The Archive opens its sealed pages. Beyond the broken crown, the Fracture Atlas offers another path.":"Eight rooms complete. The Fracture board and shared attunement are now open at Hearth." : "Rewards banked. Life, Focus and flasks restored for the next room.");
        AddButton(end ? "Exit to Hearth" : "Continue to next room", arena.ContinueJourney, true);
        if (arena.Spoils is { } reward) SpoilsCard(reward);
        else Text(arena.Character!.State.Journal.Last(), 15, gold);
        Text($"{arena.EncounterSeconds:0.0}s / {arena.EncounterHitsDealt} hits landed / {arena.EncounterHitsTaken} hits taken",13,muted);
        if (!string.IsNullOrEmpty(arena.SaveProblem)) Text(arena.SaveProblem,14,gold);
        if (!end) AddButton("Visit Hearth — retain checkpoint", arena.ReturnToTitle);
        if (arena.CheckpointRest) AddButton("Return to the restored room",arena.TogglePause);
        goBack = arena.CheckpointRest ? arena.TogglePause : arena.ReturnToTitle;
    }
    public void RoomSaveFailed()
    {
        ClearMenu("REWARD SAVE INCOMPLETE", "Retry checkpoint", arena.SaveProblem);
        AddButton("Retry saving rewards", arena.RetryRoomSave, true);
        AddButton("Hearth (unsaved room must be replayed)", arena.ReturnToTitle);
    }
}
