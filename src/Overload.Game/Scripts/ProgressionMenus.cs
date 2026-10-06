using System.Numerics;
using Overload.Domain;

namespace Overload.Game;
public partial class ArenaHud
{
    private void CharacterMenu()
    {
        var s = arena.Character!.State;
        ClearMenu($"CHARACTER / {s.Mode}", $"{s.Frame} · Level {CounterText.Short(s.ValidatedLevel)}", $"XP {CounterText.Short(s.TotalXp)} · next level at {CounterText.Short(Progression.TotalXp(s.ValidatedLevel + 1))}");
        Text($"Skill points {FoundationRules.SkillBudget(s) - FoundationRules.SkillSpent(s)} · Talent points {FoundationRules.TalentBudget(s) - s.Talents.Count}\nMight {CounterText.Short(s.Might)} · Resolve {CounterText.Short(s.Resolve)} · Unspent Resonance {CounterText.Short(FoundationRules.UnspentResonance(s))}", 15, muted);
        if(arena.PlayerState.RedCovenantActive)Text("Focus regeneration from gear and talents is INACTIVE while Red Covenant is equipped.",15,gold);
        AddButton("Skill ranks and techniques", SkillsMenu, true); AddButton($"Twelve {s.Frame} talents", TalentsMenu); AddButton("Codex inscriptions", CodexMenu);
        AddButton("Choose three active skills", LoadoutMenu);
        AddButton(s.AutoResonance ? $"Resonance: automatic {s.ResonancePolicy} → manual" : "Resonance: manual → automatic", () =>
        { arena.UpdateCharacter(c => FoundationRules.AddXp(c with { AutoResonance = !c.AutoResonance }, 0)); CharacterMenu(); });
        foreach(var policy in Enum.GetValues<ResonancePolicy>())
            AddButton($"Auto rule: {policy}{(policy==ResonancePolicy.Balanced ? " 1:1" : policy==ResonancePolicy.Offense ? " 3:1" : " 1:3")}",()=>
            { arena.UpdateCharacter(c=>FoundationRules.AddXp(c with { AutoResonance=true,ResonancePolicy=policy },0));CharacterMenu(); });
        foreach (var amount in new BigInteger[] { 1, 1000 })
        {
            AddButton($"Allocate {amount} Might", () => { arena.UpdateCharacter(c => FoundationRules.Allocate(c, true, amount)); CharacterMenu(); });
            AddButton($"Allocate {amount} Resolve", () => { arena.UpdateCharacter(c => FoundationRules.Allocate(c, false, amount)); CharacterMenu(); });
        }
        AddButton("Allocate an exact Resonance amount", ResonanceMenu);
        AddButton("Exact counters and combat values", ExactCounters);
        AddButton("Free respec — return skill, talent and Resonance points", () => { arena.UpdateCharacter(FoundationRules.Respec); CharacterMenu(); });
        Issue(); AddButton("Back to Hearth", Title);
    }
    private void ResonanceMenu()
    {
        ClearMenu("EXACT ALLOCATION", "Resonance", "Enter any available whole number. Assignment switches to manual allocation.");
        ExactInput("Might ranks", "1", amount=> { arena.UpdateCharacter(c=>FoundationRules.Allocate(c,true,amount));CharacterMenu(); });
        ExactInput("Resolve ranks", "1", amount=> { arena.UpdateCharacter(c=>FoundationRules.Allocate(c,false,amount));CharacterMenu(); });
        AddButton("Back",CharacterMenu,true);goBack=CharacterMenu;
    }
    private void ExactCounters()
    {
        var s=arena.Character!.State;
        ClearMenu("FULL DECIMAL VALUES / COPYABLE", "Character ledger", "Short HUD labels never change the values used for combat or saving.");
        options.AddChild(new Godot.TextEdit { Editable=false, CustomMinimumSize=new Godot.Vector2(620,280), Text=
            $"Level: {s.ValidatedLevel}\nTotal XP: {s.TotalXp}\nMight: {s.Might}\nResolve: {s.Resolve}\nUnspent: {FoundationRules.UnspentResonance(s)}\nAttunement: {s.AttunementGrade}\nHighest cleared: {s.HighestClearedTier}\nHighest unlocked: {s.HighestUnlockedTier}\nChapter: {s.Chapter}\nGold: {s.Gold}\nAlloy: {s.Alloy}\nMaximum Life (subunits): {arena.PlayerState.MaximumLife}\nBasic damage (subunits): {arena.PlayerState.AttackBudget(arena.Balance.Skills.Single(k=>k.Id==FrameRules.Basic(s.Frame)))}" });
        AddButton("Back",CharacterMenu,true);goBack=CharacterMenu;
    }
    private void SkillsMenu()
    {
        var s = arena.Character!.State;
        ClearMenu("SKILLS / PERSISTENT CHOICES", "Train the Frame", $"{FoundationRules.SkillBudget(s) - FoundationRules.SkillSpent(s)} skill points available; techniques require rank 3");
        foreach (var skill in arena.Balance.Skills.Where(k => k.Family == ActionFamily.Assault))
        {
            AddButton($"{skill.Id}: rank {s.SkillRanks.GetValueOrDefault(skill.Id)}/5 — spend 1 point", () => { arena.UpdateCharacter(c => FoundationRules.Rank(c, skill.Id)); SkillsMenu(); });
            Text($"Technique: {s.Techniques.GetValueOrDefault(skill.Id).ToString()} {(s.Techniques.ContainsKey(skill.Id) ? "" : "(not learned)")}", 13, muted);
            foreach (var technique in Enum.GetValues<Technique>()) AddButton($"{skill.Id}: {technique} technique (1 point; swapping is free)", () =>
            { arena.UpdateCharacter(c => FoundationRules.ChooseTechnique(c, skill.Id, technique)); SkillsMenu(); });
        }
        Issue(); AddButton("Back", CharacterMenu, true); goBack = CharacterMenu;
    }
    private void LoadoutMenu()
    {
        var s = arena.Character!.State;
        ClearMenu($"THREE ACTIVE SLOTS / {FrameRules.Basic(s.Frame)} ALWAYS AVAILABLE", "Frame loadout", "Select a slot to cycle through unequipped skills. Saved immediately.");
        for (var i = 0; i < 3; i++)
        {
            var slot = i; var id = s.EquippedSkills[i]; var skill = arena.Balance.Skills.Single(k => k.Id == id);
            AddButton($"{new[] { "Q / X", "E / Y", "R / B" }[i]}: {id} — change", () =>
            {
                var choices = arena.Balance.Skills.Where(k => k.Family == ActionFamily.Assault && k.Id != FrameRules.Basic(s.Frame)
                    && (k.Id == id || !s.EquippedSkills.Contains(k.Id))).Select(k => k.Id).ToArray();
                arena.UpdateCharacter(c => c with { EquippedSkills = c.EquippedSkills.SetItem(slot, choices[(Array.IndexOf(choices, id) + 1) % choices.Length]) }); LoadoutMenu();
            }, i == 0);
            Text(FrameRules.SkillDescription(id),13,muted);
            Text($"{skill.Damage} damage · {skill.FocusCost} Focus · {skill.Cooldown / 60f:0.#}s cooldown · {skill.Reach / 32f:0.#}m range", 14, muted);
        }
        Issue(); AddButton("Back", CharacterMenu); goBack = CharacterMenu;
    }
    private void TalentsMenu()
    {
        var s = arena.Character!.State;
        ClearMenu("THREE BRANCHES / FOLLOW EACH PATH", $"{s.Frame} talents", $"{FoundationRules.TalentBudget(s) - s.Talents.Count} points · At most two final tradeoffs");
        var descriptions = new[] { "+5% damage", "+5 stagger", "+4 reach", "+15% damage / -10% Life", "+5% Life", "+10 Armor", "+5 Resistance", "+20% Life / -10% damage", "+2 Focus per second", "Recovery -1 tick", "+5 maximum Focus", "+3 Focus per second / -5% Life" };
        for (var i = 0; i < FrameRules.Talents(s.Frame).Length; i++)
        {
            var id = FrameRules.Talents(s.Frame)[i];
            AddButton($"{(s.Talents.Contains(id) ? "Learned" : "Learn")} {id}: {descriptions[i]}", () => { arena.UpdateCharacter(c => FoundationRules.Talent(c, id)); TalentsMenu(); });
        }
        Issue(); AddButton("Back", CharacterMenu, true); goBack = CharacterMenu;
    }
    private void CodexMenu()
    {
        var s = arena.Character!.State;
        ClearMenu("CODEX / TWO EQUIPPED INSCRIPTIONS", "Recovered knowledge", "Four inscriptions are available in this accelerated slice.");
        var descriptions = new[] { "Room gold +10%", "Room Alloy +1", "Recover 1% Life/second outside combat", "Flask recovery +2 percentage points" };
        for (var i = 0; i < FoundationRules.InscriptionIds.Length; i++)
        {
            var id = FoundationRules.InscriptionIds[i];
            AddButton($"{(s.Inscriptions.Contains(id) ? "Unequip" : "Equip")} {id} — {descriptions[i]}", () => { arena.UpdateCharacter(c => FoundationRules.Inscription(c, id)); CodexMenu(); });
        }
        foreach (var entry in s.Journal.TakeLast(8)) Text(entry, 13, muted);
        Issue(); AddButton("Back", CharacterMenu, true); goBack = CharacterMenu;
    }
    private void Issue() { if (!string.IsNullOrEmpty(arena.SaveProblem)) Text(arena.SaveProblem, 14, gold); }
}
