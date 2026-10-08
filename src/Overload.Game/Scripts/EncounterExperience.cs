using Godot;
using Overload.Domain;
using BigInteger = System.Numerics.BigInteger;

namespace Overload.Game;

public sealed record RoomSpoils(BigInteger Xp, BigInteger Gold, BigInteger Alloy, BigInteger PreviousLevel, GearItem? Item, bool Converted);

public partial class Arena
{
    public RoomSpoils? Spoils { get; private set; }
    public bool CheckpointRest { get; private set; }
    public static Vector2 CheckpointExit => new(560,192);
    public float EncounterSeconds { get; private set; }
    public int EncounterHitsDealt { get; private set; }
    public int EncounterHitsTaken { get; private set; }
    private bool coachedMotion, coachedPattern, coachedTraverse;
    private float coachingDistance;
    public string CombatNotice { get; private set; } = "";
    public float CombatNoticeTime { get; private set; }
    private string lastNotice = "";
    private long noticeTick;
    public static string RegionName(Region region) => region switch
    { Region.Ash => "Ash Foundry", Region.Glass => "Glass Marsh", Region.Hollow => "Hollow Archive", _ => "Crown Scar" };
    public string RoomNarrative => !JourneyActive ? "" : (Character!.State.RegionalCampaign ? JourneyRoom : 0) switch
    {
        0 => "The old laws still burn in the furnaces. Carry a different answer.",
        1 => "A guardian holds. A Manyborn learns when to move.",
        2 => "Every abandoned life leaves something worth remembering.",
        3 => "Silence the forge. Let Hearth breathe again.",
        4 => "Beneath the glass, another version of the world is waking.",
        5 => "The Regent reflects your certainty. Change your approach.",
        6 => "Walk carefully. Broken reflections remember the weight of a footstep.",
        7 => "The Widow guards what the marsh was asked to forget.",
        8 => "The Archivists kept every possibility. Even the forbidden ones.",
        9 => "The Indexer has no entry for a life like yours.",
        10 => "Stand still long enough to hear the unwritten pages.",
        11 => "The Abbot would close the book. Write one more line.",
        12 => "The crown was a promise that reality would never change.",
        13 => "The Marshal cannot command the lives you have already lost.",
        14 => "Beyond this scar, the First Pattern waits to erase you.",
        _ => "You are the contradiction. Earn the right to remain."
    };
    public string CombatLesson
    {
        get
        {
            if (!JourneyActive || !Audio.ShowTutorialHints) return "";
            if (JourneyRoom > 1) return JourneyLesson;
            if (!coachedMotion) return "MOVE TO REMEMBER  /  WASD or left stick builds Momentum while hostiles remain.";
            if (!coachedPattern) return $"MOMENTUM STORED  /  Aim, then {Controls.Glyph("cleave")} to turn your basic attack into Pursuit.";
            if (!coachedTraverse) return $"READ THE WARNING  /  {Controls.Glyph("evade")} moves through danger. A real evaded hit stores Echo.";
            return "TAKE THE OPENING  /  Strike after a windup. Active skills and heavy hits break stagger.";
        }
    }
    private void ResetExperience()
    {
        EncounterSeconds = 0; coachingDistance = 0; coachedMotion = coachedPattern = coachedTraverse = false;
        CheckpointRest = false;
        EncounterHitsDealt = EncounterHitsTaken = 0;
        CombatNotice = ""; CombatNoticeTime = 0; lastNotice = ""; noticeTick = -100; Spoils = null;
    }
    private void ObserveExperience(Vector2 displacement)
    {
        if (Enemies.Any(e => !e.Enemy!.Dead)) EncounterSeconds += 1 / 60f;
        EncounterHitsDealt = Effects.HitsDealt; EncounterHitsTaken = Effects.HitsTaken;
        coachingDistance += displacement.Length();
        coachedMotion |= PlayerState.Memories.Momentum is not null || PlayerState.Action?.ConsumedMemories.Any(m=>m.Type==MemoryType.Momentum)==true || coachingDistance >= Balance.Memories.MomentumDistancePixels;
        coachedPattern |= PlayerState.Action is { Implementation: not ActionImplementation.Base };
        coachedTraverse |= PlayerState.Action?.Definition.Id == SkillId.Traverse;
        CombatNoticeTime = Math.Max(0, CombatNoticeTime - 1 / 60f);
    }
    private void NoticeRejectedAction()
    {
        var reason = PlayerState.LastReason;
        if (reason.Contains("Action", StringComparison.OrdinalIgnoreCase) || reason.Contains("commit",StringComparison.OrdinalIgnoreCase)) return;
        if (reason == lastNotice && PlayerState.Tick - noticeTick < 90) return;
        CombatNotice = reason; CombatNoticeTime = 1.5f; lastNotice = reason; noticeTick = PlayerState.Tick;
    }
    public void EquipSpoilsAndContinue()
    {
        if (Spoils?.Item is not { } item || Playing) return;
        if (UpdateCharacter(s => EquipmentRules.Equip(s,item.Id))) ContinueJourney(); else Hud.RoomCleared();
    }
    public void InspectCheckpoint()
    { if (!CheckpointRest) return; Paused=true;Audio.SetPaused(true);Hud.RoomCleared(); }
    private void AdvanceCheckpoint()
    {
        if (Paused) return;
        PlayerState.AdvanceClock(); Player.BeginTick();
        var mouse=(Controls.PointerPosition-worldContainer.Position)/worldContainer.Scale;
        var intent=Controls.Read(PlayerState.Tick,Player.Position,mouse);
        Player.Facing=intent.Aim;Player.Move(intent.Move*Balance.Hero.Speed);Player.PublishPose(PlayerState);
        if (Input.IsActionJustPressed("pulse") && Spoils?.Item is not null) EquipSpoilsAndContinue();
        else if (Input.IsActionJustPressed("interact") || Player.Position.DistanceTo(CheckpointExit)<18) ContinueJourney();
    }
}

