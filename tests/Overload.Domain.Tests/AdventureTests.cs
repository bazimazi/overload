using System.Collections.Immutable;
using System.Numerics;
using Overload.Content;
using Overload.Domain;
using Xunit;

namespace Overload.Domain.Tests;

public sealed class AdventureTests
{
    private static CharacterState Fresh(FrameId frame=FrameId.Warden)=>AdventureRules.Enable(WorldRules.Enroll(FrameRules.Create(frame)));
    private static CharacterState Patrol(CharacterState s)
    {s=WorldRules.Enter(s,"ash.0",new(420,1680));return WorldRules.ClaimEncounter(s,s.World!.CampaignId,"ash.0","ash.0.pack.0");}
    private static BalanceProfile Basis()=>ExpansionLoader.Load(ProfileLoader.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"arena.json"))),File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"expansion.json")));
    [Theory] [InlineData(FrameId.Warden)] [InlineData(FrameId.Threadseer)] [InlineData(FrameId.Revenant)]
    public void FirstPatrolCreatesARealUpgradeAndGuidesCollectEquipAndTrain(FrameId frame)
    {
        var s=Fresh(frame);Assert.Equal("meet",AdventureRules.Objective(s)!.Id);
        s=AdventureRules.Accept(s);Assert.Equal("road",AdventureRules.Objective(s)!.Id);
        s=Patrol(s);Assert.Equal(2,s.ValidatedLevel);Assert.Equal("loot",AdventureRules.Objective(s)!.Id);
        var drop=Assert.Single(s.World!.Adventure!.GroundLoot);Assert.Equal(GearSlot.Weapon,drop.Item.Slot);
        Assert.True(drop.Item.BaseValue>EquipmentRules.StarterItems[0].BaseValue);Assert.Equal(6,s.Inventory.Length);
        s=AdventureRules.PickUp(s,drop.Item.Id,drop.Position);Assert.Equal("equip",AdventureRules.Objective(s)!.Id);
        var before=new PlayerCombat(FoundationRules.Build(Basis(),s),character:s);
        s=AdventureRules.ObserveBuild(EquipmentRules.Equip(s,drop.Item.Id));Assert.Equal("train",AdventureRules.Objective(s)!.Id);
        var afterProfile=FoundationRules.Build(Basis(),s);var after=new PlayerCombat(afterProfile,character:s);
        Assert.True(after.AttackBudget(afterProfile.Skills.Single(k=>k.Id==FrameRules.Basic(frame)))>before.AttackBudget(FoundationRules.Build(Basis(),s with {Equipment=s.Equipment.SetItem(GearSlot.Weapon,EquipmentRules.StarterItems[0].Id)}).Skills.Single(k=>k.Id==FrameRules.Basic(frame))));
        s=AdventureRules.ObserveBuild(FoundationRules.Rank(s,FrameRules.Basic(frame)));Assert.Equal("enforcer",AdventureRules.Objective(s)!.Id);CharacterRules.Validate(s);
    }
    [Fact]
    public void ClaimedRewardsAndRepeatedPickupCannotDuplicateLootOrXp()
    {
        var s=Patrol(Fresh());var encoded=CharacterStore.Encode(s);
        Assert.Equal(encoded,CharacterStore.Encode(WorldRules.ClaimEncounter(s,s.World!.CampaignId,"ash.0","ash.0.pack.0")));
        var drop=Assert.Single(s.World!.Adventure!.GroundLoot);s=AdventureRules.PickUp(s,drop.Item.Id,drop.Position);
        Assert.Equal(CharacterStore.Encode(s),CharacterStore.Encode(AdventureRules.PickUp(s,drop.Item.Id,drop.Position)));
    }
    [Fact]
    public void GroundLootAndPortalSurviveReloadAndTravelWithoutRecreatingRewards()
    {
        var s=Patrol(Fresh());var drop=Assert.Single(s.World!.Adventure!.GroundLoot);
        s=AdventureRules.PortalOut(s,new(1200,1680));var saved=CharacterStore.Decode(CharacterStore.Encode(s));
        Assert.Equal("hearth",saved.World!.ActiveZone.Id);Assert.Single(saved.World.Adventure!.GroundLoot);
        saved=AdventureRules.PortalBack(saved);Assert.Equal("ash.0",saved.World!.ActiveZone.Id);Assert.Equal(new WorldPoint(1200,1680),saved.World.Arrival);
        Assert.Null(saved.World.Adventure!.ReturnPortal);Assert.Single(saved.World.Adventure.GroundLoot);
        saved=AdventureRules.PickUp(saved,drop.Item.Id,drop.Position);CharacterRules.Validate(saved);
    }
    [Fact]
    public void PickupRequiresTheCorrectZoneDistanceAndFreeInventorySpace()
    {
        var s=Patrol(Fresh());var drop=Assert.Single(s.World!.Adventure!.GroundLoot);
        Assert.Throws<InvalidOperationException>(()=>AdventureRules.PickUp(s,drop.Item.Id,new(400,400)));
        Assert.Throws<InvalidOperationException>(()=>AdventureRules.PickUp(WorldRules.Enter(s,"hearth",new(640,384)),drop.Item.Id,drop.Position));
        var filled=s with {Inventory=[..s.Inventory,..Enumerable.Range(0,30).Select(i=>EquipmentRules.StarterItems[0] with {Id=Guid.NewGuid()})]};
        Assert.Throws<InvalidOperationException>(()=>AdventureRules.PickUp(filled,drop.Item.Id,drop.Position));Assert.Single(filled.World!.Adventure!.GroundLoot);
    }
    [Fact]
    public void FrontierRewardRetainsDeferredLootAndOnlyClaimsOnce()
    {
        var s=Fresh();s=WorldRules.Enter(s,"frontier.0",new(180,1920));var e=s.World!.ActiveZone.Encounters.Single(e=>e.Boss>=0);
        s=WorldRules.ClaimEncounter(s,s.World.CampaignId,s.World.ActiveZone.Id,e.Id);Assert.Single(s.World!.Adventure!.GroundLoot);Assert.Contains(e.Id,FrontierWorld.Claims(s.World));
        Assert.Equal(CharacterStore.Encode(s),CharacterStore.Encode(WorldRules.ClaimEncounter(s,s.World.CampaignId,s.World.ActiveZone.Id,e.Id)));CharacterRules.Validate(s);
    }
    [Fact]
    public void SchemaSixKeepsCampaignGearAndExactCountersWhenMigrated()
    {
        var before=WorldRules.Enroll(FrameRules.Create(FrameId.Revenant)) with {SchemaVersion=6,Gold=BigInteger.Pow(10,70)};
        var after=CharacterStore.Decode(CharacterStore.Encode(before));Assert.Equal(8,after.SchemaVersion);Assert.Equal(before.Gold,after.Gold);
        Assert.Equal(CharacterStore.Encode(before with {SchemaVersion=8}),CharacterStore.Encode(after));
    }
    [Fact]
    public void UnknownAdventureVersionsAreFutureSaves()
    {
        var s=Fresh();s=s with {World=s.World! with {Adventure=s.World.Adventure! with {Version="adventure.v99"}}};
        Assert.Throws<FutureSaveException>(()=>CharacterStore.Decode(CharacterStore.Encode(s)));
    }
    [Theory] [InlineData("fleet")] [InlineData("flow")] [InlineData("guard")] [InlineData("tempo")]
    public void LegendaryPowersChangeTheirNamedStatOnce(string power)
    {
        var s=Fresh();var basis=Basis();var before=FoundationRules.Build(basis,s);
        var item=EquipmentRules.StarterItems[0] with {Band=5,Power=power};s=s with {Inventory=s.Inventory.SetItem(0,item)};
        var after=FoundationRules.Build(basis,s);CharacterRules.Validate(s);
        Assert.True(power switch {"fleet"=>after.Hero.Speed>before.Hero.Speed,"flow"=>after.Hero.FocusPerSecond==before.Hero.FocusPerSecond+4,
            "guard"=>after.Hero.Life==before.Hero.Life*112/100,_=>after.Skills.Single(k=>k.Id==SkillId.ShieldPulse).Cooldown==before.Skills.Single(k=>k.Id==SkillId.ShieldPulse).Cooldown*90/100});
        Assert.Equal(after.Hero,FoundationRules.Build(basis,s).Hero);
    }
    [Fact]
    public void ReconfiguringGearPreservesLifeFocusFlasksCooldownClockAndPendingPlansBecomeStale()
    {
        var s=Fresh();var basis=Basis();var p=new PlayerCombat(FoundationRules.Build(basis,s),character:s);
        Assert.True(p.ReceiveHit(CombatMath.Points(40),false));Assert.True(p.TryStart(SkillId.ShieldPulse,Vector2.UnitX));
        for(var tick=0;tick<50;tick++)p.AdvanceClock();var plan=p.Preview(SkillId.Cleave,Vector2.UnitX);
        var snapshot=(p.Life,p.Focus,p.FlaskCharges,p.Cooldown(SkillId.ShieldPulse),p.Tick,p.Memories.Revision);
        s=FoundationRules.AddXp(s,2000);p.Reconfigure(FoundationRules.Build(basis,s),s);
        Assert.Equal(snapshot,(p.Life,p.Focus,p.FlaskCharges,p.Cooldown(SkillId.ShieldPulse),p.Tick,p.Memories.Revision));
        Assert.False(p.TryCommit(plan));Assert.Contains("changed",p.LastReason);
    }
    [Fact]
    public void DifficultyOnlyChangesInTownAndReturnPortalRejectsWalls()
    {
        var s=AdventureRules.SetDifficulty(Fresh(),AdventureDifficulty.Story);s=WorldRules.Enter(s,"ash.0",new(420,1680));
        Assert.Throws<InvalidOperationException>(()=>AdventureRules.SetDifficulty(s,AdventureDifficulty.Veteran));
        Assert.Throws<InvalidOperationException>(()=>AdventureRules.PortalOut(s,new(-1,-1)));CharacterRules.Validate(s);
    }
    [Fact]
    public void ContractsPersistTheirObjectivesPayOnceAndOpenAnotherFreshReach()
    {
        var s=Fresh() with {ValidatedLevel=17,TotalXp=Progression.TotalXp(17)};
        s=s with {World=s.World! with {Adventure=null}};s=AdventureRules.Enable(s);s=AdventureRules.BeginContract(s);
        var targets=AdventureRules.ContractTargets(0);
        foreach(var target in targets.Take(3))s=WorldRules.ClaimEncounter(s,s.World!.CampaignId,s.World.ActiveZone.Id,target);
        s=CharacterStore.Decode(CharacterStore.Encode(s));Assert.Equal(3,s.World!.Adventure!.Contract!.Completed.Count);
        Assert.Throws<InvalidOperationException>(()=>AdventureRules.ClaimContract(s));s=WorldRules.Interact(s,targets[3]);
        Assert.Equal(4,s.World!.Adventure!.Contract!.Completed.Count);s=AdventureRules.PortalOut(s,new(3000,1920));
        var xp=s.TotalXp;var gold=s.Gold;var count=s.Inventory.Length;s=AdventureRules.ClaimContract(s);
        Assert.Equal(xp+800,s.TotalXp);Assert.Equal(gold+100,s.Gold);Assert.Equal(count+1,s.Inventory.Length);
        Assert.Equal(5,s.Inventory[^1].Band);Assert.NotNull(s.Inventory[^1].Power);Assert.Null(s.World!.Adventure!.Contract);
        Assert.Throws<InvalidOperationException>(()=>AdventureRules.ClaimContract(s));s=AdventureRules.BeginContract(s);
        Assert.Equal("frontier.1",s.World!.ActiveZone.Id);Assert.Equal(1,s.World.Adventure!.Contract!.Depth);Assert.Empty(s.World.Adventure.Contract.Completed);CharacterRules.Validate(s);
    }
    [Fact]
    public void UntrainedCharactersCannotTakeContractsAndFullBagsDoNotLoseTheirReward()
    {
        Assert.Throws<InvalidOperationException>(()=>AdventureRules.BeginContract(Fresh()));
        var s=AdventureRules.Enable(WorldRules.Enroll(FrameRules.Create(FrameId.Warden,false)));s=AdventureRules.BeginContract(s);
        foreach(var id in AdventureRules.ContractTargets(0).Take(3))s=WorldRules.ClaimEncounter(s,s.World!.CampaignId,s.World.ActiveZone.Id,id);
        s=WorldRules.Interact(s,AdventureRules.ContractTargets(0)[3]);s=AdventureRules.PortalOut(s,new(3000,1920));
        s=s with {Inventory=[..s.Inventory,..Enumerable.Range(0,30).Select(i=>EquipmentRules.StarterItems[0] with {Id=Guid.NewGuid()})]};
        Assert.Throws<InvalidOperationException>(()=>AdventureRules.ClaimContract(s));Assert.NotNull(s.World!.Adventure!.Contract);CharacterRules.Validate(s);
    }
    [Fact]
    public void RestingInTownDoesNotSkipANewCharactersFirstQuest()
    {
        var s=WorldRules.Interact(WorldRules.Enroll(FrameRules.Create(FrameId.Warden)),"hearth.waypoint");
        s=AdventureRules.Enable(s);Assert.Equal("meet",AdventureRules.Objective(s)!.Id);Assert.Single(Patrol(s).World!.Adventure!.GroundLoot);
    }
    [Fact]
    public void CorruptLootReferencesAndDisplacedDropsAreRejectedBeforeSaving()
    {
        var s=Patrol(Fresh());var a=s.World!.Adventure!;var drop=a.GroundLoot[0];
        Assert.Throws<InvalidDataException>(()=>CharacterRules.Validate(s with {World=s.World with {Adventure=a with {GroundLoot=[drop with {Position=new(10,10)}]}}}));
        Assert.Throws<InvalidDataException>(()=>CharacterRules.Validate(s with {World=s.World with {Claims=[]}}));
        Assert.Throws<InvalidDataException>(()=>CharacterRules.Validate(s with {Inventory=s.Inventory.Add(drop.Item)}));
    }
    [Fact]
    public void MissingLootArraysAreRejectedAsInvalidSaveData()
    {
        var s=Fresh();
        Assert.Throws<InvalidDataException>(()=>CharacterRules.Validate(s with {World=s.World! with {Adventure=s.World.Adventure! with {GroundLoot=default}}}));
    }
}
