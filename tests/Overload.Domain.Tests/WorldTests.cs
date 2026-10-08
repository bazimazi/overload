using System.Numerics;
using Overload.Content;
using Overload.Domain;
using Xunit;

namespace Overload.Domain.Tests;

public sealed class WorldTests
{
    private static CharacterState Fresh()=>WorldRules.Enroll(FrameRules.Create(FrameId.Warden));
    [Fact] public void AllSeventeenZonesHaveReachablePhysicalAnchors()
    {
        Assert.Equal(17,WorldContent.Zones.Count);
        foreach(var zone in WorldContent.Zones.Values)
        {
            WorldContent.Validate(zone);
            foreach(var exit in zone.Exits){Assert.True(WorldContent.Zones.ContainsKey(exit.Destination));Assert.True(WorldContent.Zone(exit.Destination).Geometry.Navigation().Clear(exit.Arrival.Vector,exit.Arrival.Vector,20));}
        }
    }
    [Fact] public void BothFoundryForksReconnectForEveryFrameWithoutAnOath()
    {
        var nav=WorldContent.Zone("ash.0").Geometry.Navigation();
        foreach(var route in new[]{new Vector2[]{new(140,560),new(610,290),new(1260,290),new(1710,560)},[new(140,560),new(620,850),new(1210,920),new(1710,560)]})
            foreach(var radius in new[]{8f,12f,20f})for(var i=1;i<route.Length;i++)Assert.NotEmpty(nav.FindPath(route[i-1],route[i],radius));
    }
    [Fact] public void RealTravelHonorsGatesAndWorksInBothDirections()
    {
        var s=Fresh();Assert.Throws<InvalidOperationException>(()=>WorldRules.Travel(s,"road.1"));
        s=WorldRules.Travel(s,"road.0");Assert.Equal("ash.0",s.World!.ActiveZone.Id);
        s=WorldRules.Travel(s,"home");Assert.Equal("hearth",s.World!.ActiveZone.Id);Assert.Contains("ash.0",s.World.Visited);
    }
    [Fact] public void RefugeRewardWaypointShortcutAndCheckpointCommitTogether()
    {
        var s=WorldRules.Travel(Fresh(),"road.0");var w=s.World!;var claim="ash.0.pack.6";
        Assert.Throws<InvalidOperationException>(()=>WorldRules.Interact(s,"ash.0.refuge"));
        s=WorldRules.ClaimEncounter(s,w.CampaignId,"ash.0",claim);var rewarded=s;
        s=WorldRules.Interact(s,"ash.0.refuge");Assert.Contains("ash.0.refuge",s.World!.Claims);Assert.Contains("ash.0.refuge",s.World.Waypoints);Assert.Equal("ash.0",s.World.SafeZone);
        Assert.Equal(rewarded.TotalXp,s.TotalXp);Assert.Equal(rewarded.Gold,s.Gold);
        Assert.Equal(s.TotalXp,WorldRules.ClaimEncounter(s,w.CampaignId,"ash.0",claim).TotalXp);
        s=WorldRules.Interact(s,"ash.0.shortcut");Assert.Equal("hearth",s.World!.ActiveZone.Id);
        s=WorldRules.FastTravel(s,"ash.0.refuge");Assert.Equal("ash.0",s.World!.ActiveZone.Id);
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)]
    public void RegionalMainManifestMatchesFourLegacyCheckpoints(int region)
    {
        var rewards=WorldContent.Zones.Values.Where(z=>z.Id!="hearth"&&(int)z.Region==region)
            .SelectMany(z=>z.Encounters.Where(e=>!e.Optional).Select(e=>e.Reward).Concat(z.Sites.Where(p=>p.Reward is not null).Select(p=>p.Reward!))).ToArray();
        Assert.Equal(6000+3200*region,rewards.Sum(r=>r.Xp));Assert.Equal(550+400*region,rewards.Sum(r=>r.Gold));Assert.Equal(32,rewards.Sum(r=>r.Alloy));Assert.Equal(4,rewards.Count(r=>r.Item>=0));
    }
    [Fact] public void BossDevicesAndStoryGatesCannotSoftlockOrGrantOathQualification()
    {
        var s=Fresh();
        foreach(var region in Enum.GetValues<Region>())
        {
            s=WorldRules.Enter(s,WorldContent.Id(region,2),new(140,520));Assert.Throws<InvalidOperationException>(()=>WorldRules.Travel(s,"ruler"));
            foreach(var e in s.World!.ActiveZone.Encounters.Where(e=>!e.Optional))s=WorldRules.ClaimEncounter(s,s.World!.CampaignId,s.World.ActiveZone.Id,e.Id);
            foreach(var p in s.World!.ActiveZone.Sites.Where(p=>p.Kind=="device"))s=WorldRules.Interact(s,p.Id);
            Assert.False(WorldRules.Satisfied(s.World!,$"{WorldContent.Id(region,2)}.ready"));
            s=WorldRules.Enter(s,WorldContent.Id(region,1),new(140,384));s=WorldRules.ClaimEncounter(s,s.World!.CampaignId,s.World.ActiveZone.Id,s.World.ActiveZone.Encounters[0].Id);
            s=WorldRules.Enter(s,WorldContent.Id(region,2),new(140,520));s=WorldRules.Travel(s,"ruler");
            s=WorldRules.ClaimEncounter(s,s.World!.CampaignId,s.World.ActiveZone.Id,s.World.ActiveZone.Encounters[0].Id);CharacterRules.Validate(s);
        }
        Assert.True(s.FractureUnlocked);Assert.Empty(s.Proofs);Assert.Empty(s.Masteries);Assert.All(s.Seals,v=>Assert.Equal(BigInteger.Zero,v));
        s=WorldRules.End(s,"preserve");Assert.Equal("preserve",s.World!.Ending);Assert.Equal("preserve",WorldRules.End(s,"bind").World!.Ending);
    }
    [Fact] public void FogAndSafeResumeSurviveSerializationWithoutInferringLegacyDiscovery()
    {
        var old=FrameRules.Create(FrameId.Warden) with {SchemaVersion=5};var migrated=CharacterStore.Decode(CharacterStore.Encode(old));Assert.Null(migrated.World);Assert.Equal(6,migrated.SchemaVersion);
        var s=WorldRules.Reveal(WorldRules.Travel(Fresh(),"road.0"),new(400,560));s=CharacterStore.Decode(CharacterStore.Encode(s));
        Assert.NotEmpty(s.World!.Fog["ash.0"]);Assert.Equal("hearth",WorldRules.Resume(s).World!.ActiveZone.Id);
        Assert.Throws<FutureSaveException>(()=>CharacterStore.Decode(CharacterStore.Encode(s with {World=s.World with {Version="world.future"}})));
    }
    [Theory][InlineData(ActivityFamily.Hunt)][InlineData(ActivityFamily.Breach)][InlineData(ActivityFamily.Vault)]
    public void GraphObjectivesAreIdempotentAndFixedBudgetsStayExact(ActivityFamily family)
    {
        var s=FrameRules.Create(FrameId.Warden) with {FractureUnlocked=true};s=FractureMapRules.Begin(s,1,Guid.NewGuid(),Region.Ash,family);var run=s.Fracture!;var xp=s.TotalXp;var gold=s.Gold;var alloy=s.Alloy;
        Assert.False(run.Completed);Assert.Equal(0,run.NextGroup);Assert.Throws<InvalidOperationException>(()=>EndlessRules.Claim(s,run.Id,run.Sequence,0));
        foreach(var e in run.Map!.Zone.Encounters.Where(e=>e.Id!="map.boss"))s=FractureMapRules.Claim(s,run.Id,e.Id);
        foreach(var p in run.Map.Zone.Sites.Where(p=>p.Kind=="cache"))s=FractureMapRules.Claim(s,run.Id,p.Id);
        foreach(var p in run.Map.Zone.Sites.Where(p=>p.Kind!="cache"&&p.Id!="map.keystone"))s=FractureMapRules.Claim(s,run.Id,p.Id);
        s=FractureMapRules.Claim(s,run.Id,"map.boss");
        if(family==ActivityFamily.Vault)s=FractureMapRules.Claim(s,run.Id,"map.keystone");
        Assert.True(s.Fracture!.Completed);Assert.Equal(run.XpBudget,s.TotalXp-xp);Assert.Equal(run.GoldBudget,s.Gold-gold);Assert.Equal(run.AlloyBudget,s.Alloy-alloy);
        CharacterRules.Validate(s);Assert.Equal(s.TotalXp,FractureMapRules.Claim(s,run.Id,"map.boss").TotalXp);
        var loaded=CharacterStore.Decode(CharacterStore.Encode(s));Assert.Equal(run.Map.Digest,loaded.Fracture!.Map!.Digest);
    }
    [Fact] public void GeneratedPhysicalRoutesUseFullSeedAndReachAllObjectives()
    {
        var signatures=new HashSet<string>();
        for(ulong seed=0;seed<64;seed++)
        {
            var map=FractureMapGenerator.Generate(seed,(Region)(seed%4),(ActivityFamily)(seed%3));WorldContent.Validate(map.Zone);
            signatures.Add(string.Join(',',map.Edges.OrderBy(e=>e.A).ThenBy(e=>e.B)));
            Assert.Equal(map.Digest,FractureMapGenerator.Generate(seed,(Region)(seed%4),(ActivityFamily)(seed%3)).Digest);
            Assert.NotEqual(map.Digest,FractureMapGenerator.Generate(seed+(1UL<<32),(Region)(seed%4),(ActivityFamily)(seed%3)).Digest);
        }
        Assert.True(signatures.Count>40);
    }
    [Theory][InlineData(SaveStage.BeforeReplace)][InlineData(SaveStage.Replaced)]
    public void WorldReceiptsRecoverAtomicallyAfterInterruptedSave(SaveStage fault)
    {
        var path=Path.Combine(Path.GetTempPath(),"overload-world-"+Guid.NewGuid());
        try
        {
            var store=new CharacterStore(path,()=>WorldRules.Travel(Fresh(),"road.0"));var w=store.State.World!;
            store.Fault=stage=>{if(stage==fault)throw new IOException("Interrupted");};
            Assert.Throws<IOException>(()=>store.Transact(0,"refuge-defenders",s=>WorldRules.ClaimEncounter(s,w.CampaignId,"ash.0","ash.0.pack.6")));
            var recovered=new CharacterStore(path);Assert.Contains("ash.0.pack.6",recovered.State.World!.Claims);var xp=recovered.State.TotalXp;
            Assert.False(recovered.Transact(recovered.State.Revision,"refuge-defenders",s=>WorldRules.ClaimEncounter(s,w.CampaignId,"ash.0","ash.0.pack.6")));Assert.Equal(xp,recovered.State.TotalXp);
        }
        finally{Directory.Delete(path,true);}
    }
    [Fact] public void HugeTierMapKeepsExactCountersAndFrozenLayoutAcrossPartialReload()
    {
        var tier=BigInteger.Pow(10,80)+123;
        var s=EndlessFixtures.Reference(tier);s=FractureMapRules.Begin(s,tier,Guid.NewGuid(),Region.Crown,ActivityFamily.Vault);var run=s.Fracture!;
        var guard=run.Map!.Zone.Encounters.First(e=>e.Boss<0);s=FractureMapRules.Claim(s,run.Id,guard.Id);
        var loaded=CharacterStore.Decode(CharacterStore.Encode(s));Assert.Equal(tier,loaded.Fracture!.Tier);Assert.Equal(run.XpBudget,loaded.Fracture.XpBudget);Assert.Equal(run.Map.Digest,loaded.Fracture.Map!.Digest);Assert.Contains(guard.Id,loaded.Fracture.Map.Claims);
        Assert.Throws<FutureSaveException>(()=>CharacterStore.Decode(CharacterStore.Encode(s with {Fracture=s.Fracture! with {Map=s.Fracture.Map! with {GeneratorVersion="maps.future"}}})));
    }
    [Fact] public void CompletedLegacyCampaignEntersGeographyWithoutDuplicateFirstClearRewards()
    {
        var s=FrameRules.Create(FrameId.Warden);s=JourneyRules.Begin(s,Guid.NewGuid());for(var room=0;room<16;room++)s=JourneyRules.Clear(s,s.RunId,room);
        var before=s;s=WorldRules.Enroll(s);Assert.Empty(s.World!.Resolved);Assert.Equal(before.FractureUnlocked,s.FractureUnlocked);
        s=WorldRules.Enter(s,"ash.3",new(140,384));s=WorldRules.ClaimEncounter(s,s.World!.CampaignId,"ash.3","ash.3.boss");
        Assert.Equal(before.TotalXp,s.TotalXp);Assert.Equal(before.Gold,s.Gold);Assert.Equal(before.Alloy,s.Alloy);Assert.Equal(before.Inventory.Length,s.Inventory.Length);Assert.Contains(Region.Ash,s.World!.Resolved);CharacterRules.Validate(s);
    }
}
