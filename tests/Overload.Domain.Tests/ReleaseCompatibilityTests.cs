using System.Numerics;
using Overload.Content;
using Overload.Domain;
using Xunit;

namespace Overload.Domain.Tests;

public sealed class ReleaseCompatibilityTests : IDisposable
{
    private readonly string directory=Path.Combine(Path.GetTempPath(),"overload-release-"+Guid.NewGuid());
    public void Dispose(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
    private static CharacterState Huge()
    {
        var tier=BigInteger.Pow(10,40)+123;
        return EndlessFixtures.Reference(tier) with { Gold=BigInteger.Pow(10,90),Alloy=BigInteger.Pow(10,80),Seals=[1,2,3,BigInteger.Pow(10,70)],ElsewhereOwned=true,ElsewhereSelected=true };
    }
    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void OldHighLevelSnapshotsKeepExactProgressionAndEquipment(int schema)
    {
        var before=Huge() with { SchemaVersion=schema };
        var after=CharacterStore.Decode(CharacterStore.Encode(before));
        Assert.Equal(before.CharacterId,after.CharacterId);Assert.Equal(before.TotalXp,after.TotalXp);Assert.Equal(before.ValidatedLevel,after.ValidatedLevel);
        Assert.Equal(before.AttunementGrade,after.AttunementGrade);Assert.Equal(before.HighestClearedTier,after.HighestClearedTier);Assert.Equal(before.HighestUnlockedTier,after.HighestUnlockedTier);
        Assert.Equal(before.Gold,after.Gold);Assert.Equal(before.Alloy,after.Alloy);Assert.True(before.Seals.SequenceEqual(after.Seals));Assert.Equal(before.Might,after.Might);Assert.Equal(before.Resolve,after.Resolve);
        Assert.Equal(CharacterStore.Encode(before with { SchemaVersion=6 }),CharacterStore.Encode(after with { Journal=before.Journal }));
    }
    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void EachActiveRunVersionResumesItsOriginalGeometryAndBudgetAtHugeTier(int version)
    {
        ExpansionLoader.Load(ProfileLoader.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"arena.json"))),File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"expansion.json")));
        var before=Huge();var tier=before.HighestUnlockedTier;
        before=version==1?EndlessRules.Begin(before,tier,Guid.NewGuid()):EndgameRules.Begin(before,tier,Guid.NewGuid(),Region.Hollow,ActivityFamily.Vault,regionalContent:version==3);
        var run=before.Fracture!;
        for(var group=0;group<3;group++)before=EndlessRules.Claim(before,run.Id,run.Sequence,group);
        var store=new CharacterStore(directory,()=>before);var loaded=new CharacterStore(directory);
        var loadedRun=loaded.State.Fracture!;
        Assert.Equal(before.TotalXp,loaded.State.TotalXp);Assert.Equal((run.XpBudget,run.GoldBudget,run.AlloyBudget),(loadedRun.XpBudget,loadedRun.GoldBudget,loadedRun.AlloyBudget));Assert.Equal(3,loadedRun.NextGroup);
        Assert.True(before.Fracture!.Rooms.SequenceEqual(loadedRun.Rooms));Assert.Equal(before.Fracture.ContentVersion,loadedRun.ContentVersion);
        if(run.Layout is not null)Assert.True(ExpeditionGenerator.Same(run.Layout,loadedRun.Layout!));
        for(var group=3;group<7;group++)loaded.Transact(loaded.State.Revision,$"remaining-{group}",s=>EndlessRules.Claim(s,run.Id,run.Sequence,group));
        Assert.True(loaded.State.Fracture!.Completed);Assert.Equal(run.XpBudget,loaded.State.TotalXp-before.TotalXp+Enumerable.Range(0,3).Aggregate(BigInteger.Zero,(sum,g)=>sum+EndlessRules.GroupXp(run,g)));
        Assert.False(loaded.Transact(loaded.State.Revision,"remaining-6",s=>EndlessRules.Claim(s,run.Id,run.Sequence,6)));
    }
    [Theory]
    [InlineData("schema")] [InlineData("character-content")] [InlineData("expedition-content")] [InlineData("generator")]
    public void FutureVersionKeepsEveryFileAndNeverFallsBackToAnOlderBackup(string kind)
    {
        var s=Huge();if(kind is "expedition-content" or "generator")s=EndgameRules.Begin(s,s.HighestUnlockedTier,Guid.NewGuid(),Region.Ash,ActivityFamily.Hunt);
        var store=new CharacterStore(directory,()=>s);store.Transact(0,"backup",c=>c with { Gold=c.Gold+1 });
        var future=kind switch {
            "schema"=>store.State with { SchemaVersion=7 },
            "character-content"=>store.State with { ContentVersion="release.future" },
            "expedition-content"=>store.State with { Fracture=store.State.Fracture! with { ContentVersion="fracture.future" } },
            _=>store.State with { Fracture=store.State.Fracture! with { Layout=store.State.Fracture.Layout! with { Version="rooms.future" } } }
        };
        File.WriteAllText(Path.Combine(directory,"character.json"),CharacterStore.Encode(future));
        var files=Directory.GetFiles(directory).ToDictionary(p=>Path.GetFileName(p)!,p=>File.ReadAllBytes(p));
        Assert.Throws<FutureSaveException>(()=>new CharacterStore(directory));
        foreach(var pair in files)Assert.Equal(pair.Value,File.ReadAllBytes(Path.Combine(directory,pair.Key!)));
    }
    [Fact]
    public void MalformedContentVersionRecoversVerifiedBackupRatherThanCrashingTheLoader()
    {
        var store=new CharacterStore(directory, Huge);store.Transact(0,"backup",s=>s with { Gold=s.Gold+1 });
        var envelope=System.Text.Json.Nodes.JsonNode.Parse(CharacterStore.Encode(store.State))!;
        var payload=System.Text.Json.Nodes.JsonNode.Parse(envelope["Payload"]!.GetValue<string>())!;
        payload["ContentVersion"]=123;var text=payload.ToJsonString();envelope["Payload"]=text;
        envelope["Sha256"]=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));
        File.WriteAllText(Path.Combine(directory,"character.json"),envelope.ToJsonString());
        var recovered=new CharacterStore(directory);
        Assert.Equal(store.State.Gold-1,recovered.State.Gold);Assert.Contains("Recovered",recovered.Notice);
    }
}
