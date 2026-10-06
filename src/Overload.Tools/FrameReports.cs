using System.Numerics;
using Overload.Domain;
namespace Overload.Tools;

public static class FrameReports
{
    public static void Export(BalanceProfile profile,string output)
    {
        Directory.CreateDirectory(output);
        var rows=new List<string>{"frame,build,encounter,oath,won,ticks,life_subunits,reservation_peak_permille"};
        var ratios=new List<double>();var wins=0;var total=0;
        foreach(var frame in Enum.GetValues<FrameId>())for(var build=0;build<3;build++)
        {
            var s=FrameRules.BuildFixture(frame,build);CharacterRules.Validate(s);
            var enemies=Enumerable.Range(0,4).Select(i=>RegionalContent.Enemy((Region)i,i+2)).Concat(Enumerable.Range(0,8).Select(i=>RegionalContent.Boss((Region)(i/2),i%2)));
            foreach(var definition in enemies)
            {
                var normal=Run(profile,s,definition,false);var covenant=Run(profile,s,definition,true);
                foreach(var result in new[]{normal,covenant})
                { rows.Add($"{frame},{build+1},{definition.Name},{result.Covenant},{result.Won},{result.Ticks},{result.Life},{result.Peak}");total++;if(result.Won)wins++; }
                if(!normal.Won)throw new InvalidDataException($"Build model failed: {frame} {build+1} {definition.Name}");
                if(covenant.Won)ratios.Add(100.0*(normal.Ticks-covenant.Ticks)/normal.Ticks);
            }
        }
        File.WriteAllLines(Path.Combine(output,"frame-builds.csv"),rows);
        ratios.Sort();var median=ratios[ratios.Count/2];
        File.WriteAllText(Path.Combine(output,"MODEL.md"),$"# A01/A03 combat model\n\n{wins}/{total} modeled encounters completed. All nine base-contract builds completed all four regional ordinary samples and eight bosses. Red Covenant median time improvement among paired wins: {median:0.00}%.\n\nThis uses the runtime action authority, cooldowns, Focus, reservations, healing, mitigation and stagger at 60 Hz. Each independent encounter resets checkpoint supplies. Legal active skills are tried in slot order, then the free basic; Flask is prioritized below 65% Life. All accepted direct hits connect with one stationary target; delayed wells, three-pulse looms and commanded echo bolts deliver their total authored budget with secondary provenance. Utility skills grant their barrier. Critical rolls and Overload memories are absent. The movement assumption avoids two of every three enemy attacks; the third resolves normally. This fixed avoidance assumption is explicit, not measured player performance or a geometric AI.\n\nCSV values are exact runtime subunits. A 180-second limit detects degenerate loops. These are reproducible feasibility checks, not proof of enjoyable builds, human boss wins or the release balance gate. Live engine checks separately exercise every skill's geometry. Human comparisons of all nine builds remain open.\n");
        Console.WriteLine($"FRAME_MODEL_OK {wins}/{total}; covenant median improvement {median:0.00}%");
    }
    private sealed record Result(bool Covenant,bool Won,int Ticks,BigInteger Life,int Peak);
    private static Result Run(BalanceProfile basis,CharacterState s,EnemyDefinition definition,bool covenant)
    {
        var profile=FoundationRules.Build(basis,s);var p=new PlayerCombat(profile,character:s);p.TryChangeOaths(covenant?["oath.red-covenant"]:[]);p.SetCombatActive(true);
        var e=new EnemyCombat(definition);var pending=new List<(long Tick,BigInteger Damage,int Stagger)>();var nextHostile=definition.Windup;var attacks=0;
        for(var tick=0;tick<10800;tick++)
        {
            p.AdvanceClock();
            if(!(p.Life<=p.MaximumLife*65/100&&p.TryStart(SkillId.Flask,Vector2.UnitX)))
            {
                foreach(var id in s.EquippedSkills.Append(FrameRules.Basic(s.Frame)))if(p.TryStart(id,Vector2.UnitX))break;
            }
            if(p.Action is { Emitted:false } action&&action.Phase(p.Tick)==ActionPhase.Active)
            {
                action.Emitted=true;var skill=action.Definition;var damage=p.AttackBudget(skill);
                if(skill.Id is SkillId.Bulwark or SkillId.Veil or SkillId.RecallThread)p.GrantBarrier(skill.BarrierPercent,180);
                else if(skill.Family==ActionFamily.Assault)
                {
                    var pulses=skill.EffectPulses;var delay=skill.EffectDelay;
                    for(var n=0;n<pulses;n++)pending.Add((p.Tick+delay+n*skill.EffectInterval,damage*(n+1)/pulses-damage*n/pulses,skill.Stagger));
                }
            }
            foreach(var hit in pending.Where(h=>h.Tick<=p.Tick).ToArray()){e.ReceiveHit(hit.Damage,hit.Stagger,p.Tick);pending.Remove(hit);}
            if(e.Dead)return new(covenant,true,tick+1,p.Life,p.PeakReservation);
            if(p.Tick>=nextHostile)
            {
                if(p.Tick>=e.StunnedUntil&&++attacks%3==0)p.ReceiveHit(e.Damage,false);
                nextHostile=tick+definition.Windup+definition.Recovery;
            }
            if(p.Dead)return new(covenant,false,tick+1,0,p.PeakReservation);
        }
        return new(covenant,false,10800,p.Life,p.PeakReservation);
    }
}
