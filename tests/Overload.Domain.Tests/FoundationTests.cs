using System.Numerics;
using Overload.Content;
using Overload.Domain;
using Xunit;
namespace Overload.Domain.Tests;
public class FoundationTests
{
    [Fact]
    public void HugeXpAwardsConserveLevelsResonanceAndFiniteFoundationBudgets()
    {
        var s = new CharacterState(); var level = BigInteger.Pow(10, 30);
        s = FoundationRules.AddXp(s, Progression.TotalXp(level) - s.TotalXp);
        Assert.Equal(level, s.ValidatedLevel); Assert.Equal(level - 60, s.Might + s.Resolve);
        Assert.Equal(20, FoundationRules.SkillBudget(s)); Assert.Equal(30, FoundationRules.TalentBudget(s));
        s = FoundationRules.Respec(s); Assert.Equal(level - 60, FoundationRules.UnspentResonance(s));
        s = FoundationRules.Allocate(s, true, level - 60); Assert.Equal(0, FoundationRules.UnspentResonance(s));
        Assert.Throws<InvalidOperationException>(() => FoundationRules.Allocate(s, false, 1)); CharacterRules.Validate(s);
    }
    [Fact]
    public void RankTechniqueAndRespecNeverDuplicatePoints()
    {
        var s = new CharacterState(); Assert.Throws<InvalidOperationException>(() => FoundationRules.ChooseTechnique(s, SkillId.Cleave, Technique.Wide));
        for (var i = 0; i < 3; i++) s = FoundationRules.Rank(s, SkillId.Cleave);
        s = FoundationRules.ChooseTechnique(s, SkillId.Cleave, Technique.Wide); Assert.Equal(4, FoundationRules.SkillSpent(s));
        s = FoundationRules.ChooseTechnique(s, SkillId.Cleave, Technique.Reach); Assert.Equal(4, FoundationRules.SkillSpent(s));
        s = FoundationRules.Respec(s); Assert.Equal(0, FoundationRules.SkillSpent(s)); Assert.Equal(8, FoundationRules.SkillBudget(s));
        CharacterRules.Validate(s);
    }
    [Fact]
    public void TalentPrerequisitesTradeoffLimitAndCodexSlotsAreEnforced()
    {
        var s = FoundationRules.AddXp(new(), Progression.TotalXp(100));
        Assert.Throws<InvalidOperationException>(() => FoundationRules.Talent(s, "force.4"));
        foreach (var id in FoundationRules.TalentIds.Take(11)) s = FoundationRules.Talent(s, id);
        Assert.Throws<InvalidOperationException>(() => FoundationRules.Talent(s, "flow.4"));
        s = FoundationRules.Inscription(s, "field-notes"); s = FoundationRules.Inscription(s, "quiet-rest");
        Assert.Throws<InvalidOperationException>(() => FoundationRules.Inscription(s, "long-sip"));
        s = FoundationRules.Inscription(s, "quiet-rest"); s = FoundationRules.Inscription(s, "long-sip"); CharacterRules.Validate(s);
    }
    [Fact]
    public void EquipmentAndResonanceApplyExactlyOnceToRootBudgets()
    {
        var basis = ProfileLoader.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "arena.json")));
        var s = FoundationRules.AddXp(new(), Progression.TotalXp(1000)); var build = FoundationRules.Build(basis, s);
        var p = new PlayerCombat(build, character: s); var skill = build.Skills.Single(k => k.Id == SkillId.Cleave);
        Assert.Equal(CombatMath.Points(skill.Damage) * (50 + s.Might) / 50, p.AttackBudget(skill));
        Assert.Equal(CombatMath.Points(build.Hero.Life) * (50 + s.Resolve) / 50, p.MaximumLife);
        var split = PatternExecution.Damage(p.AttackBudget(skill), ActionImplementation.Afterstrike);
        Assert.Equal(p.AttackBudget(skill) * 120 / 100, split.First + split.Repeat);
    }
}
