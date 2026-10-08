using System.Security.Cryptography;
using System.Text;

namespace Overload.Domain;

/// <summary>Slice rewards and checkpoint move are one aggregate change, identified by run and room.</summary>
public static class JourneyRules
{
    public static int Length(CharacterState s) => s.RegionalCampaign ? 16 : 8;
    public static CharacterState Begin(CharacterState s, Guid runId)
    {
        if (runId == Guid.Empty || s.RunId != Guid.Empty && s.CheckpointRoom < Length(s)) throw new InvalidOperationException("An unfinished journey must be resumed");
        return s with { RunId = runId, CheckpointRoom = 0, ClaimedRooms = [] };
    }
    public static CharacterState Clear(CharacterState s, Guid runId, int room)
    {
        if (runId != s.RunId || runId == Guid.Empty || room < 0 || room >= Length(s)) throw new InvalidOperationException("Wrong journey receipt");
        if (s.ClaimedRooms.Contains(room)) return s;
        if (s.CheckpointRoom != room) throw new InvalidOperationException("Room is not the active checkpoint");
        var slot = (GearSlot)(room % 6);
        var template = EquipmentRules.StarterItems.Single(i => i.Slot == slot);
        var prefix = s.RegionalCampaign ? ((Region)(room / 4)) switch { Region.Ash => "Ash-forged", Region.Glass => "Glassbound", Region.Hollow => "Archivist", _ => "Crownscar" } : room == 7 ? "Bellkeeper" : "Recovered";
        var item = template with { Id = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"{runId:N}:{room}"))[..16]),
            Name = prefix + " " + slot, Band = 2, BaseValue = template.BaseValue + 8 + room * 2 };
        var full = s.Inventory.Length >= EquipmentRules.Capacity;
        var gold = 100 + room * 25; if (s.Inscriptions.Contains("field-notes")) gold = gold * 110 / 100;
        var result = s with { Gold = s.Gold + gold + (full ? 25 : 0), Alloy = s.Alloy + 8 + (s.Inscriptions.Contains("salvagers-mark") ? 1 : 0) + (full ? 5 : 0),
            Inventory = full ? s.Inventory : s.Inventory.Add(item), ClaimedRooms = s.ClaimedRooms.Add(room), CheckpointRoom = room + 1,
            SkillMilestones = room % 2 == 1 ? s.SkillMilestones.Add(Math.Min(3,room / 2)) : s.SkillMilestones,
            Journal = [.. s.Journal.TakeLast(31), $"{(s.RegionalCampaign ? ((Region)(room / 4)).ToString() : "Court")} {room + 1}: {gold} gold, 8 Alloy, {item.Name}{(full ? " converted: bag full (+25 gold, +5 Alloy)" : " recovered")}"] };
        return FoundationRules.AddXp(result with { FractureUnlocked = s.FractureUnlocked || room == Length(s)-1 }, 1200 + room * 200);
    }
}
