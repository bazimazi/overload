using System.Collections.Immutable;
using System.Numerics;
using Overload.Content;
using Overload.Domain;
using Xunit;

namespace Overload.Domain.Tests;
public sealed class ProductionTests
{
    public static CharacterState Reference()=>EndlessFixtures.Reference(10) with { Mode=ProfileMode.Standard,Seals=[0,0,0,0],TotalXp=Progression.TotalXp(100),ValidatedLevel=100,Might=20,Resolve=20 };
    public static CharacterState Clear(CharacterState s,Region region,ActivityFamily activity,bool mastery=true,bool anomaly=false)
    {
        if(!s.ChapterOffers.IsEmpty)s=EndlessRules.ChooseRoute(s,s.ChapterOffers[0]);
        s=EndgameRules.Begin(s,10,Guid.NewGuid(),region,activity,anomaly?"mutation.anchor":null,anomaly);
        s=s with { Fracture=s.Fracture! with { MasteryEarned=mastery,ElapsedTicks=600 } };
        for(var i=0;i<7;i++)s=EndlessRules.Claim(s,s.Fracture!.Id,s.Fracture.Sequence,i);
        CharacterRules.Validate(s);return s;
    }
    [Fact]
    public void StandardBindingSlotsGrowAt12And30WhileTheSliceRemainsExplicitlyAccelerated()
    {
        var s=EndgameRules.Standard();Assert.Equal(1,EndgameRules.BindingSlots(s));
        var basis=ProfileLoader.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"arena.json")));Assert.Single(new PlayerCombat(FoundationRules.Build(basis,s),character:s).Behavior.Bindings);
        s=FoundationRules.AddXp(s,Progression.TotalXp(12));Assert.Equal(2,EndgameRules.BindingSlots(s));
        s=FoundationRules.AddXp(s,Progression.TotalXp(30)-s.TotalXp);Assert.Equal(3,EndgameRules.BindingSlots(s));
        Assert.Equal(3,EndgameRules.BindingSlots(new CharacterState()));
    }
    [Fact]
    public void NewPatternDiscoveriesAreGuaranteedBySavedMasteryAndTrialFlags()
    {
        var s=Reference();Assert.False(EndgameRules.PatternAvailable(s,"pattern.assault.focused"));Assert.False(EndgameRules.PatternAvailable(s,"pattern.assault.shatter"));Assert.False(EndgameRules.PatternAvailable(s,"pattern.assault.cascade"));
        s=Clear(s,Region.Hollow,ActivityFamily.Hunt);Assert.True(EndgameRules.PatternAvailable(s,"pattern.assault.focused"));
        s=Clear(s,Region.Crown,ActivityFamily.Hunt);Assert.True(EndgameRules.PatternAvailable(s,"pattern.assault.shatter"));
        s=EndgameRules.TrialVictory(s,Guid.NewGuid());Assert.True(EndgameRules.PatternAvailable(s,"pattern.assault.cascade"));
    }
    [Fact]
    public void ContentCatalogReportsPathsForBrokenReferenceCycleLocalizationAnimationAndDuplicates()
    {
        var c=ReleaseContent.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"release.json")));
        var first=c.Definitions[0];
        var bad=c with { Definitions=c.Definitions.SetItem(0,first with { References=["missing.id"],Prerequisites=[first.Id],DescriptionKey="missing.text",Animation="missing.animation" }).Add(first) };
        var errors=ReleaseContent.Validate(bad);
        Assert.Contains(errors,e=>e.Contains("references")&&e.Contains("missing.id"));
        Assert.Contains(errors,e=>e.Contains("cycle"));Assert.Contains(errors,e=>e.Contains("localization"));Assert.Contains(errors,e=>e.Contains("animation"));Assert.Contains(errors,e=>e.Contains("duplicate"));
    }
    [Fact]
    public void GeneratorVersionSeedAndFallbackHaveConnectedSocketsAndRepeatableRoomGeometry()
    {
        foreach(var seed in new ulong[]{0,1,42,ulong.MaxValue})
        {
            var a=ExpeditionGenerator.Generate(seed);var b=ExpeditionGenerator.Generate(seed);
            Assert.True(ExpeditionGenerator.Same(a,b));Assert.True(ExpeditionGenerator.Connected(a));
            Assert.True(ExpeditionGenerator.Generate(seed,0).Fallback);Assert.True(ExpeditionGenerator.Connected(ExpeditionGenerator.Generate(seed,0)));
            Assert.InRange(a.Nodes.Count(n=>n.Kind=="Combat"),2,4);
        }
    }
    [Theory]
    [InlineData(ActivityFamily.Hunt)] [InlineData(ActivityFamily.Breach)] [InlineData(ActivityFamily.Vault)]
    public void AllActivitiesBankExactlyOneBudgetAndOnlyEligibleTiersGrantSixSeals(ActivityFamily family)
    {
        var before=Reference();var after=Clear(before,Region.Glass,family);var run=after.Fracture!;
        Assert.Equal(run.XpBudget,after.TotalXp-before.TotalXp);Assert.Equal(2000,after.Gold-before.Gold);Assert.Equal(1000,after.Alloy-before.Alloy);
        Assert.Equal(6,after.Seals[1]);Assert.Contains(EndgameRules.Proof(Region.Glass,family),after.Proofs);
        Assert.Same(after,EndlessRules.Claim(after,run.Id,run.Sequence,6));
        var low=EndgameRules.Begin(before,9,Guid.NewGuid(),Region.Ash,family);for(var i=0;i<7;i++)low=EndlessRules.Claim(low,low.Fracture!.Id,low.Fracture.Sequence,i);
        Assert.All(low.Seals,v=>Assert.Equal(0,v));Assert.Empty(low.Proofs);
    }
    [Fact]
    public void SeventyTwoBreadthRunsReachExactly120PerRegionWithoutRepeatBonuses()
    {
        var s=Reference();
        foreach(var r in Enum.GetValues<Region>())for(var i=0;i<18;i++)s=Clear(s,r,(ActivityFamily)(i%3));
        Assert.Equal(12,s.Proofs.Count);Assert.Equal(4,s.Masteries.Count);Assert.Equal(4,s.BreadthBonuses.Count);Assert.All(s.Seals,v=>Assert.Equal(120,v));
        Assert.Equal(64,s.RunRecords.Length);Assert.Equal(72,s.ExpeditionSequence);
        s=EndgameRules.TrialVictory(s,Guid.NewGuid());Assert.Empty(EndgameRules.Qualification(s));
        var unlocked=EndgameRules.Ritual(s);Assert.True(unlocked.ElsewhereOwned);Assert.All(unlocked.Seals,v=>Assert.Equal(0,v));Assert.Throws<InvalidOperationException>(()=>EndgameRules.Ritual(unlocked));
    }
    [Theory]
    [InlineData(99,false)] [InlineData(100,true)] [InlineData(101,true)] [InlineData(1000001,true)]
    public void OV11_RitualChecksAMinimumLevelAndRetainsAllOtherRequirements(int level,bool eligible)
    {
        var s=Reference() with { TotalXp=Progression.TotalXp(level),ValidatedLevel=level,AutoResonance=false,Might=0,Resolve=0,Seals=[120,120,120,120],Masteries=[..Enum.GetValues<Region>()],Proofs=EndgameRules.AllProofs,TrialCompleted=true };
        Assert.Equal(eligible,EndgameRules.Qualification(s).IsEmpty);
        Assert.NotEmpty(EndgameRules.Qualification(s with { Mode=ProfileMode.SliceSandbox }));
        Assert.NotEmpty(EndgameRules.Qualification(s with { TrialCompleted=false }));
    }
    [Fact]
    public void OV12_TrialActorNormalizesPowerWithoutReplacingPersistentXpEquipmentOrBuildChoices()
    {
        var original=Reference() with { TotalXp=Progression.TotalXp(BigInteger.Pow(10,30)),ValidatedLevel=BigInteger.Pow(10,30),Might=123,Resolve=456 };
        var weapon=original.Inventory[0] with { Id=Guid.NewGuid(),BaseValue=500 };
        original=original with { Inventory=original.Inventory.Add(weapon),Equipment=original.Equipment.SetItem(GearSlot.Weapon,weapon.Id) };
        var trial=EndgameRules.TrialActor(original);
        Assert.Equal(100,trial.ValidatedLevel);Assert.Equal(20,trial.Might);Assert.Equal(20,trial.Resolve);Assert.Equal(10,trial.AttunementGrade);Assert.False(trial.ElsewhereSelected);
        Assert.Equal(original.Bindings,trial.Bindings);Assert.Equal(original.SkillRanks,trial.SkillRanks);
        Assert.NotEqual(weapon.Id,trial.Equipment[GearSlot.Weapon]);
        var balance=ProfileLoader.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"arena.json")));Assert.NotNull(FoundationRules.Build(balance,trial));
        var complete=EndgameRules.TrialVictory(original,Guid.NewGuid());
        Assert.Equal(original.TotalXp,complete.TotalXp);Assert.Equal(original.Inventory,complete.Inventory);Assert.Equal(original.Might,complete.Might);Assert.Equal(original.Resolve,complete.Resolve);Assert.Equal(original.Gold,complete.Gold);Assert.Equal(original.HighestUnlockedTier,complete.HighestUnlockedTier);
    }
    [Fact]
    public void EP12_ChainExtractionDeathAndReplayedClaimsRetainBankedRewardsWithoutDuplicatingBonus()
    {
        var s=EndgameRules.StartChain(Reference(),10,Region.Ash);s=Clear(s,Region.Ash,ActivityFamily.Hunt);var bank=s;
        if(!s.ChapterOffers.IsEmpty)s=EndlessRules.ChooseRoute(s,s.ChapterOffers[0]);
        s=EndgameRules.Begin(s,10,Guid.NewGuid(),Region.Glass,ActivityFamily.Vault);s=EndlessRules.Claim(s,s.Fracture!.Id,s.Fracture.Sequence,0);
        var savedXp=s.TotalXp;var early=EndgameRules.Extract(s);Assert.Equal(savedXp,early.TotalXp);Assert.Equal(bank.Gold,early.Gold);Assert.Null(early.Fracture);Assert.True(early.Chain!.Closed);
        s=EndgameRules.FailLeg(s);Assert.False(s.Fracture!.ChainBonusEligible);
        for(var i=1;i<7;i++)s=EndlessRules.Claim(s,s.Fracture!.Id,s.Fracture.Sequence,i);
        Assert.Equal(bank.Gold+2000,s.Gold);Assert.Equal(bank.Alloy+1000,s.Alloy);Assert.True(s.TotalXp>savedXp);
        var afterSecond=s;s=Clear(s,Region.Hollow,ActivityFamily.Breach);
        Assert.True(s.Chain!.Closed);Assert.Equal(3,s.Chain.CompletedLegs);Assert.Equal(afterSecond.Gold+2400,s.Gold);Assert.Equal(afterSecond.Alloy+1200,s.Alloy);
        Assert.Same(s,EndlessRules.Claim(s,s.Fracture!.Id,s.Fracture.Sequence,6));
        var extracted=EndgameRules.Extract(EndgameRules.StartChain(s,10,Region.Ash));Assert.Equal(s.TotalXp,extracted.TotalXp);Assert.Equal(s.Gold,extracted.Gold);Assert.True(extracted.Chain!.Closed);
    }
    [Fact]
    public void EveryAllowedRulePairRetainsACommonMovementOnlyEscapeLaneAtHugeTiers()
    {
        foreach(var a in WorldLaws.Rules)foreach(var b in WorldLaws.Rules.Where(r=>r.Id!=a.Id))
        {
            Assert.True(WorldLaws.Compatible([a.Id,b.Id]));Assert.True(a.WarningTicks>=60&&b.WarningTicks>=60);
            foreach(var tick in new long[]{0,90,360,450,720})foreach(var block in WorldLaws.Areas(a.Id,tick).Concat(WorldLaws.Areas(b.Id,tick)))
                Assert.True(block.Y+block.Height<=160 || block.Y>=220);
        }
        var s=EndgameRules.Begin(EndlessFixtures.Reference(BigInteger.Pow(10,60)),BigInteger.Pow(10,60),Guid.NewGuid(),Region.Hollow,ActivityFamily.Hunt);
        Assert.Equal(7,s.Fracture!.Rooms.Length);Assert.Equal(2,s.Fracture.WorldRules.Length);
    }
    [Fact]
    public void StillnessAndRuptureRequireBaseEvidenceRespectMovementAndTargetLockout()
    {
        var m=new CombatMemories(new(96,120,300,120));m.SetCombatActive(true);m.ObservePosition(Vector2.Zero);m.AdvanceTo(48);
        Assert.False(m.ObserveBaseAssaultHit(Vector2.Zero,Vector2.UnitX,1,true,SourceKind.OverloadedPlayerAction));Assert.Null(m.Stillness);Assert.Null(m.Rupture);
        Assert.True(m.ObserveBaseAssaultHit(Vector2.Zero,Vector2.UnitX,1,true,SourceKind.BasePlayerAction));Assert.NotNull(m.Stillness);Assert.NotNull(m.Rupture);
        Assert.Equal(Vector2.UnitX,m.Stillness.Direction);Assert.Equal(Vector2.Zero,m.Stillness.Origin);Assert.Equal(1,m.Rupture.TargetId);
        var id=m.Rupture.Id;m.AdvanceTo(168);m.ObserveBaseAssaultHit(new(20,0),Vector2.UnitX,1,true,SourceKind.BasePlayerAction);Assert.Equal(id,m.Rupture!.Id);
        m.AdvanceTo(348);Assert.Null(m.Stillness);m.ObservePosition(new(40,0));Assert.False(m.ObserveBaseAssaultHit(new(40,0),Vector2.UnitX,1,true,SourceKind.BasePlayerAction));
        m.AdvanceTo(408);Assert.True(m.ObserveBaseAssaultHit(new(40,0),Vector2.UnitX,1,true,SourceKind.BasePlayerAction));m.Clear(MemoryClearReason.Death);Assert.Null(m.Stillness);Assert.Null(m.Rupture);
    }
    private sealed class World:IActionPreflight
    {public ActionWorldSnapshot Capture(ActionIntent intent,SkillDefinition skill,ActorBehaviorProfile behavior)=>new(0,Vector2.Zero,Enum.GetValues<ActionImplementation>().ToImmutableDictionary(i=>i,_=>new PreflightResult(true,"Ready",Vector2.Zero)));}
    [Fact]
    public void PairwiseAllAssaultsNewPatternsAndOathsConserveTokensFocusAndRootDamage()
    {
        var profile=ProfileLoader.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"arena.json")));
        foreach(var skill in profile.Skills.Where(s=>s.Family==ActionFamily.Assault))foreach(var name in new[]{"focused","shatter","cascade"})foreach(var oath in new[]{false,true})
        {
            var p=new PlayerCombat(profile,new World());Assert.True(p.TryChangeBindings([new("pattern.assault."+name,0)]));Assert.True(p.TryChangeOaths(oath?["oath.elsewhere"]:[]));p.SetCombatActive(true);
            p.Memories.ObservePosition(Vector2.Zero);for(var i=0;i<48;i++)p.AdvanceClock();p.Memories.ObserveBaseAssaultHit(Vector2.Zero,Vector2.UnitX,1,true,SourceKind.BasePlayerAction);
            var preview=p.Preview(skill.Id,Vector2.UnitX);Assert.True(p.TryCommit(preview));Assert.Equal(preview.Selection.Implementation,p.Action!.Implementation);
            Assert.Equal(p.MaximumFocus-skill.FocusCost*1000,p.Focus);Assert.False(p.TryCommit(preview));Assert.InRange(p.Strain,0,p.MaximumStrain);
            Assert.Equal(name=="shatter"||name=="cascade",p.Memories.Rupture is null);Assert.Equal(name=="focused"||name=="cascade",p.Memories.Stillness is null);
            var damage=PatternExecution.Damage(p.AttackBudget(skill),p.Action.Implementation);Assert.True(damage.First>0);Assert.Equal(0,damage.Repeat);
            if(name=="cascade")Assert.Equal(21,p.Action.Definition.Windup);
        }
    }
}
