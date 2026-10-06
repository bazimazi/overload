using Godot;
using Overload.Domain;

namespace Overload.Game;
public partial class ArenaHud
{
    private void Inventory()
    {
        if (arena.Playing || arena.Character is null) return;
        var state = arena.Character.State;
        ClearMenu("HEARTH / EQUIPMENT", "Arsenal", $"{CounterText.Short(state.Gold)} gold · {CounterText.Short(state.Alloy)} Alloy · {state.Inventory.Length}/{EquipmentRules.Capacity} items");
        foreach (var item in state.Inventory)
            AddButton($"{(state.Equipment.ContainsValue(item.Id) ? "Equipped" : "Bag")} · {item.Name} +{item.Quality}{(item.Locked ? " [locked]" : "")}", () => InspectItem(item.Id), item == state.Inventory[0]);
        AddButton("Back to Hearth", Title);
    }
    public void InspectItem(Guid id)
    {
        var state = arena.Character!.State; var item = EquipmentRules.Find(state, id);
        ClearMenu("GEAR / COMPARE / FORGE", item.Name, $"{item.Slot} · Band {item.Band} · Quality {item.Quality}/3");
        var equipped = EquipmentRules.Equip(state, id);
        var beforeProfile = FoundationRules.Build(arena.BaseBalance,state); var afterProfile=FoundationRules.Build(arena.BaseBalance,equipped);
        var beforeActor=new PlayerCombat(beforeProfile,character:state); var afterActor=new PlayerCombat(afterProfile,character:equipped);
        Text($"Shared attunement {CounterText.Short(state.AttunementGrade)} (applied once)\nFinal Life: {CounterText.Short(beforeActor.MaximumLife/1000)} → {CounterText.Short(afterActor.MaximumLife/1000)}\nFinal basic attack: {CounterText.Short(beforeActor.AttackBudget(beforeProfile.Skills.Single(s=>s.Id==FrameRules.Basic(state.Frame)))/1000)} → {CounterText.Short(afterActor.AttackBudget(afterProfile.Skills.Single(s=>s.Id==FrameRules.Basic(state.Frame)))/1000)}",14,ink);
        foreach (var stat in Enum.GetValues<AffixKind>()) Text($"{stat}: {EquipmentRules.Stat(state, stat)} → {EquipmentRules.Stat(equipped, stat)}{(state.RedCovenantSelected&&stat==AffixKind.FocusRegeneration?" INACTIVE (Red Covenant)":"")}", 14, muted);
        Text(string.Join(" · ", item.Affixes.Select((a, i) => $"{i + 1}: {a.Kind} +{a.Value}")), 14, gold);
        AddButton("Equip", () => { arena.UpdateCharacter(s => EquipmentRules.Equip(s, id)); InspectItem(id); }, true);
        AddButton(item.Locked ? "Unlock inventory item" : "Lock inventory item", () => { arena.UpdateCharacter(s => EquipmentRules.ToggleLock(s, id)); InspectItem(id); });
        if (item.Quality < 3) AddButton($"Quality +4% base: {100 * (item.Quality + 1)} gold, {10 * (item.Quality + 1)} Alloy", () => { arena.UpdateCharacter(s => EquipmentRules.Upgrade(s, id)); InspectItem(id); });
        AddButton("Choose a replacement affix (75 gold / 15 Alloy)", () => Affixes(id));
        if (!item.Locked && !state.Equipment.ContainsValue(id)) AddButton("Salvage: +25 gold / +5 Alloy", () => { arena.UpdateCharacter(s => EquipmentRules.Salvage(s, id)); Inventory(); });
        if (!string.IsNullOrEmpty(arena.SaveProblem)) Text(arena.SaveProblem, 14, gold);
        AddButton("Back to inventory", Inventory);
        goBack = Inventory;
    }
    private void Affixes(Guid id)
    {
        var item = EquipmentRules.Find(arena.Character!.State, id);
        ClearMenu("DETERMINISTIC CRAFT / NO RANDOM FAILURE", "Replace affix", "After replacement, only that affix slot can change again.");
        for (var i = 0; i < 2; i++)
        {
            var slot = i;
            if (item.ReplaceableSlot is { } locked && locked != slot) continue;
            foreach (var kind in Enum.GetValues<AffixKind>().Where(k => EquipmentRules.LegalAffix(item.Slot, k) && item.Affixes[1 - slot].Kind != k))
                AddButton($"Slot {slot + 1} → {kind}: 75 gold + 15 Alloy", () => { arena.UpdateCharacter(s => EquipmentRules.ReplaceAffix(s, id, slot, kind)); InspectItem(id); });
        }
        AddButton("Cancel", () => InspectItem(id), true);
        goBack = () => InspectItem(id);
    }
}
