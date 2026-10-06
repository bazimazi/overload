using System.Collections.Immutable;
using System.Numerics;
using Overload.Content;
using Overload.Domain;
using Xunit;
namespace Overload.Domain.Tests;
public sealed class ExpansionTests
{
    private static BalanceProfile Profile => ExpansionLoader.Load(ProfileLoader.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"arena.json"))),File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"expansion.json")));
    private static BalanceProfile Fast => Profile with { Hero=Profile.Hero with { FocusPerSecond=0 }, Skills=[..Profile.Skills.Select(s=>s with { Windup=0,Active=1,Recovery=0,Cooldown=0 })] };
    private static void Advance(PlayerCombat p,int ticks) { for(var i=0;i<ticks;i++)p.AdvanceClock(); }
    private static PlayerCombat Covenant(BalanceProfile? profile=null)
    { var p=new PlayerCombat(profile??Fast);Assert.True(p.TryChangeOaths(["oath.red-covenant"]));return p; }
    [Theory]
    [InlineData(FrameId.Warden)] [InlineData(FrameId.Threadseer)] [InlineData(FrameId.Revenant)]
    public void ThreeEqualBudgetBuildsAreLegalAndUseTheirOwnFrame(FrameId frame)
    {
        var profile=Profile;
        for(var build=0;build<3;build++)
        {
            var s=FrameRules.BuildFixture(frame,build);CharacterRules.Validate(s);var actor=FoundationRules.Build(profile,s);
            Assert.Equal(19,FoundationRules.SkillSpent(s));Assert.Equal(8,s.Talents.Count);Assert.Equal(10,actor.Skills.Length);
            Assert.All(s.EquippedSkills,id=>Assert.Contains(id,FrameRules.Skills(frame)));Assert.Equal(0,actor.Skills.Single(k=>k.Id==FrameRules.Basic(frame)).FocusCost);
            Assert.Throws<InvalidDataException>(()=>CharacterRules.Validate(s with { EquippedSkills=[SkillId.Traverse,..s.EquippedSkills.Skip(1)] }));
        }
    }
    [Fact]
    public void EveryAuthoredRoomAndThousandsOfRegionalLayoutsKeepSpawnAndReturnRoutes()
    {
        _=Profile;
        foreach(var region in Enum.GetValues<Region>())for(ulong seed=0;seed<1000;seed++)
        {
            var layout=RegionalContent.Generate(seed,region);Assert.True(ExpeditionGenerator.Connected(layout));Assert.Equal(7,layout.Rooms.Distinct().Count());
            Assert.All(layout.Rooms,id=>Assert.Equal(region,RegionalContent.Rooms.Single(r=>r.Id==id).Region));Assert.True(RegionalContent.Rooms.Single(r=>r.Id==layout.Rooms[^1]).Boss);
        }
    }
    [Fact]
    public void NewAndOldExpeditionsRoundTripWithoutChangingTheirSeededGeometry()
    {
        _=Profile;var s=ProductionTests.Reference();
        foreach(var expanded in new[]{false,true})
        {
            var active=EndgameRules.Begin(s,10,Guid.NewGuid(),Region.Crown,ActivityFamily.Vault,regionalContent:expanded);
            var decoded=CharacterStore.Decode(CharacterStore.Encode(active));Assert.True(ExpeditionGenerator.Same(active.Fracture!.Layout!,decoded.Fracture!.Layout!));
            for(var i=0;i<7;i++)decoded=EndlessRules.Claim(decoded,decoded.Fracture!.Id,decoded.Fracture.Sequence,i);
            Assert.Equal(6,decoded.Seals[3]);Assert.Single(decoded.RunRecords);CharacterRules.Validate(decoded);
        }
        var old=s with { SchemaVersion=4 };var migrated=CharacterStore.Decode(CharacterStore.Encode(old));Assert.Equal(FrameId.Warden,migrated.Frame);Assert.False(migrated.RegionalCampaign);Assert.Equal(s.TotalXp,migrated.TotalXp);
    }
    [Fact]
    public void CapRejectsFifthFastCastWithoutSpendingFocusOrHealingOnIndependentExpiry()
    {
        var p=Covenant();var full=p.MaximumLife;
        for(var i=0;i<4;i++){Assert.True(p.TryStart(SkillId.ShieldPulse,Vector2.UnitX));Advance(p,10);}
        Assert.Equal(400,p.ReservedPermille);Assert.Equal(full*60/100,p.MaximumLife);Assert.Equal(p.MaximumLife,p.Life);Assert.Equal(p.MaximumFocus,p.Focus);
        var before=p.Life;Assert.False(p.Preview(SkillId.ShieldPulse,Vector2.UnitX).Selection.Accepted);Assert.False(p.TryStart(SkillId.ShieldPulse,Vector2.UnitX));Assert.Equal(4,p.Reservations.Count);Assert.Equal(before,p.Life);
        p.ReceiveHit(CombatMath.Points(40),false);var hurt=p.Life;Advance(p,200);Assert.Equal(300,p.ReservedPermille);Assert.Equal(hurt,p.Life);
        Advance(p,30);Assert.Empty(p.Reservations);Assert.Equal(full,p.MaximumLife);Assert.Equal(hurt,p.Life);
    }
    [Fact]
    public void FreeActionsNoRefundAndLifeChangesPreserveTheReservoir()
    {
        var p=Covenant();Assert.True(p.TryStart(SkillId.Cleave,Vector2.UnitX));Advance(p,1);Assert.Empty(p.Reservations);
        var plan=p.Preview(SkillId.ChainLance,Vector2.UnitX);Assert.True(p.TryCommit(plan));Assert.False(p.TryCommit(plan));Assert.Single(p.Reservations);Assert.Equal(96,p.ReservedPermille);
        p.ChangeMaximumLife(CombatMath.Points(500));Assert.Equal(CombatMath.Points(452),p.MaximumLife);
        var before=p.Life;Advance(p,240);Assert.Equal(before,p.Life);Assert.Equal(CombatMath.Points(500),p.MaximumLife);
        Assert.True(p.TryStart(SkillId.ShieldPulse,Vector2.UnitX));Assert.Throws<InvalidOperationException>(()=>p.ChangeMaximumLife(1000));
    }
    [Fact]
    public void MinimumAvailableLifeAndMaximumChangesRejectWithoutClampingToDeath()
    {
        var p=Covenant(Fast with { Hero=Fast.Hero with { Life=1 } });Assert.False(p.TryStart(SkillId.ShieldPulse,Vector2.UnitX));Assert.Empty(p.Reservations);Assert.Equal(1000,p.Life);
        p=Covenant();Assert.True(p.TryStart(SkillId.ShieldPulse,Vector2.UnitX));Assert.Throws<InvalidOperationException>(()=>p.ChangeMaximumLife(1000));Assert.True(p.Life>=1000);
        var life=p.Life;p.ChangeMaximumLife(2000);Assert.Equal(1800,p.MaximumLife);Assert.Equal(1800,p.Life);Advance(p,240);Assert.Equal(1800,p.Life);
    }
    [Fact]
    public void ExpiryPrecedesSelectionAndARejectedCastKeepsMemoriesAndResources()
    {
        var p=Covenant();p.SetCombatActive(true);
        for(var i=0;i<4;i++){Assert.True(p.TryStart(SkillId.ShieldPulse,Vector2.UnitX));Advance(p,1);}
        p.Memories.ObserveLocomotion(new(96,0),LocomotionKind.Walk);var memory=p.Memories.Momentum;var focus=p.Focus;
        Assert.False(p.TryStart(SkillId.ChainLance,Vector2.UnitX));Assert.Equal(memory,p.Memories.Momentum);Assert.Equal(focus,p.Focus);
        Advance(p,236);Assert.Equal(300,p.ReservedPermille);Assert.True(p.TryStart(SkillId.ChainLance,Vector2.UnitX));Assert.Equal(396,p.ReservedPermille);
    }
    [Fact]
    public void CovenantCanCommitAtZeroFocusAndFrameAuthorityRejectsForeignSkills()
    {
        var fast=Fast with { Skills=[..Fast.Skills.Select(s=>s.Id==SkillId.ChainLance?s with { FocusCost=100 }:s)] };
        var p=new PlayerCombat(fast);Assert.True(p.TryStart(SkillId.ChainLance,Vector2.UnitX));Advance(p,1);Assert.Equal(0,p.Focus);
        Assert.True(p.TryChangeOaths(["oath.red-covenant"]));Assert.True(p.TryStart(SkillId.ShieldPulse,Vector2.UnitX));Assert.Equal(0,p.Focus);Assert.Equal(100,p.ReservedPermille);
        var state=FrameRules.Create(FrameId.Threadseer);p=new(FoundationRules.Build(Profile,state),character:state);
        Assert.False(p.Preview(SkillId.Reap,Vector2.UnitX).Selection.Accepted);Assert.False(p.TryStart(SkillId.Reap,Vector2.UnitX));Assert.Null(p.Action);
    }
    [Fact]
    public void LaterOathCannotBypassMasteryFirstOathWalletOrOneSelectionRules()
    {
        var qualified=ProductionTests.Reference() with { ElsewhereOwned=true,CovenantMastery=true,Seals=[30,30,30,30] };
        Assert.Throws<InvalidOperationException>(()=>CovenantRules.Unlock(qualified with { ElsewhereOwned=false }));
        Assert.Throws<InvalidOperationException>(()=>CovenantRules.Unlock(qualified with { CovenantMastery=false }));
        Assert.Throws<InvalidOperationException>(()=>CovenantRules.Unlock(qualified with { Seals=[30,29,30,30] }));
        var earned=CovenantRules.Select(CovenantRules.Unlock(qualified),"oath.red-covenant");Assert.True(earned.RedCovenantSelected);Assert.False(earned.ElsewhereSelected);
        Assert.Throws<InvalidDataException>(()=>CharacterRules.Validate(earned with { ElsewhereSelected=true }));
        var swapped=CovenantRules.Select(earned,"oath.elsewhere");Assert.False(swapped.RedCovenantSelected);Assert.True(swapped.ElsewhereSelected);Assert.Equal(earned.Seals,swapped.Seals);
    }
    [Fact]
    public void HealingUsesReducedCapacityWhileFocusRegenerationIsInactive()
    {
        var p=Covenant(Fast with { Hero=Fast.Hero with { FocusPerSecond=100 } });Assert.True(p.TryStart(SkillId.ShieldPulse,Vector2.UnitX));Advance(p,1);
        p.ReceiveHit(CombatMath.Points(50),false);Assert.True(p.TryStart(SkillId.Flask,Vector2.UnitX));Advance(p,120);
        Assert.Equal(p.MaximumLife,p.Life);Assert.True(p.HealedUnderReservation);var life=p.Life;Advance(p,120);Assert.Equal(life,p.Life);Assert.Equal(p.MaximumFocus,p.Focus);
        Assert.False(p.TryChangeOaths(["oath.elsewhere","oath.red-covenant"]));
    }
    [Fact]
    public void UtilityAndSecondaryEffectsCannotDispatchDirectPatternsOrGenerateMemories()
    {
        var profile=Profile;
        foreach(var id in new[]{SkillId.EmberWell,SkillId.StormLoom,SkillId.EchoOrder,SkillId.Veil,SkillId.Bulwark,SkillId.RecallThread})
            Assert.False(PatternExecution.Supports(profile.Skills.Single(s=>s.Id==id),ActionImplementation.Afterstrike));
        var memories=new CombatMemories(profile.Memories);memories.SetCombatActive(true);memories.ObservePosition(Vector2.Zero);memories.AdvanceTo(60);
        Assert.False(memories.ObserveBaseAssaultHit(Vector2.Zero,Vector2.UnitX,1,true,SourceKind.SecondaryEffect));Assert.Null(memories.Stillness);Assert.Null(memories.Rupture);
    }
    [Theory]
    [InlineData(SaveStage.TemporaryWritten)] [InlineData(SaveStage.Flushed)] [InlineData(SaveStage.Verified)] [InlineData(SaveStage.BeforeReplace)] [InlineData(SaveStage.Replaced)]
    public void CovenantRitualOwnershipAndAllFourDebitsRecoverTogether(SaveStage stage)
    {
        var dir=Path.Combine(Path.GetTempPath(),"overload-covenant-"+Guid.NewGuid());
        try
        {
            var original=ProductionTests.Reference() with { ElsewhereOwned=true,CovenantMastery=true,Seals=[30,30,30,30] };
            var store=new CharacterStore(dir,()=>original);store.Fault=s=>{if(s==stage)throw new IOException("Injected ritual crash");};
            Assert.Throws<IOException>(()=>store.Transact(0,"covenant",CovenantRules.Unlock));var reloaded=new CharacterStore(dir);
            if(!reloaded.State.RedCovenantOwned)reloaded.Transact(reloaded.State.Revision,"covenant",CovenantRules.Unlock);
            Assert.True(reloaded.State.RedCovenantOwned);Assert.All(reloaded.State.Seals,v=>Assert.Equal(0,v));Assert.False(reloaded.Transact(reloaded.State.Revision,"covenant",CovenantRules.Unlock));
            reloaded.Transact(reloaded.State.Revision,"equip",s=>CovenantRules.Select(s,"oath.red-covenant"));Assert.True(new CharacterStore(dir).State.RedCovenantSelected);
        }
        finally { if(Directory.Exists(dir))Directory.Delete(dir,true); }
    }
    [Fact]
    public void RegionalCampaignPaysOnceAndOnlyOpensAtlasAfterFirstPattern()
    {
        var s=JourneyRules.Begin(FrameRules.Create(FrameId.Threadseer),Guid.NewGuid());
        for(var room=0;room<16;room++) { var next=JourneyRules.Clear(s,s.RunId,room);Assert.Equal(next,JourneyRules.Clear(next,next.RunId,room));Assert.Equal(room==15,next.FractureUnlocked);s=next;CharacterRules.Validate(s); }
        Assert.Equal(16,s.CheckpointRoom);Assert.Equal(4,s.SkillMilestones.Count);
    }
}
