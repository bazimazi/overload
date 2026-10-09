using System.Numerics;
using Overload.Domain;
using Godot;

namespace Overload.Game;
public partial class ArenaHud
{
    private void CharacterMenu()
    {
        var s = arena.Character!.State;
        ClearMenu($"CHARACTER / {s.Mode}", $"{s.Frame} · Level {CounterText.Short(s.ValidatedLevel)}", $"XP {CounterText.Short(s.TotalXp)} · next level at {CounterText.Short(Progression.TotalXp(s.ValidatedLevel + 1))}");
        PanelTabs("Character");
        var columns = new HBoxContainer(); columns.AddThemeConstantOverride("separation", 18); options.AddChild(columns);
        var body = Section(columns, "The Manyborn"); body.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        body.GetParent<Control>().SizeFlagsHorizontal=SizeFlags.ShrinkBegin;
        body.AddChild(new FramePortrait { Frame = s.Frame, CustomMinimumSize = new(240, 200) });
        StatLine(body, "Maximum Life", CounterText.Short(arena.PlayerState.MaximumLife / 1000));
        StatLine(body, "Might", CounterText.Short(s.Might)); StatLine(body, "Resolve", CounterText.Short(s.Resolve));
        StatLine(body, "Attunement", CounterText.Short(s.AttunementGrade));
        var build = Section(columns, "Shape your next life");
        ArtButton(build, 0, "Skills & techniques", $"{FoundationRules.SkillBudget(s) - FoundationRules.SkillSpent(s)} skill points available", SkillsMenu).GrabFocus();
        ArtButton(build, 1, "Talent paths", $"{FoundationRules.TalentBudget(s) - s.Talents.Count} talent points available", TalentsMenu);
        ArtButton(build, 15, "Codex inscriptions", $"{s.Inscriptions.Count}/2 inscriptions equipped", CodexMenu);
        ArtButton(build, 10, "Active loadout", "Choose your three active skills", LoadoutMenu);
        if(s.ValidatedLevel>=60)
        {
        Text($"Unspent Resonance: {CounterText.Short(FoundationRules.UnspentResonance(s))}", 15, gold);
        if(arena.PlayerState.RedCovenantActive)Text("Focus regeneration from gear and talents is INACTIVE while Red Covenant is equipped.",15,gold);
        AddButton(s.AutoResonance ? $"Resonance: automatic {s.ResonancePolicy} → manual" : "Resonance: manual → automatic", () =>
        { arena.UpdateCharacter(c => FoundationRules.AddXp(c with { AutoResonance = !c.AutoResonance }, 0)); CharacterMenu(); });
        foreach(var policy in Enum.GetValues<ResonancePolicy>())
            AddButton($"Auto rule: {policy}{(policy==ResonancePolicy.Balanced ? " 1:1" : policy==ResonancePolicy.Offense ? " 3:1" : " 1:3")}",()=>
            { arena.UpdateCharacter(c=>FoundationRules.AddXp(c with { AutoResonance=true,ResonancePolicy=policy },0));CharacterMenu(); });
        AddButton("Allocate an exact Resonance amount", ResonanceMenu);
        }
        AddButton("Exact counters and combat values", ExactCounters);
        AddButton("Free respec — return skill, talent and Resonance points", () => { arena.UpdateCharacter(FoundationRules.Respec); CharacterMenu(); });
        Issue(); AddButton(arena.Playing?"Return to adventure":"Back to Hearth", ReturnFromBuild);goBack=ReturnFromBuild;
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
        PanelTabs("Skills");
        var available=FoundationRules.SkillBudget(s)-FoundationRules.SkillSpent(s);
        AddButton("Change equipped skills · Q / E / R",LoadoutMenu,available==0);
        if(s.World?.Adventure is not null)Text("Basic hits: +8 Focus. Three basic hits: next damaging skill +50% (Surge).",14,gold);
        var grid=CardGrid();
        var focused=false;
        foreach (var skill in arena.Balance.Skills.Where(k => k.Family == ActionFamily.Assault))
        {
            var box=Section(grid,SkillName(skill.Id));
            var rank=s.SkillRanks.GetValueOrDefault(skill.Id);
            var row=new HBoxContainer();box.AddChild(row);row.AddChild(new RelicIcon {Index=RpgTheme.Icon(skill.Id),CustomMinimumSize=new(72,72)});
            var detail=new VBoxContainer {SizeFlagsHorizontal=SizeFlags.ExpandFill};row.AddChild(detail);
            BodyLabel(detail,string.Join(" ",Enumerable.Range(0,5).Select(i=>i<rank?"◆":"◇")),18,gold);
            BodyLabel(detail,$"{skill.FocusCost} Focus • {skill.Cooldown/60f:0.#}s cooldown",12,muted);
            BodyLabel(box,DescribeSkill(skill.Id),13,ink);
            var train=PanelButton(box,$"Train • Rank {rank}/5",()=>{arena.UpdateCharacter(c=>FoundationRules.Rank(c,skill.Id));SkillsMenu();},rank>=5||available==0);
            if(!focused&&!train.Disabled){FocusAfterLayout(train);focused=true;}
            var techniques=new HBoxContainer();box.AddChild(techniques);
            foreach(var technique in Enum.GetValues<Technique>())PanelButton(techniques,technique+(s.Techniques.TryGetValue(skill.Id,out var known)&&known==technique?" ◆":""),()=>{arena.UpdateCharacter(c=>FoundationRules.ChooseTechnique(c,skill.Id,technique));SkillsMenu();},rank<3||!s.Techniques.ContainsKey(skill.Id)&&FoundationRules.SkillSpent(s)>=FoundationRules.SkillBudget(s));
        }
        Issue(); AddButton("Back", CharacterMenu); goBack = CharacterMenu;
    }
    private void LoadoutMenu()
    {
        var s = arena.Character!.State;
        ClearMenu($"THREE ACTIVE SLOTS / {FrameRules.Basic(s.Frame)} ALWAYS AVAILABLE", "Frame loadout", "Select a socket, then choose its skill. Saved immediately.");
        PanelTabs("Skills");
        for (var i = 0; i < 3; i++)
        {
            var slot = i; var id = s.EquippedSkills[i]; var skill = arena.Balance.Skills.Single(k => k.Id == id);
            var button=ArtButton(options,RpgTheme.Icon(id),$"Socket {i+1} • {SkillName(id)}",DescribeSkill(id),()=>ChooseActiveSkill(slot),true);
            if(i==0)button.GrabFocus();
            Text($"{skill.Damage} damage · {skill.FocusCost} Focus · {skill.Cooldown / 60f:0.#}s cooldown · {skill.Reach / 32f:0.#}m range", 14, muted);
        }
        Issue(); AddButton("Back", CharacterMenu); goBack = CharacterMenu;
    }
    private void ChooseActiveSkill(int slot)
    {
        var s=arena.Character!.State;var equipped=s.EquippedSkills[slot];
        ClearMenu($"LOADOUT / SOCKET {slot+1}","Choose a skill","Your basic attack stays available. Each active skill occupies one socket.");
        var grid=CardGrid();Button? first=null;
        foreach(var skill in arena.Balance.Skills.Where(k=>k.Family==ActionFamily.Assault&&k.Id!=FrameRules.Basic(s.Frame)&&(k.Id==equipped||!s.EquippedSkills.Contains(k.Id))))
        {
            var id=skill.Id;
            var button=ArtButton(grid,RpgTheme.Icon(id),SkillName(id),DescribeSkill(id),()=>{arena.UpdateCharacter(c=>c with {EquippedSkills=c.EquippedSkills.SetItem(slot,id)});LoadoutMenu();},id==equipped);
            first??=button;
        }
        first?.GrabFocus();AddButton("Back to loadout",LoadoutMenu,first is null);goBack=LoadoutMenu;
    }
    private void TalentsMenu()
    {
        var s = arena.Character!.State;
        ClearMenu("THREE BRANCHES / FOLLOW EACH PATH", $"{s.Frame} talents", $"{FoundationRules.TalentBudget(s) - s.Talents.Count} points · At most two final tradeoffs");
        PanelTabs("Talents");
        options.AddChild(new TalentConstellation {State=s,TextPercent=arena.Audio.TextPercent,Learn=id=>{arena.UpdateCharacter(c=>FoundationRules.Talent(c,id));TalentsMenu();},SizeFlagsHorizontal=SizeFlags.ExpandFill});
        Issue(); AddButton("Back", CharacterMenu, true); goBack = CharacterMenu;
    }
    private void CodexMenu()
    {
        var s = arena.Character!.State;
        ClearMenu("CODEX / TWO EQUIPPED INSCRIPTIONS", "Recovered knowledge", "Four inscriptions are available in this accelerated slice.");
        var descriptions = new[] { "Room gold +10%", "Room Alloy +1", "Recover 1% Life/second outside combat", "Flask recovery +2 percentage points" };
        var grid=CardGrid();
        for (var i = 0; i < FoundationRules.InscriptionIds.Length; i++)
        {
            var id = FoundationRules.InscriptionIds[i];
            ArtButton(grid,15,SkillNameText(id.Replace('-',' ')),descriptions[i]+(s.Inscriptions.Contains(id)?" • Equipped":" • Equip inscription"),()=>{arena.UpdateCharacter(c=>FoundationRules.Inscription(c,id));CodexMenu();},s.Inscriptions.Contains(id));
        }
        foreach (var entry in s.Journal.TakeLast(8)) Text(entry, 13, muted);
        Issue(); AddButton("Back", CharacterMenu, true); goBack = CharacterMenu;
    }
    private void Issue() { if (!string.IsNullOrEmpty(arena.SaveProblem)) Text(arena.SaveProblem, 14, gold); }
}
