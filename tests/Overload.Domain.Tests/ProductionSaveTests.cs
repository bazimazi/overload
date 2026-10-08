using Overload.Content;
using Overload.Domain;
using Xunit;

namespace Overload.Domain.Tests;
public sealed class ProductionSaveTests:IDisposable
{
    private readonly string directory=Path.Combine(Path.GetTempPath(),"overload-production-"+Guid.NewGuid());
    public void Dispose(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
    [Theory]
    [InlineData(SaveStage.TemporaryWritten)] [InlineData(SaveStage.Flushed)] [InlineData(SaveStage.Verified)] [InlineData(SaveStage.BeforeReplace)] [InlineData(SaveStage.Replaced)]
    public void RitualFaultRecoversEitherCompleteOwnershipAndDebitOrUnchangedQualification(SaveStage stage)
    {
        var s=ProductionTests.Reference() with { Seals=[120,120,120,120],Proofs=EndgameRules.AllProofs,Masteries=[..Enum.GetValues<Region>()],TrialCompleted=true };
        var store=new CharacterStore(directory,()=>s);store.Fault=x=>{if(x==stage)throw new IOException("Injected ritual interruption");};
        Assert.Throws<IOException>(()=>store.Transact(0,"ritual",EndgameRules.Ritual));var loaded=new CharacterStore(directory);
        if(!loaded.State.ElsewhereOwned)loaded.Transact(loaded.State.Revision,"ritual",EndgameRules.Ritual);
        Assert.True(loaded.State.ElsewhereOwned);Assert.All(loaded.State.Seals,v=>Assert.Equal(0,v));Assert.False(loaded.Transact(loaded.State.Revision,"ritual",EndgameRules.Ritual));
    }
    [Theory]
    [InlineData(SaveStage.TemporaryWritten)] [InlineData(SaveStage.Flushed)] [InlineData(SaveStage.Verified)] [InlineData(SaveStage.BeforeReplace)] [InlineData(SaveStage.Replaced)]
    public void EP12_InterruptedChainBonusAndLegCheckpointRecoverTogether(SaveStage stage)
    {
        var s=ProductionTests.Clear(EndgameRules.StartChain(ProductionTests.Reference(),10,Region.Ash),Region.Ash,ActivityFamily.Hunt);
        if(!s.ChapterOffers.IsEmpty)s=EndlessRules.ChooseRoute(s,s.ChapterOffers[0]);s=EndgameRules.Begin(s,10,Guid.NewGuid(),Region.Glass,ActivityFamily.Breach);
        for(var i=0;i<6;i++)s=EndlessRules.Claim(s,s.Fracture!.Id,s.Fracture.Sequence,i);var run=s.Fracture!;var expected=EndlessRules.Claim(s,run.Id,run.Sequence,6);
        var store=new CharacterStore(directory,()=>s);store.Fault=x=>{if(x==stage)throw new IOException("Injected leg interruption");};
        Assert.Throws<IOException>(()=>store.Transact(0,"leg2",c=>EndlessRules.Claim(c,run.Id,run.Sequence,6)));var loaded=new CharacterStore(directory);
        if(!loaded.State.Fracture!.Completed)loaded.Transact(loaded.State.Revision,"leg2",c=>EndlessRules.Claim(c,run.Id,run.Sequence,6));
        Assert.Equal(expected.Gold,loaded.State.Gold);Assert.Equal(expected.Alloy,loaded.State.Alloy);Assert.Equal(expected.TotalXp,loaded.State.TotalXp);Assert.Equal(2,loaded.State.Chain!.CompletedLegs);Assert.Single(loaded.State.RunRecords.Where(r=>r.RunId==run.Id));
        Assert.False(loaded.Transact(loaded.State.Revision,"leg2",c=>EndlessRules.Claim(c,run.Id,run.Sequence,6)));
    }
    [Fact]
    public void TrialProofCrashRecoveryPreservesOriginalPowerAndCannotFarmRewards()
    {
        var original=ProductionTests.Reference();var store=new CharacterStore(directory,()=>original);store.Fault=x=>{if(x==SaveStage.BeforeReplace)throw new IOException("Injected trial proof interruption");};
        var attempt=Guid.NewGuid();Assert.Throws<IOException>(()=>store.Transact(0,"trial",s=>EndgameRules.TrialVictory(s,attempt)));
        var loaded=new CharacterStore(directory);if(!loaded.State.TrialCompleted)loaded.Transact(loaded.State.Revision,"trial",s=>EndgameRules.TrialVictory(s,attempt));
        Assert.True(loaded.State.TrialCompleted);Assert.Equal(original.TotalXp,loaded.State.TotalXp);Assert.Equal(System.Text.Json.JsonSerializer.Serialize(original.Inventory,CharacterStore.JsonOptions),System.Text.Json.JsonSerializer.Serialize(loaded.State.Inventory,CharacterStore.JsonOptions));Assert.Equal(original.Gold,loaded.State.Gold);Assert.Equal(original.Might,loaded.State.Might);Assert.Equal(original.AttunementGrade,loaded.State.AttunementGrade);
        Assert.False(loaded.Transact(loaded.State.Revision,"trial",s=>EndgameRules.TrialVictory(s,attempt)));
    }
    [Fact]
    public void SchemaThreeActiveExpeditionRemainsLoadableAndUnknownGeneratorsAreRejected()
    {
        var legacy=EndlessRules.Begin(ProductionTests.Reference(),10,Guid.NewGuid()) with { SchemaVersion=3 };
        var loaded=CharacterStore.Decode(CharacterStore.Encode(legacy));Assert.Equal(6,loaded.SchemaVersion);Assert.Equal(legacy.Fracture!.Id,loaded.Fracture!.Id);Assert.True(legacy.Fracture.Rooms.SequenceEqual(loaded.Fracture.Rooms));
        var regional=EndgameRules.Begin(ProductionTests.Reference(),10,Guid.NewGuid(),Region.Ash,ActivityFamily.Hunt);
        Assert.Throws<FutureSaveException>(()=>CharacterStore.Decode(CharacterStore.Encode(regional with { Fracture=regional.Fracture! with { Layout=regional.Fracture.Layout! with { Version="rooms.future" } } })));
    }
}
