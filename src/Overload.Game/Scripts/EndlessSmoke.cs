using Godot;
using Overload.Content;
using Overload.Domain;
using System.Text.Json;
using BigInteger = System.Numerics.BigInteger;

namespace Overload.Game;
public sealed class EndlessSmoke(Arena arena)
{
    private readonly ulong startup=Time.GetTicksMsec();
    private readonly List<string> checks=[];
    private int frame;
    private int tier=9;
    private int group;
    private bool failed;
    private BigInteger startingXp;
    private BigInteger enemyLife;
    private ActorBody? target;
    private Guid runId;
    public void Tick()
    {
        if(failed || DisplayServer.GetName()!="headless" && Time.GetTicksMsec()-startup<1000) return;
        frame++;
        var age=(frame-1)%140;
        if(age==0)
        {
            if(group==0)
            {
                arena.ReturnToTitle();
                var profile=EndlessFixtures.Reference(tier);
                Check(arena.UpdateCharacter(s=>profile with { CharacterId=s.CharacterId,Revision=s.Revision }),$"tier {tier}: isolated reference character prepared");
                startingXp=arena.Character!.State.TotalXp; arena.StartFracture(tier); runId=arena.Character.State.Fracture!.Id;
            }
            Check(arena.FractureActive && arena.EncounterTier==tier && arena.FractureGroup==group,$"tier {tier} group {group+1}: resumes authored checkpoint");
        }
        if(age==2)
        {
            Check(arena.Enemies.Count<=5 && arena.Enemies.All(e=>e.Enemy!.Definition.Windup>=24
                && WorldQueries.Connected(arena.Player.Position,e.Position,arena.Player.Radius,arena.CollisionWalls)), $"tier {tier} group {group+1}: bounded encounter has reachable spawn lanes");
            if(group==0)
            {
                // Keep one real collider; exercise an actual accepted strike against a tier-scaled victim.
                target=arena.Enemies[0];
                foreach(var e in arena.Enemies.Skip(1)) { e.CollisionLayer=0; e.CollisionMask=0; e.Enemy!.ReceiveHit(e.Enemy.MaximumLife,0,arena.PlayerState.Tick); }
                target.Position=arena.Player.Position+new Vector2(35,0); target.Enemy!.ReceiveHit(0,target.Enemy.Definition.StaggerThreshold,arena.PlayerState.Tick);
                enemyLife=target.Enemy.Life;
                Check(enemyLife==Progression.TierScaled(CombatMath.Points(target.Enemy.Definition.Life),tier),$"tier {tier}: enemy Life uses chosen tier");
                arena.PlayerState.TryStart(SkillId.Cleave,System.Numerics.Vector2.UnitX);
            }
        }
        if(age==12 && group==0)
        {
            Check(target!.Enemy!.Life<enemyLife && arena.Effects.HitsDealt>0,$"tier {tier}: real melee geometry delivers scaled damage");
            var before=arena.PlayerState.Life; var enemy=target.Enemy;
            arena.Effects.Launch(arena.Player.Position,Vector2.Right,120,5,100,enemy.Damage,false,1,0,100000+tier,arena.PlayerState.Tick+60);
            enemyLife=before;
        }
        if(age==13 && group==0)
        {
            Check(arena.PlayerState.Life<enemyLife,$"tier {tier}: hostile scaled projectile passes through defense authority");
            arena.PlayerState.ReceiveHit(arena.PlayerState.MaximumLife*100,false); arena.RetryCurrentRoom();
            Check(arena.Character!.State.Fracture!.Id==runId && arena.Character.State.Fracture.NextGroup==0 && arena.PlayerState.Life==arena.PlayerState.MaximumLife,$"tier {tier}: retry restores supplies without replacing reward identity");
        }
        if(age==20)
        {
            foreach(var e in arena.Enemies) e.Enemy!.ReceiveHit(e.Enemy.MaximumLife,0,arena.PlayerState.Tick);
        }
        if(age==125)
        {
            var s=arena.Character!.State; var r=s.Fracture!;
            Check(!arena.Playing && r.NextGroup==group+1 && r.Claims.Count==group+1,$"tier {tier} group {group+1}: atomic reward and checkpoint saved");
            if(group==2)
            {
                arena.ReturnToTitle(); arena.ReloadCharacterForCheck(); arena.StartFracture(tier);
                Check(arena.Character.State.Fracture!.Id==runId && arena.FractureGroup==3,$"tier {tier}: disk reload resumes same expedition");
            }
            else if(group<6) arena.ContinueFracture();
            else
            {
                Check(s.TotalXp-startingXp==r.XpBudget && s.HighestUnlockedTier==tier+1,$"tier {tier}: exact XP budget banked and next tier unlocked");
                arena.ContinueFracture();
                Check(arena.UpdateCharacter(c=>EndlessRules.Forge(c,tier)) && arena.Character.State.AttunementGrade==tier,$"tier {tier}: earned attunement upgrade reaches combat profile");
                if(tier==10)
                {
                    var offers=arena.Character.State.ChapterOffers; arena.ReloadCharacterForCheck();
                    Check(offers.Length==3 && offers.SequenceEqual(arena.Character.State.ChapterOffers),"tier 10: saved chapter offers survive real disk reload");
                    Check(arena.UpdateCharacter(c=>EndlessRules.ChooseRoute(c,offers[0])) && arena.Character.State.Chapter==2,"tier 10: selecting a saved route enters chapter two");
                }
            }
        }
        if(age==139)
        {
            group++;
            if(group==7) { tier++;group=0; }
            if(tier==12)
            {
                var path=ProjectSettings.GlobalizePath("user://tests/endless-smoke.json");
                System.IO.File.WriteAllText(path,JsonSerializer.Serialize(new { success=true,checks }));
                GD.Print("OVERLOAD_ENDLESS_SMOKE_OK "+checks.Count); failed=true; arena.QuitGame();
            }
        }
    }
    private void Check(bool success,string message)
    {
        if(!success) { failed=true; GD.PushError("ENDLESS FAILED: "+message);arena.QuitGame(1);return; }
        checks.Add(message);GD.Print("ENDLESS PASS: "+message);
    }
}
