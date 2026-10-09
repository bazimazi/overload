using Godot;
using Overload.Domain;

namespace Overload.Game;

public partial class Arena
{
    public bool AdventureActive=>WorldActive&&!GraphFracture&&Character?.State.World?.Adventure is not null;
    public AdventureObjective? AdventureObjective=>AdventureActive?AdventureRules.Objective(Character!.State):null;
    public IEnumerable<WorldLoot> NearbyLoot=>AdventureActive?Character!.State.World!.Adventure!.GroundLoot.Where(d=>d.ZoneId==WorldZone.Id):[];
    private long portalEnds;
    private Vector2 portalOrigin;
    private int portalHits;
    private Guid? pendingLootPickup;
    public bool PortalChanneling=>portalEnds>0;
    public float PortalProgress=>PortalChanneling?Math.Clamp(1-(portalEnds-PlayerState.Tick)/180f,0,1):0;
    public string AdventureToastTitle {get;private set;}="";
    public string AdventureToastDetail {get;private set;}="";
    private long adventureToastUntil;
    public bool AdventureToastVisible=>PlayerState.Tick<adventureToastUntil;
    public bool HazardUnderfoot=>laws.WarningAreas.Any(r=>r.Grow(Player.Radius).HasPoint(Player.Position))||WarningElites.Any(e=>Player.Position.DistanceTo(e.EliteWarningPoint)<64+Player.Radius);
    public void AdventureToast(string title,string detail)
    {AdventureToastTitle=title;AdventureToastDetail=detail;adventureToastUntil=PlayerState.Tick+300;}
    private void RefreshAdventureBuild()
    {
        Balance=FoundationRules.Build(BaseBalance,Character!.State);
        PlayerState.Reconfigure(Balance,Character.State);
    }
    private EnemyDefinition AdventureEnemy(EnemyDefinition definition)
    {
        if(!AdventureActive)return definition;
        return Character!.State.World!.Adventure!.Difficulty switch
        {
            AdventureDifficulty.Story=>definition with {Life=Math.Max(1,definition.Life*80/100),Damage=Math.Max(1,definition.Damage*65/100),Windup=definition.Windup*125/100},
            AdventureDifficulty.Veteran=>definition with {Life=definition.Life*135/100,Damage=definition.Damage*125/100},
            _=>definition
        };
    }
    public bool TryPickUpNearby()
    {
        var loot=NearbyLoot.Where(d=>Player.Position.DistanceTo(V(d.Position))<=84).OrderBy(d=>Player.Position.DistanceSquaredTo(V(d.Position))).FirstOrDefault();
        if(loot is null)return false;
        return PickUpWorldLoot(loot);
    }
    private bool PickUpWorldLoot(WorldLoot loot)
    {
        if(WorldTransaction("loot:"+loot.Item.Id.ToString("N"),s=>AdventureRules.PickUp(s,loot.Item.Id,new(Player.Position.X,Player.Position.Y))))
        {
            Audio.Play("reward","UI");AdventureToast(AdventureRules.Rarity(loot.Item).ToUpperInvariant()+" · "+loot.Item.Name,"Collected · open inventory [I] to compare and equip");
            nextGuidanceTick=0;
        }
        return true;
    }
    private bool TryClickLoot()
    {
        foreach(var loot in NearbyLoot.Where(d=>WorldCellKnown(V(d.Position))))
        {
            var p=V(loot.Position);var width=Math.Max(50,ThemeDB.FallbackFont.GetStringSize(loot.Item.Name,fontSize:11).X);
            if(!new Rect2(p-new Vector2(width/2+6,58),new(width+12,82)).HasPoint(CursorWorldPosition))continue;
            Controls.SuppressPrimaryUntilRelease();
            if(Player.Position.DistanceTo(p)<=84&&WorldNavigation.Clear(new(Player.Position.X,Player.Position.Y),loot.Position.Vector,0))PickUpWorldLoot(loot);
            else if(RequestWorldWalk(p))pendingLootPickup=loot.Item.Id;
            return true;
        }
        return false;
    }
    public void AcceptFirstQuest()
    {
        if(!AdventureActive)return;
        if(WorldTransaction("quest:first-road:"+Character!.State.World!.CampaignId.ToString("N"),AdventureRules.Accept)){nextGuidanceTick=0;CloseWorldMenu();AdventureToast("QUEST STARTED","Take the west road to Cinderroad and clear the first patrol");}
    }
    public void BeginTownPortal()
    {
        if(!AdventureActive||Paused||PlayerState.Dead)return;
        if(WorldZone.Id=="hearth")
        {
            if(Character!.State.World!.Adventure!.ReturnPortal is null){WorldNotice("Explore a road first. Press P outside Hearth to open a return portal.");return;}
            if(WorldTransaction("portal:return:"+Guid.NewGuid().ToString("N"),AdventureRules.PortalBack))EnterWorldZone(false);
            return;
        }
        if(PortalChanneling){portalEnds=0;WorldNotice("Portal canceled.");return;}
        if(PlayerState.Action is not null){WorldNotice("Finish your action before opening a portal.");return;}
        if(!WorldNavigation.Clear(new(Player.Position.X,Player.Position.Y),new(Player.Position.X,Player.Position.Y),20))
        {WorldNotice("Move into open ground to open a portal.");return;}
        ResetPointerTravel();portalOrigin=Player.Position;portalHits=Effects.HitsTaken;portalEnds=PlayerState.Tick+180;
        WorldNotice("Opening portal · stand still for 3 seconds. Movement, attacks or damage interrupt it.");
    }
    private void AdvanceAdventure()
    {
        if(!AdventureActive){portalEnds=0;return;}
        if(pendingLootPickup is { } id)
        {
            var loot=NearbyLoot.FirstOrDefault(d=>d.Item.Id==id);
            if(loot is null)pendingLootPickup=null;
            else if(Player.Position.DistanceTo(V(loot.Position))<=80&&WorldNavigation.Clear(new(Player.Position.X,Player.Position.Y),loot.Position.Vector,0))
            {pendingLootPickup=null;ResetPointerTravel();PickUpWorldLoot(loot);}
        }
        if(!PortalChanneling)return;
        if(Player.Position.DistanceTo(portalOrigin)>2||Effects.HitsTaken!=portalHits||PlayerState.Action is not null||PlayerState.Dead)
        {portalEnds=0;WorldNotice("Portal interrupted. Find safe ground and press P again.");return;}
        if(PlayerState.Tick<portalEnds)return;
        portalEnds=0;
        if(WorldTransaction("portal:out:"+Guid.NewGuid().ToString("N"),s=>AdventureRules.PortalOut(s,new(Player.Position.X,Player.Position.Y))))
        {EnterWorldZone(true);AdventureToast("HEARTH · RESTED","Press P to return to your portal. Inventory [I], skills [K].");}
    }
    public void ChooseDifficulty(AdventureDifficulty difficulty)
    {
        if(!AdventureActive||WorldZone.Id!="hearth")return;
        if(UpdateCharacter(s=>AdventureRules.SetDifficulty(s,difficulty)))Hud.AdventureJournal();
    }
    public void StartFrontierContract()
    {
        if(WorldTransaction("contract:begin:"+Guid.NewGuid().ToString("N"),AdventureRules.BeginContract))
        {EnterWorldZone(true);AdventureToast("TRAILKEEPER CONTRACT","Clear the three central patrols, then activate the camp · J for details");}
        else Hud.AdventureJournal();
    }
    public void CollectContractReward()
    {
        var contract=Character!.State.World!.Adventure!.Contract;
        if(contract is null)return;
        if(WorldTransaction($"contract:claim:{Character.State.World.CampaignId:N}:{contract.Depth}",AdventureRules.ClaimContract))
        {Audio.Play("reward","UI");RefreshAdventureBuild();Hud.AdventureJournal();}
        else Hud.AdventureJournal();
    }
    public void AbandonFrontierContract()
    {if(UpdateCharacter(AdventureRules.AbandonContract))Hud.AdventureJournal();}
    private WorldTarget? GuidedAdventureTarget()
    {
        var step=AdventureObjective;if(step is null)return null;
        if(Character!.State.World!.Adventure!.Contract is { } contract&&WorldZone.Id==FrontierWorld.Id(contract.Depth))
        {
            var next=AdventureRules.ContractTargets(contract.Depth).FirstOrDefault(id=>!contract.Completed.Contains(id));
            var encounter=WorldZone.Encounters.FirstOrDefault(e=>e.Id==next);
            if(encounter is not null)return new(encounter.Id,"Contract patrol",V(encounter.Position),"encounter");
            var camp=WorldZone.Sites.FirstOrDefault(s=>s.Id==next);
            if(camp is not null)return new(camp.Id,"Contract camp",V(camp.Position),"waypoint");
            var home=WorldZone.Exits.Single(e=>e.Id=="home");return new("exit:home","Return to Mara · or press P",V(home.Position),"exit");
        }
        if(step.Id=="meet"&&WorldZone.Id=="hearth")
        {var mara=WorldZone.Sites.Single(s=>s.Id=="mara");return new(mara.Id,"Mara · your first quest",V(mara.Position),"npc");}
        if(step.Id=="road"&&WorldZone.Id=="ash.0")
        {var pack=WorldZone.Encounters.Single(e=>e.Id=="ash.0.pack.0");return new(pack.Id,"First patrol",V(pack.Position),"encounter");}
        if(step.Id=="loot")
        {
            var loot=NearbyLoot.FirstOrDefault(d=>d.Receipt=="ash.0.pack.0");
            if(loot is not null)return new("loot:"+loot.Item.Id,"Patrol reward",V(loot.Position),"cache");
        }
        return null;
    }
}