public partial class ArenaHud
{
    private HBoxContainer checkpointButtons = null!;
    private Button continueCheckpoint = null!, equipCheckpoint = null!;
    private void InitializeCheckpointButtons()
    {
        checkpointButtons=new HBoxContainer { Alignment=BoxContainer.AlignmentMode.Center,MouseFilter=MouseFilterEnum.Ignore,Visible=false };
        checkpointButtons.AddThemeConstantOverride("separation",12);AddChild(checkpointButtons);
        continueCheckpoint=new Button { CustomMinimumSize=new(260,42) };
        equipCheckpoint=new Button { CustomMinimumSize=new(260,42) };
        checkpointButtons.AddChild(continueCheckpoint);checkpointButtons.AddChild(equipCheckpoint);
        continueCheckpoint.Pressed+=arena.ContinueJourney;equipCheckpoint.Pressed+=arena.EquipSpoilsAndContinue;
    }
    private void UpdateCheckpointButtons()
    {
        var visible=arena.CheckpointRest&&!MenuVisible;
        var gained=visible&&!checkpointButtons.Visible;
        checkpointButtons.Visible=visible;
        checkpointButtons.Position=new(20,Size.Y-64);checkpointButtons.Size=new(Size.X-40,44);
        continueCheckpoint.Text=$"Continue [{arena.Controls.Glyph("interact")}]";
        equipCheckpoint.Text=$"Equip reward & continue [{arena.Controls.Glyph("pulse")}]";
        equipCheckpoint.Visible=arena.Spoils?.Item is not null;
        if(gained)continueCheckpoint.GrabFocus();
    }
    private void DrawCheckpointHud()
    {
        var y=Size.Y-194;
        Surface(new(18,y,Size.X-36,176),new Color("8b7956"),.96f);
        var level=arena.Spoils is { } gained&&arena.Character!.State.ValidatedLevel>gained.PreviousLevel ? $" / LEVEL {CounterText.Short(arena.Character.State.ValidatedLevel)}" : "";
        CenterWrite(new(Size.X/2,y+25),"MEMORY RESTORED / CHECKPOINT SAVED"+level,13,gold);
        CenterWrite(new(Size.X/2,y+54),arena.JourneyName.ToUpperInvariant(),22,ink);
        if (arena.Spoils is { } reward)
        {
            CenterWrite(new(Size.X/2,y+80),$"+{CounterText.Short(reward.Xp)} XP   /   +{CounterText.Short(reward.Gold)} gold   /   +{CounterText.Short(reward.Alloy)} Alloy",13,gold);
            var item=reward.Item;
            CenterWrite(new(Size.X/2,y+102),Fit(item is not null ? $"{item.Name} / {item.BaseKind} +{item.BaseValue} / {arena.Controls.Glyph("pause")} to compare & review" : "Life, Focus and flasks restored. Walk into the gate to continue.",Size.X-100,12),12,muted);
        }
        CenterWrite(new(Size.X/2,40),Fit(arena.RoomNarrative,Size.X-100,14),14,ink);
        CenterWrite(new(Size.X/2,65),"Walk into the gate, continue here, or return to Hearth from pause.",12,muted);
    }
    private void SpoilsCard(RoomSpoils reward)
    {
        Text($"+{CounterText.Short(reward.Xp)} XP     +{CounterText.Short(reward.Gold)} gold     +{CounterText.Short(reward.Alloy)} Alloy",19,gold);
        var state = arena.Character!.State;
        if (state.ValidatedLevel > reward.PreviousLevel)
        {
            Text($"LEVEL {CounterText.Short(reward.PreviousLevel)}  →  {CounterText.Short(state.ValidatedLevel)}",23,ink);
            var skillPoints = FoundationRules.SkillBudget(state)-FoundationRules.SkillSpent(state);
            var talentPoints = FoundationRules.TalentBudget(state)-state.Talents.Count;
            if (skillPoints > 0 || talentPoints > 0) Text($"Available at Hearth: {skillPoints} skill points / {talentPoints} talent points",14,muted);
        }
        if (reward.Item is { } item)
        {
            var equipped = state.Equipment.TryGetValue(item.Slot,out var id) ? EquipmentRules.Find(state,id) : null;
            Text(item.Name.ToUpperInvariant(),21,gold);
            Text($"{item.Slot} / {item.BaseKind}: {equipped?.BaseValue ?? 0} → {item.BaseValue}\n{string.Join(" · ", item.Affixes.Select(a => $"{a.Kind} +{a.Value}"))}",15,ink);
            AddButton("Equip reward & continue",arena.EquipSpoilsAndContinue);
        }
        else if (reward.Converted) Text("Bag full: this drop became 25 gold and 5 Alloy.",14,muted);
    }
}
