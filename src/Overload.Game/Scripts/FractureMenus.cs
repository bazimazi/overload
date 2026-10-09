using Godot;
using Overload.Domain;
using BigInteger = System.Numerics.BigInteger;

namespace Overload.Game;
public partial class ArenaHud
{
    private BigInteger selectedTier = 1;
    public void Fractures()
    {
        var s = arena.Character!.State;
        if(s.Chain is { Closed:false } chain) { selectedTier=chain.Tier;selectedRegion=chain.Regions[chain.CompletedLegs]; }
        ClearMenu("FRACTURE BOARD / SAVED EXPEDITIONS", "Beyond the court", $"Cleared {CounterText.Short(s.HighestClearedTier)} · Frontier {CounterText.Short(s.HighestUnlockedTier)} · Attunement {CounterText.Short(s.AttunementGrade)}");
        PanelTabs("Atlas");
        options.AddChild(new WorldAtlasCanvas {SelectedRegion=selectedRegion,SelectRegion=r=>{selectedRegion=r;Fractures();},TextPercent=arena.Audio.TextPercent,SizeFlagsHorizontal=SizeFlags.ExpandFill});
        if (!s.FractureUnlocked)
        { Text("Complete your campaign to open Fractures and shared attunement.", 18, ink); AddButton("Back", Title, true); return; }
        if (!s.ChapterOffers.IsEmpty)
        {
            Text($"Chapter {CounterText.Short(s.PendingChapter)}: choose a saved route. Each uses the same XP and wallet budget, with a different targeted item. Branching maps contain guarded objectives and optional caches.", 17, ink);
            foreach (var id in s.ChapterOffers)
            {
                var route = EndlessRules.Routes.Single(r => r.Id == id);
                foreach(var ruleId in WorldLaws.ForRoute(id,s.PendingChapter)){var law=WorldLaws.Rules.Single(r=>r.Id==ruleId);Text(law.Name+": "+law.Description,14,gold);}
                AddButton($"{route.Region} route — target {route.Target}", () => { arena.UpdateCharacter(c => EndlessRules.ChooseRoute(c, id)); Fractures(); }, id == s.ChapterOffers[0]);
            }
        }
        else if (s.Fracture is { Completed: false } run)
        {
            Text($"Tier {CounterText.Short(run.Tier)} · {(run.Map is {} map?$"{map.Required.Count(map.Claims.Contains)}/{map.Required.Count} objectives":$"group {run.NextGroup+1}/7")}\nBanked XP stays saved on death or abandonment. Completion awards {CounterText.Short(run.GoldBudget)} gold and {CounterText.Short(run.AlloyBudget)} Alloy.", 17, ink);
            AddButton("Resume saved expedition", () => arena.StartFracture(run.Tier), true);
            AddButton("Abandon expedition (keep banked XP)", () => { arena.UpdateCharacter(EndlessRules.Abandon); Fractures(); });
        }
        else
        {
            selectedTier = BigInteger.Clamp(selectedTier, 1, s.HighestUnlockedTier);
            Text($"Selected tier {CounterText.Short(selectedTier)} · suggested level {CounterText.Short(Progression.ReferenceLevel(selectedTier))}", 20, ink);
            var route = EndlessRules.Routes.Single(r => r.Id == s.SelectedRoute);
            Text($"{route.Region}: targeted {route.Target} · maximum XP {CounterText.Short(EndlessRules.ExpeditionXp(selectedTier))}\nCompletion: {CounterText.Short(selectedTier * 200)} gold / {CounterText.Short(selectedTier * 100)} Alloy\nPhysical branches and objective gates. Main objectives bank 80% of XP; optional caches bank 20%. Full bags forfeit the targeted drop. Saved older runs retain their room checkpoints.", 16, muted);
            AddButton("Begin selected tier", () => arena.StartRegional(selectedTier,selectedRegion,anomaly?ActivityFamily.Hunt:selectedActivity,anomaly?selectedMutation:null,anomaly), true);
            ActivityOptions();
            AddButton("Select frontier", () => { selectedTier = s.HighestUnlockedTier; Fractures(); });
            foreach(var known in s.KnownRoutes.Order())
            {
                var knownRoute=EndlessRules.Routes.Single(r=>r.Id==known);
                AddButton($"Use known {knownRoute.Region} route ({knownRoute.Target})",()=> { arena.UpdateCharacter(c=>EndlessRules.SelectKnownRoute(c,known));Fractures(); });
            }
            var tiers=new HBoxContainer();options.AddChild(tiers);
            foreach (var offset in new[] { -1000, -10, -1, 1, 10, 1000 })
                PanelButton(tiers,$"{(offset > 0 ? "+" : "")}{offset}", () => { selectedTier = BigInteger.Clamp(selectedTier + offset, 1, s.HighestUnlockedTier); Fractures(); });
            ExactInput("Enter exact unlocked tier", selectedTier.ToString(), value =>
            {
                if (value < 1 || value > s.HighestUnlockedTier) { Text("That tier is not unlocked.", 16, gold); return; }
                selectedTier = value; Fractures();
            });
        }
        AddButton("Shared attunement forge", Attunement); Issue(); AddButton("Back to Hearth", Title);
    }
    private void ExactInput(string label, string value, Action<BigInteger> apply)
    {
        Text(label, 16, muted);
        var input = new LineEdit { Text = value, CustomMinimumSize = new Vector2(500, 40), PlaceholderText = "Nonnegative whole number" };
        options.AddChild(input);
        void Confirm()
        {
            try { apply(Quantity.Parse(input.Text).Value); }
            catch (FormatException) { Text("Use decimal digits with no signs, spaces or leading zeroes.", 14, gold); }
        }
        input.TextSubmitted += _ => Confirm(); AddButton("Use exact value", Confirm);
    }
    private void Attunement()
    {
        var s = arena.Character!.State;
        ClearMenu("ONE SHARED GRADE / SIX EQUIPMENT SLOTS", "Attunement forge", $"Grade {CounterText.Short(s.AttunementGrade)} · earned limit {CounterText.Short(BigInteger.Max(1, s.HighestClearedTier))}");
        Text($"{CounterText.Short(s.Gold)} gold · {CounterText.Short(s.Alloy)} Alloy\nShared grade scales final damage, Life, Armor and Resistance once. It does not scale speed, Focus or extra affixes.", 17, ink);
        foreach (var target in new[] { s.AttunementGrade + 1, EndlessRules.AffordableGrade(s), s.HighestClearedTier }.Distinct().Where(t => t > s.AttunementGrade && t <= s.HighestClearedTier))
        {
            var quote = EndlessRules.Quote(s.AttunementGrade, target);
            AddButton($"Grade {CounterText.Short(target)} — {CounterText.Short(quote.Gold)} gold / {CounterText.Short(quote.Alloy)} Alloy", () => ConfirmForge(target), target == s.AttunementGrade + 1);
        }
        ExactInput("Preview an exact target grade", (s.AttunementGrade + 1).ToString(), ConfirmForge);
        Issue(); AddButton("Back", Fractures, true); goBack = Fractures;
    }
    private void ConfirmForge(BigInteger target)
    {
        var s = arena.Character!.State;
        if (target <= s.AttunementGrade || target > s.HighestClearedTier) { Text("Choose a higher grade up to your highest cleared tier.", 16, gold); return; }
        var q = EndlessRules.Quote(s.AttunementGrade, target);
        ClearMenu("FIXED COST / ONE SAVED TRANSACTION", "Confirm attunement", $"Grade {CounterText.Short(q.FromGrade)} to {CounterText.Short(q.ToGrade)}");
        Text($"Gold: {q.Gold}\nAlloy: {q.Alloy}\nAll equipped and future gear shares this grade.", 17, ink);
        AddButton("Buy upgrade", () => { arena.UpdateCharacter(c => EndlessRules.Forge(c, target)); Attunement(); }, true);
        AddButton("Cancel", Attunement); goBack = Attunement;
    }
    public void FractureCleared()
    {
        var run = arena.Character!.State.Fracture!;
        ClearMenu("EXPEDITION CHECKPOINT SAVED", run.Completed ? "Fracture cleared" : "Encounter cleared",
            $"Tier {CounterText.Short(run.Tier)} · {run.NextGroup}/7 groups complete · {CounterText.Short(EndlessRules.GroupXp(run, run.NextGroup - 1))} XP banked");
        if (run.Completed) Text(arena.Character.State.Journal.Last(), 17, ink);
        AddButton(run.Completed ? "Return to Hearth" : "Continue expedition", arena.ContinueFracture, true);
        if (!run.Completed) AddButton("Hearth — keep checkpoint", arena.ReturnToTitle);
        goBack = arena.ReturnToTitle;
    }
    public void FractureSaveFailed()
    {
        ClearMenu("FILES PRESERVED", "Reward save incomplete", arena.SaveProblem);
        AddButton("Retry saving reward", arena.RetryFractureSave, true); AddButton("Hearth (replay unsaved group)", arena.ReturnToTitle); goBack = arena.ReturnToTitle;
    }
}
