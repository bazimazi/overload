using System.Collections.Immutable;

namespace Overload.Domain;
public enum GearSlot { Weapon, OffHand, Helm, Chest, Gloves, Boots }
public enum AffixKind { Attack, Life, Armor, Resistance, FocusRegeneration, CriticalChance }
public sealed record GearAffix(AffixKind Kind, int Value);
public sealed record GearItem(Guid Id, string Name, GearSlot Slot, int Band, int Quality, AffixKind BaseKind, int BaseValue,
    ImmutableArray<GearAffix> Affixes, bool Locked = false, int? ReplaceableSlot = null, string? Power = null);

public static class EquipmentRules
{
    public const int Capacity = 36;
    public static ImmutableArray<GearItem> StarterItems { get; } = [.. Enum.GetValues<GearSlot>().Select((slot, i) => new GearItem(
        new Guid($"00000000-0000-0000-0000-{i + 1:D12}"), "Court " + slot, slot, 1, 0,
        slot == GearSlot.Weapon ? AffixKind.Attack : slot == GearSlot.Chest ? AffixKind.Life : AffixKind.Armor,
        slot == GearSlot.Weapon ? 40 : slot == GearSlot.Chest ? 100 : 25, [new(AffixKind.Resistance, 2), new(AffixKind.FocusRegeneration, 1)]))];
    public static bool LegalAffix(GearSlot slot, AffixKind kind) => kind switch
    {
        AffixKind.Attack or AffixKind.CriticalChance => slot is GearSlot.Weapon or GearSlot.Gloves,
        AffixKind.Life => slot is GearSlot.Chest or GearSlot.Helm or GearSlot.Boots,
        _ => Enum.IsDefined(kind)
    };
    public static CharacterState Equip(CharacterState s, Guid id)
    {
        var item = Find(s, id); return s with { Equipment = s.Equipment.SetItem(item.Slot, id) };
    }
    public static CharacterState ToggleLock(CharacterState s, Guid id)
    { var item = Find(s, id); return Replace(s, item with { Locked = !item.Locked }); }
    public static CharacterState Salvage(CharacterState s, Guid id)
    {
        var item = Find(s, id);
        if (item.Locked || s.Equipment.ContainsValue(id)) throw new InvalidOperationException("Locked or equipped gear cannot be salvaged");
        return s with { Inventory = s.Inventory.Remove(item), Gold = s.Gold + 25, Alloy = s.Alloy + 5 };
    }
    public static CharacterState SalvageLowRarity(CharacterState s)
    {
        if(s.World?.Adventure is not null&&s.World.ActiveZone.Id!="hearth")throw new InvalidOperationException("Salvage services require Hearth");
        var spare=s.Inventory.Where(i=>i.Band<=2&&!i.Locked&&!s.Equipment.ContainsValue(i.Id)).ToArray();
        return s with {Inventory=s.Inventory.RemoveRange(spare),Gold=s.Gold+spare.Length*25,Alloy=s.Alloy+spare.Length*5};
    }
    public static CharacterState Upgrade(CharacterState s, Guid id)
    {
        var item = Find(s, id); if (item.Quality >= 3) throw new InvalidOperationException("Quality is already 3/3");
        var gold = 100 * (item.Quality + 1); var alloy = 10 * (item.Quality + 1);
        Pay(s, gold, alloy); return Replace(s with { Gold = s.Gold - gold, Alloy = s.Alloy - alloy }, item with { Quality = item.Quality + 1 });
    }
    public static CharacterState ReplaceAffix(CharacterState s, Guid id, int slot, AffixKind kind)
    {
        var item = Find(s, id);
        if (slot is < 0 or > 1 || item.ReplaceableSlot is { } fixedSlot && slot != fixedSlot || !LegalAffix(item.Slot, kind)
            || item.Affixes.Where((_, i) => i != slot).Any(a => a.Kind == kind)) throw new InvalidOperationException("Illegal or duplicate affix; only the chosen slot remains replaceable");
        Pay(s, 75, 15);
        return Replace(s with { Gold = s.Gold - 75, Alloy = s.Alloy - 15 }, item with { ReplaceableSlot = slot, Affixes = item.Affixes.SetItem(slot, new(kind, kind == AffixKind.CriticalChance ? 1 : 4)) });
    }
    public static int Stat(CharacterState s, AffixKind kind) => s.Equipment.Values.Select(id => Find(s, id)).Sum(item =>
        (item.BaseKind == kind ? item.BaseValue * (100 + item.Quality * 4) / 100 : 0) + item.Affixes.Where(a => a.Kind == kind).Sum(a => a.Value));
    public static GearItem Find(CharacterState s, Guid id) => s.Inventory.FirstOrDefault(i => i.Id == id) ?? throw new InvalidOperationException("Item no longer exists");
    private static CharacterState Replace(CharacterState s, GearItem item) => s with { Inventory = s.Inventory.SetItem(s.Inventory.IndexOf(Find(s, item.Id)), item) };
    private static void Pay(CharacterState s, int gold, int alloy) { if (s.Gold < gold || s.Alloy < alloy) throw new InvalidOperationException($"Need {gold} gold and {alloy} Alloy"); }
    public static void Validate(CharacterState s)
    {
        if (s.Inventory.IsDefault || s.Inventory.Length > Capacity || s.Inventory.Any(i => i is null) || s.Inventory.Select(i => i.Id).Distinct().Count() != s.Inventory.Length || s.Equipment is null) throw new InvalidDataException("Invalid inventory");
        foreach (var i in s.Inventory) ValidateItem(i);
        foreach (var pair in s.Equipment)
            if (!Enum.IsDefined(pair.Key) || !s.Inventory.Any(i => i.Id == pair.Value && i.Slot == pair.Key)) throw new InvalidDataException("Invalid equipped item reference");
    }
    public static void ValidateItem(GearItem i)
    {
        if (i.Id == Guid.Empty || string.IsNullOrWhiteSpace(i.Name) || i.Name.Length > 128 || !Enum.IsDefined(i.Slot) || i.Band is < 1 or > 5 || i.Quality is < 0 or > 3 || !Enum.IsDefined(i.BaseKind) || i.BaseValue is < 0 or > 1000
            || i.Affixes.IsDefault || i.Affixes.Length != 2 || i.Affixes.Any(a => a is null || !LegalAffix(i.Slot, a.Kind) || a.Value is < 0 or > 20)
            || i.Affixes.Select(a => a.Kind).Distinct().Count() != 2 || i.ReplaceableSlot is < 0 or > 1
            || i.Power is not null && (!AdventureRules.PowerIds.Contains(i.Power)||i.Band != 5)) throw new InvalidDataException("Invalid item definition");
    }
}
