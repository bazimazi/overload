using System.Numerics;
using Overload.Domain;

namespace Overload.Tools;

public static class QualityReports
{
    private sealed record Outcome(bool Won,int Ticks,BigInteger Damage,BigInteger Life,int FocusSpent,int Flasks,int Signatures,int Missed);
    public static void Export(BalanceProfile basis,string output)
    {
        Directory.CreateDirectory(output);
        var rows=new List<string>{"scope,frame,checkpoint,level,tier,encounter,movement_assumption,won,ticks,damage_subunits,life_subunits,focus_spent,flasks_used,signature_actions,base_with_memory"};
        var campaignFailures=new List<string>();var encounters=0;
        foreach(var frame in Enum.GetValues<FrameId>())
        {
            var s=JourneyRules.Begin(FrameRules.Create(frame),Guid.NewGuid());
            for(var room=0;room<16;room++)
            {
                s=Allocate(s);var region=(Region)(room/4);var local=room%4;
                var definitions=local is 1 or 3 ? new[]{RegionalContent.Boss(region,local==1?0:1)} : Enumerable.Range(0,3).Select(i=>RegionalContent.Enemy(region,local==0?i:i+3)).ToArray();
                foreach(var moving in new[]{false,true})
                {
                    var result=Run(basis,s,definitions,1,moving,true);encounters++;
                    rows.Add(Row("campaign",s,room+1,1,local is 1 or 3?definitions[0].Name!:"regional pack",moving,result));
                    if(moving&&!result.Won)campaignFailures.Add($"{frame} checkpoint {room+1}");
                }
                s=JourneyRules.Clear(s,s.RunId,room);
                // Only deterministic earned ordinary drops; no quality upgrades or synthetic item stats.
                foreach(var slot in Enum.GetValues<GearSlot>())
                    s=EquipmentRules.Equip(s,s.Inventory.Where(i=>i.Slot==slot).MaxBy(i=>i.BaseValue)!.Id);
            }
            foreach(var tier in new BigInteger[]{1,10,11,1000,BigInteger.Pow(10,30)})
            {
                var reference=EndlessFixtures.Reference(tier);
                s=FrameRules.BuildFixture(frame,0) with { TotalXp=reference.TotalXp,ValidatedLevel=reference.ValidatedLevel,Might=reference.Might,Resolve=reference.Resolve,
                    AttunementGrade=reference.AttunementGrade,HighestClearedTier=reference.HighestClearedTier,HighestUnlockedTier=tier,Chapter=reference.Chapter };
                CharacterRules.Validate(s);
                var samples=new (string Name,EnemyDefinition[] Enemies,bool Hazard)[]{
                    ("stationary",[basis.Enemies[0] with { Speed=0 }],false),
                    ("moving",[RegionalContent.Enemy(Region.Glass,0)],false),
                    ("three enemy pack",Enumerable.Range(0,3).Select(_=>basis.Enemies[0]).ToArray(),false),
                    ("mixed ranged pack",[basis.Enemies[0],basis.Enemies[1],basis.Enemies[2]],false),
                    ("staggerable elite",[basis.Enemies[2]],false),
                    ("Bellkeeper",[basis.Enemies[3]],false),
                    ("hazard arena",[basis.Enemies[0],basis.Enemies[1]],true)};
                foreach(var sample in samples)foreach(var moving in new[]{false,true})
                { rows.Add(Row("endgame",s,0,tier,sample.Name,moving,Run(basis,s,sample.Enemies,tier,moving,sample.Hazard)));encounters++; }
            }
        }
        File.WriteAllLines(Path.Combine(output,"ordinary-gear.csv"),rows);
        File.WriteAllText(Path.Combine(output,"BALANCE.md"),$"# Q01 ordinary-gear model\n\n{encounters} comparisons: each Frame's sixteen campaign checkpoints at earned XP and equipped deterministic drops; seven fixed endgame samples at tiers 1, 10, 11, 1000 and 10^30. Campaign moving-assumption failures: {(campaignFailures.Count==0?"none":string.Join(", ",campaignFailures))}.\n\nNo quality upgrades or rare/legendary affixes are required by these fixtures. Campaign points are allocated through ordinary rules and rewards are carried between checkpoints; combat supplies reset per checkpoint. Endgame uses matched reference level/grade with the same starter gear and legal finite allocations.\n\nThe exact runtime authority supplies action timing, damage, defense, cooldowns, Focus, flasks, stagger and memory selection. All emitted geometry is assumed to hit one living target; pulse budgets are split and delayed. Moving means 2 of 3 hostile attacks are assumed avoided and walking credit is modeled; stationary takes every attack. Hazard damage uses the same damage authority every 180 ticks. Critical rolls are absent. Enemies do not simulate navigation, fan geometry, marks or human reaction. Base actions while holding a memory are counted as an opportunity diagnostic, not a measured player mistake.\n\nCSV includes damage, time alive, remaining Life, Focus expenditure, flask use and selected signatures. Failures are reported rather than hidden or tuned away by weakening encounters. These are screening assumptions, not human campaign balance or completion-time evidence. Pair these with rendered engine runs and independent player sessions before closing Q01.\n");
        Console.WriteLine($"QUALITY_BALANCE_OK comparisons={encounters} campaignMovingFailures={campaignFailures.Count}");
    }
    private static string Row(string scope,CharacterState s,int checkpoint,BigInteger tier,string encounter,bool moving,Outcome r)
        =>$"{scope},{s.Frame},{checkpoint},{s.ValidatedLevel},{tier},{encounter},{(moving?"avoid2of3":"all connect")},{r.Won},{r.Ticks},{r.Damage},{r.Life},{r.FocusSpent},{r.Flasks},{r.Signatures},{r.Missed}";
    private static CharacterState Allocate(CharacterState s)
    {
        var ids=s.EquippedSkills.Prepend(FrameRules.Basic(s.Frame)).ToArray();
        while(FoundationRules.SkillSpent(s)<FoundationRules.SkillBudget(s)&&ids.Any(id=>s.SkillRanks.GetValueOrDefault(id)<5))
            foreach(var id in ids)if(s.SkillRanks.GetValueOrDefault(id)<5&&FoundationRules.SkillSpent(s)<FoundationRules.SkillBudget(s))s=FoundationRules.Rank(s,id);
        foreach(var id in FrameRules.Talents(s.Frame))
            if(s.Talents.Count<FoundationRules.TalentBudget(s)&&!s.Talents.Contains(id)&&(!id.EndsWith(".4",StringComparison.Ordinal)||s.Talents.Count(t=>t.EndsWith(".4",StringComparison.Ordinal))<2))s=FoundationRules.Talent(s,id);
        CharacterRules.Validate(s);return s;
    }
    private static Outcome Run(BalanceProfile basis,CharacterState s,EnemyDefinition[] definitions,BigInteger tier,bool moving,bool hazard)
    {
        var p=new PlayerCombat(FoundationRules.Build(basis,s),character:s,tier:tier);p.TryChangeBindings(s.Bindings);p.SetCombatActive(true);
        var enemies=definitions.Select(d=>new EnemyCombat(d,tier)).ToArray();var hostile=definitions.Select(d=>(long)d.Windup).ToArray();var attacks=new int[enemies.Length];
        var pending=new List<(long Due,int Target,BigInteger Damage,int Stagger,SourceKind Source)>();BigInteger dealt=0;var focus=0;var signatures=0;var missed=0;
        for(var tick=0;tick<10800;tick++)
        {
            p.AdvanceClock();p.Memories.ObservePosition(moving?new(tick%240,0):Vector2.Zero);
            if(moving)p.Memories.ObserveLocomotion(new(2,0),LocomotionKind.Walk);
            var target=Array.FindIndex(enemies,e=>!e.Dead);if(target<0)return new(true,tick,dealt,p.Life,focus,3-p.FlaskCharges,signatures,missed);
            var before=p.Focus;
            if(!(p.Life<p.MaximumLife*65/100&&p.TryStart(SkillId.Flask,Vector2.UnitX)))
                foreach(var id in s.EquippedSkills.Append(FrameRules.Basic(s.Frame)))if(p.TryStart(id,Vector2.UnitX))break;
            focus+=Math.Max(0,before-p.Focus)/1000;
            if(p.Action is { Emitted:false } action&&action.Phase(p.Tick)==ActionPhase.Active)
            {
                action.Emitted=true;var skill=action.Definition;
                if(action.Implementation!=ActionImplementation.Base)signatures++;
                else if(p.Memories.Momentum is not null||p.Memories.Echo is not null||p.Memories.Stillness is not null)missed++;
                if(skill.BarrierPercent>0)p.GrantBarrier(skill.BarrierPercent,180);
                else if(skill.Family==ActionFamily.Assault)
                {
                    var budget=PatternExecution.Damage(p.AttackBudget(skill),action.Implementation);
                    for(var n=0;n<skill.EffectPulses;n++)pending.Add((p.Tick+skill.EffectDelay+n*skill.EffectInterval,target,budget.First*(n+1)/skill.EffectPulses-budget.First*n/skill.EffectPulses,skill.Stagger,n==0?action.Source:SourceKind.SecondaryEffect));
                    if(budget.Repeat>0)pending.Add((p.Tick+PatternExecution.AfterstrikeDelay,target,budget.Repeat,skill.Stagger,SourceKind.SecondaryEffect));
                }
            }
            foreach(var hit in pending.Where(h=>h.Due<=p.Tick).ToArray())
            {
                var enemy=enemies[hit.Target];var beforeLife=enemy.Life;var broke=enemy.ReceiveHit(hit.Damage,hit.Stagger,p.Tick);dealt+=beforeLife-enemy.Life;
                p.Memories.ObserveBaseAssaultHit(Vector2.Zero,Vector2.UnitX,hit.Target,broke,hit.Source);pending.Remove(hit);
            }
            for(var i=0;i<enemies.Length;i++)if(!enemies[i].Dead&&p.Tick>=hostile[i])
            {
                if(p.Tick>=enemies[i].StunnedUntil&&(!moving||++attacks[i]%3==0))p.ReceiveHit(enemies[i].Damage,false);
                hostile[i]=p.Tick+definitions[i].Windup+definitions[i].Recovery;
            }
            if(hazard&&tick%180==179&&(!moving||tick%540==539))p.ReceiveHit(enemies[0].Damage,false);
            if(p.Dead)return new(false,tick+1,dealt,0,focus,3-p.FlaskCharges,signatures,missed);
        }
        return new(false,10800,dealt,p.Life,focus,3-p.FlaskCharges,signatures,missed);
    }
}
