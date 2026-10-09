using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

namespace Overload.Domain;

public enum AdventureDifficulty { Story, Adventurer, Veteran }
public sealed record WorldLoot(string ZoneId, WorldPoint Position, GearItem Item, string Receipt);
public sealed record TownReturn(string ZoneId, WorldPoint Position);
public sealed record FrontierContract(long Depth, ImmutableHashSet<string> Completed);
public sealed record AdventureProgress
{
    public string Version { get; init; } = "adventure.v1";
    public AdventureDifficulty Difficulty { get; init; } = AdventureDifficulty.Adventurer;
    public ImmutableHashSet<string> Milestones { get; init; } = [];
    public ImmutableArray<WorldLoot> GroundLoot { get; init; } = [];
    public TownReturn? ReturnPortal { get; init; }
    public FrontierContract? Contract { get; init; }
    public long ContractsCompletedThrough { get; init; } = -1;
}
public sealed record AdventureObjective(string Id, string Title, string Instruction, string Hint, string Reward);

/// <summary>Persistent adventure rewards. A cleared encounter owns its loot receipt even before pickup.</summary>
public static class AdventureRules
{
    public const int MaximumGroundLoot = 64;
    public static readonly ImmutableArray<string> PowerIds=["fleet","flow","guard","tempo","reaving","split","nova"];
    public static bool IsEliteEncounter(WorldEncounter e)=>e.Boss<0&&e.Optional&&e.Enemies.Length>0&&e.Id.Sum(c=>c)%3==0;
    private static readonly ImmutableHashSet<string> KnownMilestones = ["accepted", "road", "collected", "equipped", "trained"];
    public static CharacterState Enable(CharacterState s)
    {
        if (s.World is not { } w) throw new InvalidOperationException("Enter the connected world first");
        if (w.Adventure is not null) return s;
        var veteran = s.ValidatedLevel > 1 || w.Claims.Any(id=>id.Contains(".pack.",StringComparison.Ordinal)||id.EndsWith(".boss",StringComparison.Ordinal)) || w.LegacyRewardsSuppressed;
        return s with { World = w with { Adventure = new() { Milestones = veteran ? KnownMilestones : [] } } };
    }
    private static AdventureProgress Require(CharacterState s) => s.World?.Adventure ?? throw new InvalidOperationException("Adventure is unavailable");
    private static CharacterState With(CharacterState s, AdventureProgress a) => s with { World = s.World! with { Adventure = a } };
    public static CharacterState Accept(CharacterState s) => With(s, Require(s) with { Milestones = Require(s).Milestones.Add("accepted") });
    public static CharacterState ObserveBuild(CharacterState s)
    {
        var a = Require(s); var milestones = a.Milestones;
        if (a.Milestones.Contains("collected") && s.Equipment.TryGetValue(GearSlot.Weapon, out var id)
            && !EquipmentRules.StarterItems.Any(i => i.Id == id)) milestones = milestones.Add("equipped");
        if (a.Milestones.Contains("road") && FoundationRules.SkillSpent(s) > 0) milestones = milestones.Add("trained");
        return milestones.SetEquals(a.Milestones) ? s : With(s, a with { Milestones = milestones });
    }
    public static AdventureObjective? Objective(CharacterState s)
    {
        if (s.World?.Adventure is not { } a) return null;
        var m = a.Milestones;
        if(a.Contract is { } contract)
        {
            var complete=contract.Completed.Count==4;
            return new("contract","Trailkeeper contract",complete?s.World.ActiveZone.Id=="hearth"?"Collect your contract reward [J]":"Return to Hearth for your reward [P]":$"Secure Reach {contract.Depth+1} ({contract.Completed.Count}/4)",
                complete?"Mara pays for the survey. Open your quest journal in Hearth and claim the reward.":"Defeat the three patrols along the central road, then activate Trailkeeper Camp. Follow the gold route.","Legendary equipment + XP + gold + Alloy");
        }
        if (!m.Contains("accepted")) return new("meet", "A road worth saving", "Speak to Mara in Hearth", "Follow the gold marker. Press G near Mara.", "First battle: a weapon, 800 bonus XP and 50 gold");
        if (!m.Contains("road")) return new("road", "The first patrol", "Clear the guards on Cinderroad", "Take the west road. Click an enemy to approach and attack; hold to repeat. Q / E / R use skills.", "An upgraded weapon + 800 bonus XP + 50 gold");
        if (!m.Contains("collected")) return new("loot", "Claim your reward", "Pick up the weapon from the patrol", "Press G near the glowing item. Open inventory with I.", "A stronger weapon for your Frame");
        if (!m.Contains("equipped")) return new("equip", "Ready for the journey", "Equip your new weapon [I]", "Select the weapon, compare its damage, then choose Equip.", "Stronger basic attacks and skills");
        if (!m.Contains("trained")) return new("train", "Shape your power", "Spend a skill point [K]", "Train your basic attack or an active skill. Points are saved.", "Your first skill rank");
        if (!s.World.Claims.Contains("ash.1.boss")) return new("enforcer", "Break the blockade", "Defeat the Ash enforcer", "Follow the gold route. Amber warns of danger; Space evades, F heals.", "Rare or legendary equipment + XP");
        var w=s.World;
        if(w.Resolved.Count==4)return null;
        var region=!w.Resolved.Contains(w.ActiveZone.Region)?w.ActiveZone.Region:Enum.GetValues<Region>().First(r=>!w.Resolved.Contains(r));
        var n=(int)region;var places=WorldContent.Places[n];
        if(!w.Claims.Contains(WorldContent.Id(region,1)+".boss"))return new("regional-enforcer",WorldContent.RegionNames[n],"Defeat "+places[1],"Follow the regional road. Defeat its enforcer, then restore the dungeon conduits.","Boss equipment and XP");
        var conduits=Enumerable.Range(0,2).Count(i=>w.Claims.Contains(WorldContent.Id(region,2)+".device."+i));
        if(conduits<2)return new("conduits","Restore the conduits",$"Activate dungeon conduits ({conduits}/2)","Enter "+places[2]+" from the wilderness. Clear the guards, then press G at each conduit.","Open the regional ruler's gate");
        return new("regional-ruler",WorldContent.RegionNames[n],"Defeat "+places[3],"Both conduits are restored. Enter the ruler's arena from the dungeon and break its guard.","Restore a region, unlock roads and earn equipment");
    }
    public static CharacterState Reward(CharacterState s, WorldReward r, string receipt)
    {
        var w = s.World!; var a = Require(s);
        var encounter = w.ActiveZone.Encounters.FirstOrDefault(e => e.Id == receipt);
        var first = receipt == "ash.0.pack.0" && !a.Milestones.Contains("road");
        var boss = encounter?.Boss >= 0;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{w.CampaignId:N}:adventure:{receipt}"));
        var elite=encounter is not null&&IsEliteEncounter(encounter);
        var shouldDrop = first || boss || elite || r.Item >= 0 || encounter is not null && hash[0] % 3 == 0;
        var gold = r.Gold * (s.Inscriptions.Contains("field-notes") ? 110 : 100) / 100;
        var alloy = r.Alloy;
        if (shouldDrop)
        {
            var slot = first ? GearSlot.Weapon : r.Item >= 0 ? (GearSlot)(r.Item % 6) : (GearSlot)(hash[1] % 6);
            var band = first ? 3 : boss ? receipt=="ash.1.boss"||hash[2]%2==0?5:3 : elite?3:2 + hash[2] % 2;
            var template = EquipmentRules.StarterItems.Single(i => i.Slot == slot);
            var growth = Math.Min(80, (int)System.Numerics.BigInteger.Min(80, s.ValidatedLevel * 2));
            var legal = Enum.GetValues<AffixKind>().Where(k => EquipmentRules.LegalAffix(slot, k)).ToArray();
            var affix1 = legal[hash[3] % legal.Length]; var affix2 = legal.Where(k => k != affix1).ToArray();
            var power = band == 5 ? receipt=="ash.1.boss"?s.Frame==FrameId.Threadseer?"split":"nova":PowerIds[hash[6]%PowerIds.Length] : null;
            var item = template with { Id = new Guid(hash[..16]), Name = (band == 5 ? "Runebound " : first ? "Mara's " : band == 3 ? "Tempered " : "Inscribed ") + slot,
                Band = band, BaseValue = template.BaseValue + (first ? 32 : 12 + growth + hash[7] % 12),
                Affixes = [new(affix1, 2 + hash[4] % 5), new(affix2[hash[5] % affix2.Length], 2 + hash[8] % 5)], Power = power };
            var position = encounter?.Position ?? w.ActiveZone.Sites.FirstOrDefault(p => p.Id == receipt)?.Position ?? w.Arrival;
            var capacity=MaximumGroundLoot-(!first&&!a.Milestones.Contains("road")?1:0);
            if (a.GroundLoot.Length < capacity) a = a with { GroundLoot = a.GroundLoot.Add(new(w.ActiveZone.Id, position, item, receipt)) };
            else { gold += 25; alloy += 5; }
            if (s.Inscriptions.Contains("salvagers-mark")) alloy++;
        }
        if (first) a = a with { Milestones = a.Milestones.Add("accepted").Add("road") };
        return FoundationRules.AddXp(With(s, a) with { Gold = s.Gold + gold + (first ? 50 : 0), Alloy = s.Alloy + alloy }, r.Xp + (first ? 800 : 0));
    }
    public static CharacterState PickUp(CharacterState s, Guid itemId, WorldPoint position)
    {
        var a = Require(s); var drop = a.GroundLoot.FirstOrDefault(d => d.Item.Id == itemId);
        if (drop is null) return s; // A repeated pickup never grants another item.
        if (drop.ZoneId != s.World!.ActiveZone.Id || System.Numerics.Vector2.Distance(position.Vector, drop.Position.Vector) > 84
            ||!s.World.ActiveZone.Geometry.Navigation().Clear(position.Vector,drop.Position.Vector,0))
            throw new InvalidOperationException("Move closer to pick up this item");
        if (s.Inventory.Length >= EquipmentRules.Capacity) throw new InvalidOperationException("Inventory full. Salvage an unequipped item at Hearth.");
        if (s.Inventory.Any(i => i.Id == itemId)) throw new InvalidDataException("Loot already belongs to the inventory");
        return With(s with { Inventory = s.Inventory.Add(drop.Item) }, a with { GroundLoot = a.GroundLoot.Remove(drop),
            Milestones = drop.Receipt == "ash.0.pack.0" ? a.Milestones.Add("collected") : a.Milestones });
    }
    public static CharacterState PortalOut(CharacterState s, WorldPoint position)
    {
        var a = Require(s); var w = s.World!;
        if (w.ActiveZone.Id == "hearth") throw new InvalidOperationException("You are already in Hearth");
        if (!w.ActiveZone.Geometry.Navigation().Clear(position.Vector, position.Vector, 20)) throw new InvalidOperationException("Move into open ground to open the portal");
        s = With(s, a with { ReturnPortal = new(w.ActiveZone.Id, position) });
        return WorldRules.Enter(s, "hearth", new(700, 430));
    }
    public static CharacterState PortalBack(CharacterState s)
    {
        var a = Require(s);
        if (s.World!.ActiveZone.Id != "hearth" || a.ReturnPortal is not { } p) throw new InvalidOperationException("There is no return portal in Hearth");
        return WorldRules.Enter(With(s, a with { ReturnPortal = null }), p.ZoneId, p.Position);
    }
    public static CharacterState SetDifficulty(CharacterState s, AdventureDifficulty difficulty)
    {
        if (!Enum.IsDefined(difficulty) || s.World!.ActiveZone.Id != "hearth") throw new InvalidOperationException("Change difficulty in Hearth");
        return With(s, Require(s) with { Difficulty = difficulty });
    }
    public static string Rarity(GearItem i) => i.Band switch { 1 => "Common", 2 => "Magic", 3 => "Rare", 4 => "Epic", _ => "Legendary" };
    public static string PowerText(string? power) => power switch { "fleet" => "Fleet: move 6% faster", "flow" => "Flow: regenerate 4 more Focus each second", "guard" => "Guard: 12% more maximum Life", "tempo" => "Tempo: active skill cooldowns are 10% shorter",
        "reaving"=>"Reaving: basic attacks sweep farther or pierce two more enemies",
        "split"=>"Split volley: projectile attacks fan into 3 bolts at 65% damage; each enemy is hit once",
        "nova"=>"Deathburst: your first kill per attack erupts for 50% damage around the victim; bursts cannot trigger bursts", _ => "" };
    public static bool HasPower(CharacterState s, string power) => s.Equipment.Values.Any(id => EquipmentRules.Find(s, id).Power == power);
    public static ImmutableArray<string> ContractTargets(long depth)=>[FrontierWorld.Id(depth)+".pack.7",FrontierWorld.Id(depth)+".pack.8",FrontierWorld.Id(depth)+".pack.9",FrontierWorld.Id(depth)+".camp"];
    public static CharacterState BeginContract(CharacterState s)
    {
        var a=Require(s);var w=s.World!;
        if(w.ActiveZone.Id!="hearth"||!a.Milestones.Contains("trained"))throw new InvalidOperationException("Finish your first training, then take a contract in Hearth");
        var depth=a.Contract?.Depth??checked(w.Frontier.Farthest+1);
        if(depth<=w.Frontier.RetiredThrough)throw new InvalidOperationException("This old survey was archived. Abandon it and choose a new contract.");
        if(a.Contract is null)s=With(s,a with {Contract=new(depth,[])});
        return WorldRules.Enter(s,FrontierWorld.Id(depth),new(180,1920));
    }
    public static CharacterState ObserveContract(CharacterState s)
    {
        if(s.World?.Adventure?.Contract is not { } contract)return s;
        if(!s.World.Frontier.Surveys.TryGetValue(contract.Depth,out var survey))return s;
        var completed=contract.Completed.Union(ContractTargets(contract.Depth).Where(survey.Claims.Contains));
        return completed.SetEquals(contract.Completed)?s:With(s,s.World.Adventure with {Contract=contract with {Completed=completed}});
    }
    public static CharacterState AbandonContract(CharacterState s)
    {
        if(s.World!.ActiveZone.Id!="hearth")throw new InvalidOperationException("Manage contracts in Hearth");
        return With(s,Require(s) with {Contract=null});
    }
    public static CharacterState ClaimContract(CharacterState s)
    {
        var a=Require(s);var c=a.Contract??throw new InvalidOperationException("No active contract");
        if(s.World!.ActiveZone.Id!="hearth"||c.Completed.Count!=4||c.Depth<=a.ContractsCompletedThrough)throw new InvalidOperationException("Complete the survey and return to Hearth");
        if(s.Inventory.Length>=EquipmentRules.Capacity)throw new InvalidOperationException("Inventory full. Salvage an unequipped item before claiming the contract.");
        var hash=SHA256.HashData(Encoding.UTF8.GetBytes($"{s.World.CampaignId:N}:contract:{c.Depth}"));
        var slot=(GearSlot)(hash[0]%6);var template=EquipmentRules.StarterItems[(int)slot];
        var worn=s.Equipment.TryGetValue(slot,out var id)?EquipmentRules.Find(s,id):template;
        var item=template with {Id=new Guid(hash[..16]),Name="Trailkeeper's "+slot,Band=5,
            BaseValue=Math.Min(1000,Math.Max(worn.BaseValue+12,template.BaseValue+20+(int)System.Numerics.BigInteger.Min(240,s.ValidatedLevel*4))),
            Affixes=[new(AffixKind.Resistance,4+hash[2]%5),new(AffixKind.FocusRegeneration,2+hash[3]%4)],Power=c.Depth==0?s.Frame==FrameId.Threadseer?"nova":s.Frame==FrameId.Warden?"reaving":"split":PowerIds[hash[4]%PowerIds.Length]};
        var growth=(int)Math.Min(c.Depth,2000);
        s=FoundationRules.AddXp(With(s,a with {Contract=null,ContractsCompletedThrough=c.Depth}) with {Inventory=s.Inventory.Add(item),Gold=s.Gold+100+growth*40,Alloy=s.Alloy+10},800+growth*400);
        return s with {Journal=[..s.Journal.TakeLast(31),$"Trailkeeper survey {c.Depth+1} paid: {item.Name}. Another contract opens a new reach."]};
    }
    public static void Validate(CharacterState s)
    {
        if (s.World?.Adventure is not { } a) return;
        bool Known(string id) => WorldContent.Zones.ContainsKey(id) || FrontierWorld.TryDepth(id, out _);
        if (a.Version != "adventure.v1" || !Enum.IsDefined(a.Difficulty) || a.Milestones is null || !a.Milestones.IsSubsetOf(KnownMilestones)
            ||a.ContractsCompletedThrough< -1||a.ContractsCompletedThrough>s.World.Frontier.Farthest
            ||a.Milestones.Contains("collected")&&!a.Milestones.Contains("road")||a.Milestones.Contains("equipped")&&!a.Milestones.Contains("collected")||a.Milestones.Contains("trained")&&!a.Milestones.Contains("road")
            || a.GroundLoot.IsDefault || !a.Milestones.Contains("road")&&a.GroundLoot.Length>=MaximumGroundLoot
            || a.GroundLoot.Length > MaximumGroundLoot || a.GroundLoot.Any(d => d is null || !Known(d.ZoneId) || d.Item is null
                || string.IsNullOrEmpty(d.Receipt) || d.Receipt.Length > 96 || !float.IsFinite(d.Position.X) || !float.IsFinite(d.Position.Y))
            || a.GroundLoot.Select(d => d.Item.Id).Distinct().Count() != a.GroundLoot.Length || a.GroundLoot.Any(d => s.Inventory.Any(i => i.Id == d.Item.Id)))
            throw new InvalidDataException("Invalid adventure state");
        if(a.Contract is { } contract && (contract.Depth<0||contract.Depth>s.World.Frontier.Farthest||contract.Depth<=a.ContractsCompletedThrough
            ||contract.Completed is null||!contract.Completed.IsSubsetOf(ContractTargets(contract.Depth))))throw new InvalidDataException("Invalid frontier contract");
        var zones=new Dictionary<string,ZoneDefinition>();
        ZoneDefinition Zone(string id)
        {
            if(zones.TryGetValue(id,out var known))return known;
            var zone=id==s.World.ActiveZone.Id?s.World.ActiveZone:FrontierWorld.TryDepth(id,out var depth)?FrontierWorld.Generate(s.World.CampaignId,depth):WorldContent.Zone(id);
            zones[id]=zone;return zone;
        }
        foreach (var drop in a.GroundLoot)
        {
            EquipmentRules.ValidateItem(drop.Item);
            var zone=Zone(drop.ZoneId);
            var source=zone.Encounters.FirstOrDefault(e=>e.Id==drop.Receipt)?.Position??zone.Sites.FirstOrDefault(p=>p.Id==drop.Receipt)?.Position;
            var claimed=FrontierWorld.TryDepth(drop.ZoneId,out var depth)?depth<=s.World.Frontier.RetiredThrough||s.World.Frontier.Surveys.GetValueOrDefault(depth,new()).Claims.Contains(drop.Receipt):s.World.Claims.Contains(drop.Receipt);
            if(source is null||source.Value!=drop.Position||!claimed)
                throw new InvalidDataException("Invalid loot location or receipt");
        }
        if (a.ReturnPortal is { } p)
        {
            if (!Known(p.ZoneId) || p.ZoneId == "hearth") throw new InvalidDataException("Invalid return portal");
            var zone=Zone(p.ZoneId);
            if (!zone.Geometry.Navigation().Clear(p.Position.Vector, p.Position.Vector, 20)) throw new InvalidDataException("Unsafe return portal");
        }
    }
}
