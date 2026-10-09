using Godot;
using Overload.Domain;
using BigInteger=System.Numerics.BigInteger;

namespace Overload.Game;

public partial class CombatEffects
{
    private readonly HashSet<long> deathburstRoots=[];
    private readonly Queue<long> deathburstHistory=[];
    public int Deathbursts {get;private set;}
    public int SplitVolleys {get;private set;}
    private bool SplitVolley(StrikePayload payload)
    {
        if(payload.Recipe.Geometry!=AttackGeometry.Projectile||payload.Provenance.EffectId!=0
            ||payload.Provenance.Source==SourceKind.SecondaryEffect||arena.Character is null||!AdventureRules.HasPower(arena.Character.State,"split"))return false;
        HashSet<int> victims=[];
        SplitVolleys++;
        for(var side=-1;side<=1;side++)Launch(payload.Origin,payload.Aim.Rotated(side*.24f),payload.Skill.ProjectileSpeed,payload.Skill.ProjectileRadius,
            payload.Recipe.Reach,payload.Damage*65/100,true,Math.Min(64,payload.Recipe.MaxVictims*3),payload.Recipe.Stagger,payload.Provenance.RootActionId,
            provenance:payload.Provenance,skill:payload.Skill.Id,sharedVictims:victims);
        return true;
    }
    private void Deathburst(ActorBody target,BigInteger damage,EffectProvenance provenance,SkillId? skill)
    {
        if(provenance.Source is not (SourceKind.BasePlayerAction or SourceKind.OverloadedPlayerAction)||skill is null
            ||arena.Character is null||!AdventureRules.HasPower(arena.Character.State,"nova")||!deathburstRoots.Add(provenance.RootActionId))return;
        deathburstHistory.Enqueue(provenance.RootActionId);
        if(deathburstHistory.Count>128)deathburstRoots.Remove(deathburstHistory.Dequeue());
        // A distinct secondary identity prevents both memory generation and recursive death explosions.
        var child=new EffectProvenance(provenance.RootActionId,1001,provenance.EffectId,SourceKind.SecondaryEffect,1);
        var definition=arena.Balance.Skills.Single(s=>s.Id==skill) with {Windup=0,Active=1,Recovery=0};
        var payload=new StrikePayload(definition,new(AttackGeometry.Sector,78,360,0,64,0,0),target.Position,Vector2.Right,damage/2,child,false);
        if(delayed.TryEnqueue(arena.PlayerState.Tick+1,payload))
        {Deathbursts++;Burst(target.Position,new Color("eebd6f"),22,80);sweeps.Add(new(target.Position,Vector2.Right,78,360,true));}
    }
}
