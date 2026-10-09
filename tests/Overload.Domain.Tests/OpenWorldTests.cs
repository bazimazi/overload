using System.Numerics;
using Overload.Content;
using Overload.Domain;
using Xunit;

namespace Overload.Domain.Tests;

public sealed class OpenWorldTests
{
    private static CharacterState Fresh()=>WorldRules.Enroll(FrameRules.Create(FrameId.Warden));
    [Fact] public void LevelOneCharacterStartsAtChartedHearthWithCampaignAndFrontierRoads()
    {
        var s=Fresh();Assert.Equal(BigInteger.One,s.ValidatedLevel);Assert.Equal("hearth",s.World!.ActiveZone.Id);
        s=WorldRules.Travel(s,"frontier");Assert.Equal("frontier.0",s.World!.ActiveZone.Id);
        Assert.Empty(s.World.Resolved);Assert.False(s.FractureUnlocked);CharacterRules.Validate(s);
        s=WorldRules.Travel(s,"back");Assert.Equal("hearth",s.World!.ActiveZone.Id);
        s=WorldRules.Travel(s,"road.0");Assert.Equal(5760,s.World!.ActiveZone.Geometry.Bounds.Width);Assert.Equal(3456,s.World.ActiveZone.Geometry.Bounds.Height);
        Assert.True(s.World.ActiveZone.Encounters.Length>16);
    }
    [Fact] public void ManyFrontierSeedsHaveReachableAnchorsAndStableDistinctTerrain()
    {
        var campaign=Guid.Parse("d19b537d-3d05-4ff1-8399-b4f569761037");var layouts=new HashSet<string>();
        for(var depth=0;depth<64;depth++)
        {
            var z=FrontierWorld.Generate(campaign,depth);WorldContent.Validate(z);
            Assert.Equal(z.Geometry.Bounds,FrontierWorld.Generate(campaign,depth).Geometry.Bounds);
            Assert.Equal(z.Geometry.Blocks.ToArray(),FrontierWorld.Generate(campaign,depth).Geometry.Blocks.ToArray());
            layouts.Add(string.Join(';',z.Geometry.Blocks));
            Assert.Equal(6144,z.Geometry.Bounds.Width);Assert.Equal(3840,z.Geometry.Bounds.Height);
            var nav=z.Geometry.Navigation();
            foreach(var radius in new[]{7f,17f,36f})
            {
                var from=z.Arrival.Vector;var to=z.Exits.Single(e=>e.Id=="onward").Position.Vector;
                var path=nav.FindPath(from,to,radius);Assert.NotEmpty(path);
                foreach(var p in path){Assert.True(nav.Clear(from,p,radius));from=p;}
            }
        }
        Assert.Equal(64,layouts.Count);
        Assert.NotEqual(layouts.First(),string.Join(';',FrontierWorld.Generate(Guid.NewGuid(),0).Geometry.Blocks));
    }
    [Fact] public void FrontierLootFogCheckpointAndBacktrackingSurviveReloadWithoutDuplicateRewards()
    {
        var s=WorldRules.Travel(Fresh(),"frontier");var w=s.World!;var encounter=w.ActiveZone.Encounters.First();
        s=WorldRules.ClaimEncounter(s,w.CampaignId,w.ActiveZone.Id,encounter.Id);var xp=s.TotalXp;
        s=WorldRules.Interact(s,"frontier.0.camp");s=WorldRules.Reveal(s,new(3000,1920));
        s=WorldRules.Park(s,new(3100,1920));var loaded=CharacterStore.Decode(CharacterStore.Encode(s));
        Assert.Equal(new WorldPoint(3100,1920),loaded.World!.Arrival);Assert.NotEmpty(WorldRules.ZoneFog(loaded.World));
        Assert.Equal("frontier.0",WorldRules.Resume(loaded).World!.ActiveZone.Id);
        Assert.Equal(new WorldPoint(3000,1920),WorldRules.Resume(loaded).World!.Arrival);
        s=WorldRules.Travel(loaded,"onward");Assert.Equal("frontier.1",s.World!.ActiveZone.Id);
        s=WorldRules.Travel(s,"back");Assert.Equal(xp,WorldRules.ClaimEncounter(s,w.CampaignId,"frontier.0",encounter.Id).TotalXp);
        s=WorldRules.FastTravel(s,"hearth.waypoint");s=WorldRules.FastTravel(s,"frontier.0.camp");
        Assert.Equal("frontier.0",s.World!.ActiveZone.Id);CharacterRules.Validate(s);
    }
    [Fact] public void CachesCannotBeClaimedTwiceAndFrontierBossesCannotResolveTheCampaign()
    {
        var s=WorldRules.Travel(Fresh(),"frontier");var w=s.World!;
        Assert.Throws<InvalidOperationException>(()=>WorldRules.Interact(s,"frontier.0.cache.north"));
        foreach(var id in new[]{"frontier.0.pack.3","frontier.0.guardian"})s=WorldRules.ClaimEncounter(s,w.CampaignId,"frontier.0",id);
        s=WorldRules.Interact(s,"frontier.0.cache.north");var rewarded=s;
        s=WorldRules.Interact(s,"frontier.0.cache.north");Assert.Equal(rewarded.TotalXp,s.TotalXp);Assert.Equal(rewarded.Gold,s.Gold);
        Assert.Empty(s.World!.Resolved);Assert.Empty(s.Proofs);Assert.Empty(s.Masteries);Assert.False(s.FractureUnlocked);
    }
    [Fact] public void LongExplorationBoundsSaveGrowthAndRetiredReceiptsStaySpent()
    {
        var s=WorldRules.Travel(Fresh(),"frontier");var first=s.World!;
        for(var depth=0;depth<80;depth++)
        {
            var e=s.World!.ActiveZone.Encounters.First();s=WorldRules.ClaimEncounter(s,s.World.CampaignId,s.World.ActiveZone.Id,e.Id);
            s=WorldRules.SaveFog(s,[..Enumerable.Range(0,2560)]);s=WorldRules.Travel(s,"onward");
        }
        var loaded=CharacterStore.Decode(CharacterStore.Encode(s));var f=loaded.World!.Frontier;
        Assert.Equal(80,f.Farthest);Assert.Equal(32,f.Surveys.Count);Assert.Equal(48,f.RetiredThrough);
        var saveBytes=System.Text.Encoding.UTF8.GetByteCount(CharacterStore.Encode(loaded));
        Assert.True(saveBytes<2*1024*1024,$"Fully surveyed retained reaches require {saveBytes} bytes");
        for(var depth=80;depth>0;depth--)loaded=WorldRules.Travel(loaded,"back");
        var before=loaded.TotalXp;
        loaded=WorldRules.ClaimEncounter(loaded,first.CampaignId,"frontier.0",first.ActiveZone.Encounters[0].Id);
        Assert.Equal(before,loaded.TotalXp);CharacterRules.Validate(loaded);
    }
    [Fact] public void LargeNavigationRespectsClosedWallsAndOverlappingStructures()
    {
        var blocked=new TacticalNavigation([new(2900,0,200,3840)],new(0,0,6144,3840));
        Assert.Empty(blocked.FindPath(new(400,1920),new(5800,1920),17));
        var nav=new TacticalNavigation([new(2400,1300,500,1300),new(2800,1800,700,400)],new(0,0,6144,3840));
        var from=new Vector2(180,1920);var path=nav.FindPath(from,new(6000,1920),36);Assert.NotEmpty(path);
        foreach(var p in path){Assert.True(nav.Clear(from,p,36));from=p;}
    }
    [Fact] public void FrontierRejectsSkippedRoadsStaleClaimsAndInvalidSurveyState()
    {
        var s=WorldRules.Travel(Fresh(),"frontier");
        Assert.Throws<InvalidOperationException>(()=>WorldRules.Enter(s,"frontier.20",new(180,1920)));
        Assert.Throws<InvalidOperationException>(()=>WorldRules.ClaimEncounter(s,Guid.NewGuid(),"frontier.0","frontier.0.pack.0"));
        Assert.Throws<InvalidDataException>(()=>CharacterRules.Validate(s with {World=s.World! with {Frontier=s.World.Frontier with {Farthest=-2}}}));
        Assert.False(FrontierWorld.TryDepth("frontier.00",out _));Assert.False(FrontierWorld.TryDepth("frontier.-1",out _));
        Assert.Throws<FutureSaveException>(()=>CharacterStore.Decode(CharacterStore.Encode(s with {World=s.World! with {Frontier=s.World.Frontier with {Version="frontier.future"}}})));
    }
    [Fact] public void OlderWorldSavesUpgradeTerrainFogAndHearthRoadWithoutResettingTheCharacter()
    {
        var s=Fresh();var current=WorldContent.Zone("ash.0");
        var old=current with {Geometry=new(new(0,0,1920,1152),[..current.Geometry.Blocks.Select(b=>new RoomBlock(b.X/3,b.Y/3,b.Width/3,b.Height/3))])};
        s=s with {World=s.World! with {GeographyVersion=1,ActiveZone=old,Arrival=new(400,560),Fog=s.World.Fog.SetItem("ash.0",[0,21]),
            SafeSnapshot=WorldContent.Zone("hearth") with {Exits=[..WorldContent.Zone("hearth").Exits.Where(e=>e.Id!="frontier")]}}};
        var loaded=CharacterStore.Decode(CharacterStore.Encode(s));var upgraded=WorldRules.UpgradeGeography(loaded);
        Assert.Equal(5760,upgraded.World!.ActiveZone.Geometry.Bounds.Width);Assert.Equal(new WorldPoint(1200,1680),upgraded.World.Arrival);
        Assert.Equal(18,upgraded.World.Fog["ash.0"].Count);Assert.Contains(upgraded.World.SafeSnapshot.Exits,e=>e.Id=="frontier");
        Assert.Equal(s.CharacterId,upgraded.CharacterId);Assert.Equal(s.TotalXp,upgraded.TotalXp);Assert.Equal(s.Gold,upgraded.Gold);
        Assert.Equal(upgraded,WorldRules.UpgradeGeography(upgraded));CharacterRules.Validate(upgraded);
    }
}