public partial class CombatEffects
{
    private void DrawAdventureLoot()
    {
        if(!arena.AdventureActive)return;
        foreach(var loot in arena.NearbyLoot)
        {
            var p=new Vector2(loot.Position.X,loot.Position.Y);
            if(p.DistanceTo(arena.Player.Position)>440||!arena.WorldCellKnown(p))continue;
            var color=RpgTheme.GearColor(loot.Item);
            var selected=p.DistanceTo(arena.Player.Position)<=84;
            DrawLine(p,p-new Vector2(0,36),new Color(color,.28f),selected?5:3);
            DrawCircle(p-new Vector2(0,6),4,color);DrawArc(p,11,0,Mathf.Tau,24,new Color(color,.6f),1);
            var name=loot.Item.Name;var width=ThemeDB.FallbackFont.GetStringSize(name,fontSize:11).X;
            DrawRect(new(p.X-width/2-6,p.Y-55,width+12,18),new Color("101214",.92f));
            DrawString(ThemeDB.FallbackFont,p-new Vector2(width/2,42),name,fontSize:11,modulate:color);
            if(selected)DrawString(ThemeDB.FallbackFont,p-new Vector2(24,-18),arena.Controls.Glyph("interact")+"  PICK UP",fontSize:10,modulate:Colors.White);
        }
        var town=arena.WorldZone.Id=="hearth";
        if(arena.PortalChanneling||town&&arena.Character!.State.World!.Adventure!.ReturnPortal is not null)
        {
            var p=town?new Vector2(700,430):arena.Player.Position;
            DrawArc(p,26,0,Mathf.Tau,48,new Color("5adecf",.6f),2);
            DrawArc(p,20,-Mathf.Pi/2,-Mathf.Pi/2+Math.Max(.02f,arena.PortalProgress*Mathf.Tau),48,new Color("b1fff2"),3);
            if(town)DrawString(ThemeDB.FallbackFont,p-new Vector2(46,36),"P · RETURN PORTAL",fontSize:11,modulate:new Color("b1fff2"));
        }
    }
}
