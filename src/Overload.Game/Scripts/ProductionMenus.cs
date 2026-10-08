using Godot;
using Overload.Domain;

namespace Overload.Game;
public partial class ArenaHud
{
    private Region selectedRegion;
    private ActivityFamily selectedActivity;
    private bool anomaly;
    private string selectedMutation="mutation.anchor";
    private void ActivityOptions()
    {
        var s=arena.Character!.State;
        if(s.Chain is { Closed:false } c)
        {
            selectedRegion=c.Regions[c.CompletedLegs];selectedTier=c.Tier;
            Text($"Chain {c.CompletedLegs+1}/3 - {selectedRegion}. Previous legs are banked; only this leg's bonus is at risk.",16,gold);
            AddButton("Extract chain now (keep banked rewards)",()=>{arena.UpdateCharacter(EndgameRules.Extract);Fractures();});
        }
        else
        {
            foreach(var region in Enum.GetValues<Region>()) AddButton($"Region: {region}{(selectedRegion==region?" / selected":"")}",()=>{selectedRegion=region;Fractures();});
            AddButton("Start three-leg regional chain",()=>{arena.UpdateCharacter(s=>EndgameRules.StartChain(s,selectedTier,selectedRegion));Fractures();});
        }
        foreach(var family in Enum.GetValues<ActivityFamily>())AddButton($"{family}{(selectedActivity==family?" / selected":"")}",()=>{selectedActivity=family;Fractures();});
        Text("Hunt: marked elite and regional boss. Breach: restore two defended conduits, then defeat the boss. Vault: open guardian seals and claim the boss keystone. Optional supply caches hold 20% of XP; all families share the tier budget.",15,muted);
        var rules=WorldLaws.ForRoute(s.SelectedRoute,EndlessRules.ChapterAt(selectedTier));
        foreach(var id in rules){var rule=WorldLaws.Rules.Single(r=>r.Id==id);Text(rule.Name+": "+rule.Description,15,gold);}
        AddButton(anomaly?"Anomaly Hunt: Bellkeeper / targeted route loot":"Anomaly Hunt: choose a disclosed boss mutation",()=>{anomaly=!anomaly;Fractures();});
        if(anomaly)foreach(var mutation in WorldLaws.Mutations)AddButton($"{mutation.Split('.')[1]}{(selectedMutation==mutation?" / selected":"")}",()=>{selectedMutation=mutation;Fractures();});
        if(anomaly)Text(WorldLaws.MutationDescription(selectedMutation),15,gold);
        AddButton("Hazard assistance: "+(arena.HazardAssisted?"50% damage":"standard"),()=>{arena.SetHazardAssisted(!arena.HazardAssisted);Fractures();});
        AddButton("Challenge the Remembering Sovereign (cosmetic / Codex)",()=>arena.StartSovereign(selectedTier));
        AddButton("Regional journal and build records",Records);
    }
    public void Records()
    {
        var s=arena.Character!.State;ClearMenu("LOCAL RECORDS / LAST 64 RUNS","Regional journal",$"Highest cleared {CounterText.Short(s.HighestClearedTier)} - {s.Proofs.Count}/12 breadth proofs");
        foreach(var r in Enum.GetValues<Region>())Text($"{r}: {CounterText.Short(s.Seals[(int)r])} Seals - mastery {(s.Masteries.Contains(r)?"complete":"open")} - {string.Join(", ",Enum.GetValues<ActivityFamily>().Select(a=>$"{a}: {(s.Proofs.Contains(EndgameRules.Proof(r,a))?"yes":"no")}"))}",15,ink);
        Text("Mastery: Ash evade an actual hostile hit; Glass break cover; Hollow earn Stillness with a base hit; Crown stagger the boss with a base Assault. Complete the expedition to bank mastery.",15,gold);
        if(s.SovereignRewards.Contains("echo.anchor"))Text("Remembering Sovereign defeated. Anchor halo variant unlocked; Codex: The sovereign retreats to its old mark, but walking away defeats its memory.",16,gold);
        foreach(var best in s.RunRecords.Where(r=>r.Tier==selectedTier).GroupBy(r=>new {r.Assisted,r.Encounter}))Text($"Best at selected T{CounterText.Short(selectedTier)} / {best.Key.Encounter} / {(best.Key.Assisted?"assisted":"standard")}: {best.Min(r=>r.ElapsedTicks)/60.0:0.0}s",15,gold);
        foreach(var record in s.RunRecords.Reverse().Take(12))Text($"T{CounterText.Short(record.Tier)} {record.Region} {record.Activity} / {record.Encounter} / {record.ElapsedTicks/60.0:0.0}s / {(record.Assisted?"assisted":"standard")}\n{record.Build}",13,muted);
        AddButton("Back",Fractures,true);goBack=Fractures;
    }
    public void TrialChoiceRetry()
    {
        ClearMenu("TRIAL / NO MATERIAL COST","Choose two approaches","Land hits with two different Assault skills or two signatures in the first encounter. Your Frame basic plus an active damaging Assault supplies a base-skill solution.");
        AddButton("Retry this trial encounter",arena.RetrySpecial,true);AddButton("Return to Hearth",arena.ReturnToTitle);goBack=arena.ReturnToTitle;
    }
    public void TrialSaveFailed()
    {ClearMenu("TRIAL COMPLETE / SAVE INCOMPLETE","Bank personal proof",arena.SaveProblem);AddButton("Retry proof save",arena.RetryTrialReward,true);AddButton("Hearth",arena.ReturnToTitle);}
    public void SovereignSaveFailed()
    {ClearMenu("ECHO DEFEATED / SAVE INCOMPLETE","Bank cosmetic record",arena.SaveProblem);AddButton("Retry record save",arena.RetrySovereignReward,true);AddButton("Hearth",arena.ReturnToTitle);}
    public void DeathSaveFailed()
    {ClearMenu("CHAIN FAILURE / SAVE INCOMPLETE","Bank failure checkpoint",arena.SaveProblem);AddButton("Retry saving checkpoint",arena.RetryDeathSave,true);AddButton("Hearth",arena.ReturnToTitle);}
}
