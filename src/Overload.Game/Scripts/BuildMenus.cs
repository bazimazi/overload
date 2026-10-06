using Godot;
using System.Collections.Immutable;
using Overload.Domain;

namespace Overload.Game;

public partial class ArenaHud
{
    private string?[] bindingDraft = new string?[3];
    public void Bindings()
    {
        if (arena.Playing) return;
        bindingDraft = [null, null, null];
        var bindings = arena.PlayerState.Behavior.Bindings.OrderBy(b => b.Priority).ToArray();
        for (var i = 0; i < bindings.Length; i++) bindingDraft[i] = bindings[i].Pattern.Id;
        DrawBindings();
    }
    private void DrawBindings(string message = "Equal-size signatures use this priority. Two-memory patterns win first.")
    {
        ClearMenu("HEARTH / BUILD", "Bindings", message);
        for (var i = 0; i < EndgameRules.BindingSlots(arena.Character!.State); i++)
        {
            var row = i;
            var id = bindingDraft[row];
            var pattern = arena.Balance.Overload.Patterns.FirstOrDefault(p => p.Id == id);
            AddButton($"{i + 1}. {pattern?.ImplementationId.Split('.')[1] ?? "Empty"} — change", () =>
            {
                var choices = new string?[] { null }.Concat(arena.Balance.Overload.Patterns
                    .Where(p=>EndgameRules.PatternAvailable(arena.Character!.State,p.Id)&&(p.RequiredMemories.Length<2||EndgameRules.BindingSlots(arena.Character.State)==3)).Select(p => (string?)p.Id).Where(p => p == id || !bindingDraft.Contains(p))).ToArray();
                bindingDraft[row] = choices[(Array.IndexOf(choices, id) + 1) % choices.Length]; DrawBindings();
            }, i == 0);
            if (pattern is not null) Text($"{string.Join(" + ", pattern.RequiredMemories)} · {pattern.StrainCost} Strain"
                + (arena.PlayerState.ElsewhereActive && pattern.Family == ActionFamily.Traverse && pattern.RequiredCapabilities.HasFlag(ActionCapabilities.PathTraversal) ? " · SUSPENDED by Elsewhere" : ""), 13, muted);
        }
        Text("Discover Focused through Hollow mastery, Shatter through Crown mastery, and Cascade through the Trial of Contradiction.",14,gold);
        AddButton("Apply bindings", () =>
        {
            var selected = bindingDraft.Select((id, priority) => new { id, priority }).Where(b => b.id is not null)
                .Select(b => new BindingDefinition(b.id!, b.priority)).ToImmutableArray();
            if (arena.UpdateCharacter(s => s with { Bindings = selected })) Title(); else DrawBindings(arena.SaveProblem);
        });
        AddButton("Cancel", Title);
    }
    public void Oaths()
    {
        if(arena.Playing||arena.Character is null)return;
        var state=arena.Character.State;
        ClearMenu("HEARTH / EARNED CONTRACT","Elsewhere","Replace evade with an anchor. Placement grants no evasion.");
        Text("Mark your feet, wait 0.25s, return within 4s and 8m. Crossing is suspended; Shelter can wrap the anchor.",15,muted);
        AddButton("Practice Elsewhere (no ownership or rewards)",arena.StartOathPractice,true);
        Text("Trial: separate level-100 power, 20 Might/Resolve, grade 10, standardized Rare gear. Your legal build choices stay; Override is disabled. No XP, loot or frontier reward. Failure costs time only.",15,gold);
        if(!state.ElsewhereSelected&&!state.RedCovenantSelected)AddButton("Enter Trial of Contradiction",arena.StartTrial);
        else Text("Disable Elsewhere before entering the personal trial.",15,gold);
        if(!state.ElsewhereOwned)
        {
            foreach(var reason in EndgameRules.Qualification(state))Text(reason,14,muted);
            if(state.Mode==ProfileMode.Standard)AddButton("Commit earned ritual - 120 of each Seal",()=>{arena.UpdateCharacter(EndgameRules.Ritual);Oaths();});
            else AddButton("Sandbox ritual - spend synthetic 120 x 4",()=>{arena.UpdateCharacter(CharacterRules.UnlockSandboxOath);Oaths();});
        }
        else AddButton(state.ElsewhereSelected?"Disable Elsewhere":"Equip Elsewhere",()=>{arena.UpdateCharacter(s=>CovenantRules.Select(s,s.ElsewhereSelected?null:"oath.elsewhere"));Oaths();});
        Text("Red Covenant: reserve 0.4% maximum Life per base Focus point for 4s, capped at 40%. Expiry restores capacity without healing. Focus regeneration becomes inactive.",15,gold);
        if(!state.ElsewhereSelected&&!state.RedCovenantSelected)AddButton("Practice the Red Covenant mastery challenge",arena.StartCovenantChallenge);
        else Text("Disable your active oath to enter a normalized mastery challenge.",14,gold);
        Text($"Mastery: {(state.CovenantMastery?"complete":"open")}. Earn a first oath, then pay 30 Seals from each region.",14,muted);
        if(state.RedCovenantOwned)AddButton(state.RedCovenantSelected?"Disable Red Covenant":"Equip Red Covenant",()=>{arena.UpdateCharacter(s=>CovenantRules.Select(s,s.RedCovenantSelected?null:"oath.red-covenant"));Oaths();});
        else AddButton("Earn Red Covenant - 30 of each Seal",()=>{arena.UpdateCharacter(CovenantRules.Unlock);Oaths();});
        Issue();AddButton("Back to Hearth",Title);goBack=Title;
    }
    private void SaveRecovery()
    {
        ClearMenu("CHARACTER FILES PRESERVED", "Save recovery", "Loading stopped to protect your existing character.");
        Text(arena.SaveProblem, 14, gold);
        AddButton("Retry loading", () => { arena.OpenCharacter(); Title(); }, true);
        AddButton("Create a separate character", () => { arena.OpenCharacter(true); Title(); });
        AddButton("Quit", () => arena.QuitGame());
    }
}
