using System.Collections.Immutable;
using System.Numerics;

namespace Overload.Domain;

public enum Region { Ash, Glass, Hollow, Crown }
public enum ActivityFamily { Hunt, Breach, Vault }
public sealed record ExpeditionChain(Guid Id, BigInteger Tier, ImmutableArray<Region> Regions, int CompletedLegs = 0, bool Closed = false);
public sealed record BuildRecord(Guid RunId, BigInteger Sequence, BigInteger Tier, Region Region, ActivityFamily Activity,
    long ElapsedTicks, bool Assisted, string Build, string Encounter = "Expedition");

public static class EndgameRules
{
    public const string Version = "fracture.v2";
    public static string Proof(Region region, ActivityFamily activity) => $"proof.{region}.{activity}";
    public static readonly ImmutableHashSet<string> AllProofs = [.. Enum.GetValues<Region>().SelectMany(r => Enum.GetValues<ActivityFamily>().Select(a => Proof(r,a)))];
    public static bool PatternAvailable(CharacterState s,string id)=>id switch
    { "pattern.assault.focused"=>s.Masteries.Contains(Region.Hollow),"pattern.assault.shatter"=>s.Masteries.Contains(Region.Crown),"pattern.assault.cascade"=>s.TrialCompleted,_=>true };
    public static int BindingSlots(CharacterState s)=>s.Mode==ProfileMode.SliceSandbox?3:s.ValidatedLevel>=30?3:s.ValidatedLevel>=12?2:1;
    public static CharacterState Standard() => new() { Mode = ProfileMode.Standard, TotalXp = 0, ValidatedLevel = 1, Gold = 0, Alloy = 0, Seals = [0,0,0,0], Bindings = [new("pattern.assault.pursuit",0)] };
    public static ImmutableArray<string> Qualification(CharacterState s)
    {
        var result = ImmutableArray.CreateBuilder<string>();
        if (s.Mode != ProfileMode.Standard) result.Add("Use a Standard character; sandbox resources cannot qualify.");
        if (s.ValidatedLevel < 100) result.Add("Reach level 100 or higher.");
        if (!s.FractureUnlocked) result.Add("Complete the court campaign.");
        foreach(var r in Enum.GetValues<Region>())
        {
            if (!s.Masteries.Contains(r)) result.Add($"Complete {r}'s mastery quest.");
            foreach(var a in Enum.GetValues<ActivityFamily>()) if(!s.Proofs.Contains(Proof(r,a))) result.Add($"Clear {r} {a} at tier 10 or higher.");
            if(s.Seals[(int)r]<120) result.Add($"Collect 120 {r} Seals ({s.Seals[(int)r]} held).");
        }
        if(!s.TrialCompleted) result.Add("Complete the Trial of Contradiction without an Override.");
        return result.ToImmutable();
    }
    public static CharacterState Ritual(CharacterState s)
    {
        if(s.ElsewhereOwned) throw new InvalidOperationException("Elsewhere is already owned");
        var missing=Qualification(s); if(!missing.IsEmpty) throw new InvalidOperationException(string.Join("\n",missing));
        return s with { ElsewhereOwned=true, Seals=[..s.Seals.Select(v=>v-120)] };
    }
    public static CharacterState TrialActor(CharacterState s)
    {
        // A separate aggregate is used only to build the temporary actor. Persistent state is never replaced.
        return s with { TotalXp=Progression.TotalXp(100), ValidatedLevel=100, Might=20, Resolve=20, AttunementGrade=10,
            Inventory=[..EquipmentRules.StarterItems.Select(i=>i with { Band=3, Quality=2 })], Equipment=EquipmentRules.StarterItems.ToImmutableDictionary(i=>i.Slot,i=>i.Id), ElsewhereSelected=false, RedCovenantSelected=false };
    }
    public static CharacterState TrialVictory(CharacterState s, Guid attempt)
    {
        if(attempt==Guid.Empty || (s.ElsewhereSelected||s.RedCovenantSelected)) throw new InvalidOperationException("Trial requires base contracts");
        return s with { TrialCompleted=true };
    }
    public static CharacterState Begin(CharacterState s, BigInteger tier, Guid id, Region region, ActivityFamily activity,
        string? mutation = null, bool anomaly = false, bool assisted = false, bool regionalContent = false)
    {
        if(!Enum.IsDefined(region) || !Enum.IsDefined(activity) || mutation is not null && !WorldLaws.Mutations.Contains(mutation)) throw new InvalidOperationException("Unknown regional activity or mutation");
        if(s.Chain is { Closed:false } chain && (region!=chain.Regions[chain.CompletedLegs] || tier!=chain.Tier)) throw new InvalidOperationException("Continue the saved chain region or extract first");
        var next=EndlessRules.Begin(s,tier,id);
        if(mutation is not null&&!anomaly)throw new InvalidOperationException("Explicit mutation selection requires an Anomaly Hunt");
        if(anomaly&&activity!=ActivityFamily.Hunt)throw new InvalidOperationException("Anomaly Hunt uses Hunt objectives");
        var version=regionalContent?RegionalContent.ExpeditionVersion:Version;
        var seed=EndlessRules.Seed(tier,id,$"{s.SelectedRoute}|{region}|{activity}|{mutation}",version);
        var layout=regionalContent?RegionalContent.Generate(seed,region):ExpeditionGenerator.Generate(seed);
        var rules=WorldLaws.ForRoute(s.SelectedRoute,EndlessRules.ChapterAt(tier));
        var boss=mutation ?? WorldLaws.Mutations[(int)(seed%4)];
        return next with { Fracture=next.Fracture! with { ContentVersion=version,Seed=seed,Rooms=layout.Rooms,Layout=layout,
            Region=region,Activity=activity,WorldRules=rules,Mutation=boss,Anomaly=anomaly,Assisted=assisted,
            ChainId=s.Chain is { Closed:false } c ? c.Id:null,ChainLeg=s.Chain is { Closed:false } c2 ? c2.CompletedLegs+1:0 } };
    }
    public static CharacterState Completed(CharacterState s, Expedition run)
    {
        var proofs=s.Proofs; var seals=s.Seals; var bonuses=s.BreadthBonuses;
        if(s.FractureUnlocked && run.Tier>=10)
        {
            proofs=proofs.Add(Proof(run.Region,run.Activity));
            var amount=6;
            if(!bonuses.Contains(run.Region) && Enum.GetValues<ActivityFamily>().All(a=>proofs.Contains(Proof(run.Region,a)))) { amount+=12;bonuses=bonuses.Add(run.Region); }
            seals=seals.SetItem((int)run.Region,seals[(int)run.Region]+amount);
        }
        var chain=s.Chain;
        BigInteger bonusGold=0,bonusAlloy=0;
        if(run.ChainId is not null)
        {
            if(chain is null || chain.Closed || chain.Id!=run.ChainId || chain.CompletedLegs+1!=run.ChainLeg) throw new InvalidOperationException("Stale chain leg");
            var percent=!run.ChainBonusEligible?0:run.ChainLeg==2?10:run.ChainLeg==3?20:0;
            bonusGold=run.GoldBudget*percent/100;bonusAlloy=run.AlloyBudget*percent/100;
            s=s with { Gold=s.Gold+bonusGold,Alloy=s.Alloy+bonusAlloy };
            chain=chain with { CompletedLegs=run.ChainLeg,Closed=run.ChainLeg==3 };
        }
        var build=Build(s);
        var sealGain=seals[(int)run.Region]-s.Seals[(int)run.Region];
        return s with { Proofs=proofs,Seals=seals,BreadthBonuses=bonuses,Masteries=run.MasteryEarned?s.Masteries.Add(run.Region):s.Masteries,Chain=chain,
            Journal=[..s.Journal.TakeLast(31),$"{run.Region} {run.Activity}: {run.XpBudget} XP, {run.GoldBudget+bonusGold} gold, {run.AlloyBudget+bonusAlloy} Alloy, {sealGain} Seals banked. {(run.MasteryEarned?"Regional mastery complete.":"Mastery objective remains open.")}"],
            RunRecords=[..s.RunRecords.TakeLast(63),new(run.Id,run.Sequence,run.Tier,run.Region,run.Activity,run.ElapsedTicks,run.Assisted,build,run.Anomaly?"Anomaly "+run.Mutation:"Expedition")] };
    }
    public static string Build(CharacterState s)
    {
        var gear=s.Equipment.OrderBy(p=>p.Key).Select(p=>s.Inventory.Single(i=>i.Id==p.Value));
        return $"{s.Frame}; G{CounterText.Short(s.AttunementGrade)}; M{CounterText.Short(s.Might)}/R{CounterText.Short(s.Resolve)}; {string.Join(',',s.EquippedSkills)}; {string.Join(',',s.Bindings.Select(b=>b.PatternId))}; oath={(s.ElsewhereSelected?"Elsewhere":s.RedCovenantSelected?"Red Covenant":"none")}; ranks={string.Join(',',s.SkillRanks.OrderBy(p=>p.Key).Select(p=>$"{p.Key}:{p.Value}"))}; techniques={string.Join(',',s.Techniques.OrderBy(p=>p.Key).Select(p=>$"{p.Key}:{p.Value}"))}; talents={string.Join(',',s.Talents.Order())}; Codex={string.Join(',',s.Inscriptions.Order())}; gear={string.Join(';',gear.Select(i=>$"{i.Slot}/B{i.Band}/Q{i.Quality}/{i.BaseKind}:{i.BaseValue}/{string.Join(',',i.Affixes.Select(a=>$"{a.Kind}:{a.Value}"))}"))}";
    }
    public static CharacterState StartChain(CharacterState s,BigInteger tier,Region first)
    {
        if(s.Fracture is { Completed:false } || s.Chain is { Closed:false } || tier<1 || tier>s.HighestUnlockedTier) throw new InvalidOperationException("Finish or abandon the active expedition/chain first");
        return s with { Chain=new(Guid.NewGuid(),tier,[first,(Region)(((int)first+1)%4),(Region)(((int)first+2)%4)]),Fracture=null };
    }
    public static CharacterState Extract(CharacterState s) => s with { Chain=s.Chain is { } c?c with { Closed=true }:null,Fracture=s.Fracture is { Completed:false,ChainId:not null }?null:s.Fracture };
    public static CharacterState FailLeg(CharacterState s) => s.Fracture is { Completed:false,ChainId:not null } r ? s with { Fracture=r with { ChainBonusEligible=false } } : s;
    public static void Validate(CharacterState s)
    {
        if(s.Proofs is null || s.Proofs.Any(p=>!AllProofs.Contains(p)) || s.Masteries is null || s.Masteries.Any(r=>!Enum.IsDefined(r))
            || s.BreadthBonuses is null || s.BreadthBonuses.Any(r=>!Enum.IsDefined(r) || !Enum.GetValues<ActivityFamily>().All(a=>s.Proofs.Contains(Proof(r,a))))
            || s.RunRecords.IsDefault || s.RunRecords.Length>64 || s.RunRecords.Any(r=>r.RunId==Guid.Empty || r.Sequence<1 || r.Tier<1 || r.ElapsedTicks<0 || !Enum.IsDefined(r.Region) || !Enum.IsDefined(r.Activity) || r.Build.Length>4096)
            || s.RunRecords.Select(r=>r.RunId).Distinct().Count()!=s.RunRecords.Length || s.SovereignRewards is null || s.SovereignRewards.Any(id=>id!="echo.anchor") ) throw new InvalidDataException("Invalid production progress or records");
        if(s.Chain is { } c && (c.Id==Guid.Empty || c.Tier<1 || c.Tier>s.HighestUnlockedTier || c.Regions.IsDefault || c.Regions.Length!=3 || c.Regions.Distinct().Count()!=3 || c.Regions.Any(r=>!Enum.IsDefined(r)) || c.CompletedLegs is <0 or >3 || c.CompletedLegs==3 && !c.Closed)) throw new InvalidDataException("Invalid saved chain");
    }
}
