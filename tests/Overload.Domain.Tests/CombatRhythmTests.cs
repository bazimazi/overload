using System.Numerics;
using System.Collections.Immutable;
using Overload.Content;
using Overload.Domain;
using Xunit;

namespace Overload.Domain.Tests;

public sealed class CombatRhythmTests
{
    private static BalanceProfile Basis()=>ExpansionLoader.Load(ProfileLoader.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"arena.json"))),File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"expansion.json")));
    private sealed class ClearWorld : IActionPreflight
    {public ActionWorldSnapshot Capture(ActionIntent intent,SkillDefinition skill,ActorBehaviorProfile behavior)=>new(0,Vector2.Zero,Enum.GetValues<ActionImplementation>().ToImmutableDictionary(i=>i,_=>PreflightResult.Ready));}
    private static PlayerCombat Create(FrameId frame=FrameId.Warden, IActionPreflight? world=null)
    {
        var s=AdventureRules.Enable(WorldRules.Enroll(FrameRules.Create(frame)));
        return new(FoundationRules.Build(Basis(),s),world,character:s);
    }
    private static void Idle(PlayerCombat p){for(var i=0;i<120&&p.Action is not null;i++)p.AdvanceClock();}
    private static EffectProvenance Basic(PlayerCombat p,FrameId frame=FrameId.Warden)
    {Idle(p);Assert.True(p.TryStart(FrameRules.Basic(frame),Vector2.UnitX,true));return new(p.Action!.RootActionId,0,null,SourceKind.BasePlayerAction,0);}
    [Theory][InlineData(FrameId.Warden)][InlineData(FrameId.Threadseer)][InlineData(FrameId.Revenant)]
    public void ThreeLandedBasicsEmpowerOnePaidAction(FrameId frame)
    {
        var p=Create(frame);
        for(var i=0;i<3;i++)Assert.True(p.ObserveBasicHit(Basic(p,frame)));
        Assert.Equal(3,p.SurgeCharges);Idle(p);
        Assert.True(p.TryStart(FrameRules.Skills(frame)[1],Vector2.UnitX,true));
        Assert.Equal(150,p.Action!.DamagePercent);Assert.Equal(0,p.SurgeCharges);Assert.Equal(1,p.SurgedActions);
        Idle(p);while(p.Cooldown(FrameRules.Skills(frame)[1])>0)p.AdvanceClock();
        Assert.True(p.TryStart(FrameRules.Skills(frame)[1],Vector2.UnitX,true));Assert.Equal(100,p.Action!.DamagePercent);
    }
    [Fact]
    public void FocusIsEarnedOncePerRootAndCapsAtMaximum()
    {
        var p=Create();Assert.True(p.TryStart(SkillId.Faultline,Vector2.UnitX,true));
        var root=Basic(p);var focus=p.Focus;Assert.True(p.ObserveBasicHit(root));Assert.Equal(Math.Min(p.MaximumFocus,focus+8000),p.Focus);
        Assert.False(p.ObserveBasicHit(root));Assert.Equal(1,p.SurgeCharges);Assert.Equal(1,p.BasicFocusReceipts);
        for(var i=0;i<30;i++)p.ObserveBasicHit(Basic(p));Assert.Equal(p.MaximumFocus,p.Focus);Assert.Equal(3,p.SurgeCharges);
    }
    [Theory][InlineData(SourceKind.SecondaryEffect,0,0)][InlineData(SourceKind.OverloadedPlayerAction,0,0)][InlineData(SourceKind.BasePlayerAction,1,0)][InlineData(SourceKind.BasePlayerAction,0,1)]
    public void SecondaryAndAlteredEvidenceCannotGenerateCharges(SourceKind source,long effect,int depth)
    {var p=Create();var root=Basic(p);Assert.False(p.ObserveBasicHit(root with {Source=source,EffectId=effect,Depth=depth}));Assert.Equal(0,p.SurgeCharges);}
    [Fact]
    public void PaidRootsAndMissesCannotGenerateCharges()
    {var p=Create();Assert.True(p.TryStart(SkillId.ShieldPulse,Vector2.UnitX));Assert.False(p.ObserveBasicHit(new(p.Action!.RootActionId,0,null,SourceKind.BasePlayerAction,0)));Basic(p);Assert.Equal(0,p.SurgeCharges);}
    [Fact]
    public void ExpiryAndResetClearTransientCharges()
    {var p=Create();p.ObserveBasicHit(Basic(p));for(var i=0;i<360;i++)p.AdvanceClock();Assert.Equal(0,p.SurgeCharges);p.ObserveBasicHit(Basic(p));p.Reset();Assert.Equal(0,p.SurgeCharges);}
    [Fact]
    public void RejectedCastDoesNotConsumeSurgeAndPreviewDoesNotMutateIt()
    {
        var p=Create();for(var i=0;i<3;i++)p.ObserveBasicHit(Basic(p));
        var plan=p.Preview(SkillId.Faultline,Vector2.UnitX);Assert.False(plan.Selection.Accepted);Assert.False(p.TryCommit(plan));Assert.Equal(3,p.SurgeCharges);
        Idle(p);p.Preview(SkillId.Faultline,Vector2.UnitX);Assert.Equal(3,p.SurgeCharges);Assert.Equal(0,p.SurgedActions);
    }
    [Fact]
    public void NewHitInvalidatesAnOlderAtomicPlan()
    {var p=Create();var root=Basic(p);Idle(p);var plan=p.Preview(SkillId.ShieldPulse,Vector2.UnitX);p.ObserveBasicHit(root);Assert.False(p.TryCommit(plan));Assert.Equal(1,p.SurgeCharges);}
    [Fact]
    public void CovenantStillChargesSurgeWithoutRestoringFocus()
    {var p=Create();Assert.True(p.TryChangeOaths(["oath.red-covenant"]));var focus=p.Focus;p.ObserveBasicHit(Basic(p));Assert.Equal(focus,p.Focus);Assert.Equal(1,p.SurgeCharges);}
    [Fact]
    public void LegacyArenaRetainsItsOriginalResourceRules()
    {var p=new PlayerCombat(Basis());var root=Basic(p);Assert.False(p.CombatRhythmEnabled);Assert.False(p.ObserveBasicHit(root));Assert.Equal(0,p.SurgeCharges);}
    [Fact]
    public void AnOverloadedBasicKeepsItsGeneratorButItsChildCannotRewardIt()
    {
        var p=Create(world:new ClearWorld());p.SetCombatActive(true);p.Memories.ObserveLocomotion(new(96,0),LocomotionKind.Walk);
        Assert.True(p.TryStart(SkillId.Cleave,Vector2.UnitX));Assert.Equal(SourceKind.OverloadedPlayerAction,p.Action!.Source);
        var root=new EffectProvenance(p.Action.RootActionId,0,null,p.Action.Source,0);
        Assert.False(p.ObserveBasicHit(root with {Source=SourceKind.SecondaryEffect,EffectId=1,Depth=1}));Assert.True(p.ObserveBasicHit(root));Assert.Equal(1,p.SurgeCharges);
    }
    [Fact]
    public void GlobeHealingCapsAtLifeAndCannotResurrect()
    {
        var p=Create();p.ReceiveHit(p.MaximumLife/2,false);var before=p.Life;
        Assert.Equal(p.MaximumLife*12/100,p.RestoreLife(12));Assert.True(p.Life>before);for(var i=0;i<10;i++)p.RestoreLife(20);Assert.Equal(p.MaximumLife,p.Life);
        Assert.Equal(0,p.RestoreLife(21));p.ReceiveHit(CombatMath.Points(100000),false);Assert.True(p.Dead);Assert.Equal(0,p.RestoreLife(12));Assert.True(p.Dead);
    }
}
