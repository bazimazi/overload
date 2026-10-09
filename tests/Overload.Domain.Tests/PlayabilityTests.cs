using System.Numerics;
using Overload.Content;
using Overload.Domain;
using Xunit;

namespace Overload.Domain.Tests;

public sealed class PlayabilityTests
{
    private static CharacterState Fresh(FrameId frame=FrameId.Warden)=>AdventureRules.Enable(WorldRules.Enroll(FrameRules.Create(frame)));
    private static BalanceProfile Basis()=>ExpansionLoader.Load(ProfileLoader.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"arena.json"))),File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"expansion.json")));
    [Theory] [InlineData(4)] [InlineData(7)] [InlineData(10)]
    public void DefensiveEvadeCanInterruptBasicAttackAtAnyPhase(int age)
    {
        var p=new PlayerCombat(Basis());Assert.True(p.TryStart(SkillId.Cleave,Vector2.UnitX));
        for(var i=0;i<age;i++)p.AdvanceClock();Assert.True(p.TryStart(SkillId.Traverse,Vector2.UnitY));Assert.Equal(SkillId.Traverse,p.Action!.Definition.Id);
    }
    [Fact]
    public void HealingCanInterruptABasicButCannotDuplicateACharge()
    {
        var p=new PlayerCombat(Basis());p.ReceiveHit(CombatMath.Points(80),false);p.TryStart(SkillId.Cleave,Vector2.UnitX);
        for(var i=0;i<7;i++)p.AdvanceClock();Assert.True(p.TryStart(SkillId.Flask,Vector2.UnitX));Assert.Equal(2,p.FlaskCharges);
        Assert.False(p.TryStart(SkillId.Flask,Vector2.UnitX));Assert.Equal(2,p.FlaskCharges);
    }
    [Theory] [InlineData(FrameId.Warden)] [InlineData(FrameId.Threadseer)] [InlineData(FrameId.Revenant)]
    public void FirstEnforcerAlwaysDropsAGameplayChangingLegendary(FrameId frame)
    {
        var s=WorldRules.Enter(Fresh(frame),"ash.1",new(140,384));s=WorldRules.ClaimEncounter(s,s.World!.CampaignId,"ash.1","ash.1.boss");
        var item=Assert.Single(s.World!.Adventure!.GroundLoot).Item;Assert.Equal(5,item.Band);Assert.Contains(item.Power,new[]{"split","nova"});
        CharacterRules.Validate(CharacterStore.Decode(CharacterStore.Encode(s)));
    }
    [Theory] [InlineData(FrameId.Warden)] [InlineData(FrameId.Threadseer)] [InlineData(FrameId.Revenant)]
    public void ReavingChangesBasicGeometryWithoutChangingSkillCosts(FrameId frame)
    {
        var s=Fresh(frame);var before=FoundationRules.Build(Basis(),s).Skills.Single(k=>k.Id==FrameRules.Basic(frame));
        s=s with {Inventory=s.Inventory.SetItem(0,s.Inventory[0] with {Band=5,Power="reaving"})};
        var after=FoundationRules.Build(Basis(),s).Skills.Single(k=>k.Id==before.Id);
        if(before.ProjectileSpeed>0){Assert.Equal(before.MaxVictims+2,after.MaxVictims);Assert.True(after.ProjectileRadius>before.ProjectileRadius);}
        else {Assert.Equal(before.Reach+18,after.Reach);Assert.Equal(Math.Min(180,before.ArcDegrees+60),after.ArcDegrees);}
        Assert.Equal(before.FocusCost,after.FocusCost);Assert.Equal(before.Damage,after.Damage);
    }
    [Fact]
    public void BulkSalvageRetainsEquippedLockedAndRareItemsAndPaysOnlyOnce()
    {
        var s=Fresh();var spare=s.Inventory[0] with {Id=Guid.NewGuid()};var rare=spare with {Id=Guid.NewGuid(),Band=3};var locked=spare with {Id=Guid.NewGuid(),Locked=true};
        s=s with {Inventory=[..s.Inventory,spare,rare,locked]};var after=EquipmentRules.SalvageLowRarity(s);
        Assert.Equal(s.Gold+25,after.Gold);Assert.Equal(s.Alloy+5,after.Alloy);Assert.Equal(8,after.Inventory.Length);Assert.DoesNotContain(after.Inventory,i=>i.Id==spare.Id);
        Assert.Equal(CharacterStore.Encode(after),CharacterStore.Encode(EquipmentRules.SalvageLowRarity(after)));
        Assert.Throws<InvalidOperationException>(()=>EquipmentRules.SalvageLowRarity(WorldRules.Enter(after,"ash.0",new(420,1680))));
    }
    [Fact]
    public void SchemaSevenPreservesSavedAdventureLootAndPortalsWhenMigrated()
    {
        var s=WorldRules.Enter(Fresh(),"ash.0",new(420,1680));s=WorldRules.ClaimEncounter(s,s.World!.CampaignId,"ash.0","ash.0.pack.0");s=AdventureRules.PortalOut(s,new(1200,1680));
        var after=CharacterStore.Decode(CharacterStore.Encode(s with {SchemaVersion=7}));Assert.Equal(CharacterStore.Encode(s),CharacterStore.Encode(after));Assert.Equal(8,after.SchemaVersion);
    }
}
