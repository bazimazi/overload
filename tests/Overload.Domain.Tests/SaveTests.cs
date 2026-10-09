using System.Numerics;
using Overload.Content;
using Overload.Domain;
using Xunit;

namespace Overload.Domain.Tests;
public sealed class SaveTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "overload-save-test-" + Guid.NewGuid());
    public void Dispose()
    {
        if (!Path.GetFullPath(directory).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Test cleanup escaped temporary directory");
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
    [Theory]
    [InlineData(SaveStage.TemporaryWritten)] [InlineData(SaveStage.Flushed)] [InlineData(SaveStage.Verified)] [InlineData(SaveStage.BeforeReplace)] [InlineData(SaveStage.Replaced)]
    public void InterruptedRitualRecoversOnlyWholeOwnershipAndPayment(SaveStage stage)
    {
        var store = new CharacterStore(directory); store.Fault = s => { if (s == stage) throw new IOException("Injected crash"); };
        Assert.Throws<IOException>(() => store.Transact(0, "ritual", CharacterRules.UnlockSandboxOath));
        var recovered = new CharacterStore(directory);
        Assert.True(recovered.State.ElsewhereOwned ? recovered.State.Seals.All(s => s == 0) : recovered.State.Seals.All(s => s == 120));
        if (recovered.State.ElsewhereOwned) Assert.False(recovered.Transact(recovered.State.Revision, "ritual", CharacterRules.UnlockSandboxOath));
    }
    [Fact]
    public void CorruptAndTruncatedPrimaryRecoverWithThreeBackups()
    {
        var store = new CharacterStore(directory);
        for (var i = 0; i < 5; i++) store.Transact(store.State.Revision, $"gold-{i}", s => s with { Gold = s.Gold + 1 });
        Assert.All(new[] { 1, 2, 3 }, i => Assert.True(File.Exists(Path.Combine(directory, $"character.json.bak{i}"))));
        File.WriteAllText(Path.Combine(directory, "character.json"), "{truncated");
        var recovered = new CharacterStore(directory); Assert.Equal(504, recovered.State.Gold); Assert.Contains("Recovered", recovered.Notice);
        recovered.Transact(recovered.State.Revision, "repair", s => s with { Gold = s.Gold + 10 });
        Assert.Equal(514, new CharacterStore(directory).State.Gold);
    }
    [Fact]
    public void MigrationPreservesExactHugeXpAndFutureVersionCannotBeOverwritten()
    {
        var xp = Progression.TotalXp(BigInteger.Pow(10, 30));
        var old = new CharacterState { SchemaVersion = 1, TotalXp = xp, ValidatedLevel = 1 };
        var migrated = CharacterStore.Decode(CharacterStore.Encode(old)); Assert.Equal(xp, migrated.TotalXp); Assert.Equal(8, migrated.SchemaVersion);
        Assert.Equal(BigInteger.Pow(10, 30), migrated.ValidatedLevel);
        Directory.CreateDirectory(directory); var future = CharacterStore.Encode(migrated with { SchemaVersion = 999 });
        File.WriteAllText(Path.Combine(directory, "character.json"), future);
        Assert.Throws<FutureSaveException>(() => new CharacterStore(directory)); Assert.Equal(future, File.ReadAllText(Path.Combine(directory, "character.json")));
    }
    [Fact]
    public void DuplicateAndStaleCommandsCannotMutateTheWallet()
    {
        var store = new CharacterStore(directory); Assert.True(store.Transact(0, "one", s => s with { Gold = s.Gold - 100 }));
        Assert.False(store.Transact(0, "one", s => s with { Gold = 0 }));
        Assert.Throws<InvalidOperationException>(() => store.Transact(0, "different", s => s with { Gold = 0 }));
        Assert.Equal(400, new CharacterStore(directory).State.Gold);
    }
    [Theory]
    [InlineData(SaveStage.TemporaryWritten)] [InlineData(SaveStage.BeforeReplace)] [InlineData(SaveStage.Replaced)]
    public void InterruptedRoomRewardCannotSplitXpItemAndCheckpoint(SaveStage stage)
    {
        var store = new CharacterStore(directory, () => JourneyRules.Begin(new CharacterState(), Guid.NewGuid()));
        var before = store.State;
        store.Fault = s => { if (s == stage) throw new IOException("Injected crash"); };
        Assert.Throws<IOException>(() => store.Transact(0, "room0", s => JourneyRules.Clear(s, s.RunId, 0)));
        var after = new CharacterStore(directory);
        if (after.State.CheckpointRoom == 1)
        {
            Assert.Equal(before.TotalXp + 1200, after.State.TotalXp); Assert.Equal(before.Inventory.Length + 1, after.State.Inventory.Length);
            Assert.Equal(before.Gold + 100, after.State.Gold); Assert.False(after.Transact(after.State.Revision, "room0", s => JourneyRules.Clear(s, s.RunId, 0)));
        }
        else Assert.Equal(before.TotalXp, after.State.TotalXp);
    }
    [Fact]
    public void LevelThousandProgressAndAllocationSurviveRealDiskReload()
    {
        var store = new CharacterStore(directory); var xp = Progression.TotalXp(1001);
        store.Transact(0, "large-xp", s => FoundationRules.AddXp(s, xp - s.TotalXp));
        var loaded = new CharacterStore(directory).State;
        Assert.Equal(xp, loaded.TotalXp); Assert.Equal(1001, loaded.ValidatedLevel); Assert.Equal(941, loaded.Might + loaded.Resolve);
        Assert.Equal(20, FoundationRules.SkillBudget(loaded)); Assert.Equal(30, FoundationRules.TalentBudget(loaded));
    }
}
