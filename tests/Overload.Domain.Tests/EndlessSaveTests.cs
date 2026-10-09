using System.Numerics;
using Overload.Content;
using Overload.Domain;
using Xunit;

namespace Overload.Domain.Tests;
public sealed class EndlessSaveTests : IDisposable
{
    private readonly string directory=Path.Combine(Path.GetTempPath(),"overload-endless-"+Guid.NewGuid());
    public void Dispose()
    {
        if (!Path.GetFullPath(directory).StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException();
        if (Directory.Exists(directory)) Directory.Delete(directory,true);
    }
    [Theory]
    [InlineData(SaveStage.TemporaryWritten)] [InlineData(SaveStage.Flushed)] [InlineData(SaveStage.Verified)] [InlineData(SaveStage.BeforeReplace)] [InlineData(SaveStage.Replaced)]
    public void EP09_CrashAtChapterCompletionRetainsOneAtomicBudgetAndStableOffers(SaveStage stage)
    {
        var state=EndlessRules.Begin(EndlessTests.Reference(10),10,Guid.NewGuid()); var run=state.Fracture!;
        for(var i=0;i<6;i++) state=EndlessRules.Claim(state,run.Id,run.Sequence,i);
        var store=new CharacterStore(directory,()=>state); var expected=EndlessRules.Claim(state,run.Id,run.Sequence,6);
        store.Fault=s=> { if(s==stage) throw new IOException("Injected interruption"); };
        Assert.Throws<IOException>(()=>store.Transact(0,"complete",s=>EndlessRules.Claim(s,run.Id,run.Sequence,6)));
        var recovered=new CharacterStore(directory);
        if (!recovered.State.Fracture!.Completed) recovered.Transact(recovered.State.Revision,"complete",s=>EndlessRules.Claim(s,run.Id,run.Sequence,6));
        Assert.Equal(expected.TotalXp,recovered.State.TotalXp); Assert.Equal(expected.Gold,recovered.State.Gold); Assert.Equal(expected.Alloy,recovered.State.Alloy);
        Assert.Equal(11,recovered.State.HighestUnlockedTier); Assert.True(expected.ChapterOffers.SequenceEqual(recovered.State.ChapterOffers));
        Assert.False(recovered.Transact(recovered.State.Revision,"complete",s=>EndlessRules.Claim(s,run.Id,run.Sequence,6)));
        var route=recovered.State.ChapterOffers[0];
        recovered.Fault=s=> { if(s==stage) throw new IOException("Injected route interruption"); };
        Assert.Throws<IOException>(()=>recovered.Transact(recovered.State.Revision,"choose",s=>EndlessRules.ChooseRoute(s,route)));
        var reload=new CharacterStore(directory);
        if (!reload.State.ChapterOffers.IsEmpty) reload.Transact(reload.State.Revision,"choose",s=>EndlessRules.ChooseRoute(s,route));
        Assert.Equal(route,reload.State.SelectedRoute); Assert.Equal(2,reload.State.Chapter); Assert.Equal(expected.TotalXp,reload.State.TotalXp);
    }
    [Theory]
    [InlineData(SaveStage.BeforeReplace)] [InlineData(SaveStage.Replaced)]
    public void EP08_InterruptedBulkForgeNeverSplitsGradeAndBothWalletDebits(SaveStage stage)
    {
        var before=EndlessTests.Reference(101) with { AttunementGrade=1 }; var expected=EndlessRules.Forge(before,100);
        var store=new CharacterStore(directory,()=>before); store.Fault=s=> { if(s==stage) throw new IOException("Injected forge interruption"); };
        Assert.Throws<IOException>(()=>store.Transact(0,"forge",s=>EndlessRules.Forge(s,100)));
        var recovered=new CharacterStore(directory);
        if(recovered.State.AttunementGrade==1) recovered.Transact(recovered.State.Revision,"forge",s=>EndlessRules.Forge(s,100));
        Assert.Equal(expected.Gold,recovered.State.Gold); Assert.Equal(expected.Alloy,recovered.State.Alloy); Assert.Equal(100,recovered.State.AttunementGrade);
        Assert.False(recovered.Transact(recovered.State.Revision,"forge",s=>EndlessRules.Forge(s,100)));
    }
    [Fact]
    public void SchemaTwoMigratesWithoutChangingCourtItemsOrWalletsAndOversizeInputRecovers()
    {
        var old=new CharacterState { SchemaVersion=2, TotalXp=Progression.TotalXp(1000),ValidatedLevel=1000 };
        var migrated=CharacterStore.Decode(CharacterStore.Encode(old));
        Assert.Equal(8,migrated.SchemaVersion); Assert.Equal(old.Gold,migrated.Gold); Assert.Equal(old.TotalXp,migrated.TotalXp);
        Assert.Equal(old.Inventory.Length,migrated.Inventory.Length); Assert.Null(migrated.Fracture);
        var store=new CharacterStore(directory); store.Transact(0,"backup",s=>s with { Gold=s.Gold+1 });
        File.WriteAllText(Path.Combine(directory,"character.json"),new string('x',CharacterStore.MaximumSaveBytes+1));
        var recovered=new CharacterStore(directory); Assert.Equal(500,recovered.State.Gold); Assert.Contains("Recovered",recovered.Notice);
    }
    [Fact]
    public void MutatedImmutableRunBudgetAndInconsistentFrontierFailValidation()
    {
        var s=EndlessRules.Begin(EndlessTests.Reference(1000),1000,Guid.NewGuid());
        Assert.Throws<InvalidDataException>(()=>CharacterStore.Decode(CharacterStore.Encode(s with { Fracture=s.Fracture! with { XpBudget=s.Fracture.XpBudget+1 } })));
        Assert.Throws<InvalidDataException>(()=>CharacterRules.Validate(s with { HighestUnlockedTier=1002 }));
        Assert.Throws<FutureSaveException>(()=>CharacterStore.Decode(CharacterStore.Encode(s with { Fracture=s.Fracture! with { ContentVersion="fracture.v99" } })));
    }
}
