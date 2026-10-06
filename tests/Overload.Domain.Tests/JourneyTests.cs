using Overload.Domain;
using Overload.Content;
using Xunit;

namespace Overload.Domain.Tests;
public class JourneyTests
{
    [Fact]
    public void EightRoomRewardsResumeWithoutDuplicatePaymentOrMilestones()
    {
        var s = JourneyRules.Begin(new CharacterState(), Guid.NewGuid()); var start = s;
        for (var i = 0; i < 8; i++)
        {
            var next = JourneyRules.Clear(s, s.RunId, i);
            Assert.Equal(i + 1, next.CheckpointRoom); Assert.Equal(s.Inventory.Length + 1, next.Inventory.Length);
            Assert.Equal(next, JourneyRules.Clear(next, next.RunId, i));
            s = CharacterStore.Decode(CharacterStore.Encode(next));
        }
        Assert.Equal(4, s.SkillMilestones.Count); Assert.True(s.TotalXp > start.TotalXp);
        Assert.Throws<InvalidOperationException>(() => JourneyRules.Clear(s, Guid.NewGuid(), 7));
        s = JourneyRules.Begin(s, Guid.NewGuid());
        s = JourneyRules.Clear(JourneyRules.Clear(s, s.RunId, 0), s.RunId, 1);
        Assert.Equal(4, s.SkillMilestones.Count);
    }
    [Fact]
    public void RewardCannotSkipCheckpointAndFullBagHasExplicitConversion()
    {
        var s = JourneyRules.Begin(new CharacterState(), Guid.NewGuid());
        Assert.Throws<InvalidOperationException>(() => JourneyRules.Clear(s, s.RunId, 1));
        Assert.Throws<InvalidOperationException>(() => JourneyRules.Begin(s, Guid.NewGuid()));
        s = s with { Inventory = [.. Enumerable.Range(0, 36).Select(i => EquipmentRules.StarterItems[i % 6] with { Id = Guid.NewGuid() })],
            Inscriptions = ["field-notes", "salvagers-mark"] };
        var result = JourneyRules.Clear(s, s.RunId, 0);
        Assert.Equal(36, result.Inventory.Length); Assert.Equal(s.Gold + 135, result.Gold); Assert.Equal(s.Alloy + 14, result.Alloy);
        Assert.Contains("bag full", result.Journal.Last());
    }
}
