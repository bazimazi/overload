using Overload.Domain;
using Xunit;
namespace Overload.Domain.Tests;
public class EquipmentTests
{
    [Fact]
    public void EquipLockAndSalvagePreserveReferencesAndProtectItems()
    {
        var s = new CharacterState(); CharacterRules.Validate(s); var item = s.Inventory[0];
        Assert.Throws<InvalidOperationException>(() => EquipmentRules.Salvage(s, item.Id));
        var copy = item with { Id = Guid.NewGuid() }; s = s with { Inventory = s.Inventory.Add(copy) };
        s = EquipmentRules.ToggleLock(s, copy.Id); Assert.Throws<InvalidOperationException>(() => EquipmentRules.Salvage(s, copy.Id));
        s = EquipmentRules.ToggleLock(s, copy.Id); s = EquipmentRules.Equip(s, copy.Id); s = EquipmentRules.Salvage(s, item.Id);
        Assert.Equal(525, s.Gold); Assert.Equal(105, s.Alloy); CharacterRules.Validate(s);
        Assert.Throws<InvalidOperationException>(() => EquipmentRules.Salvage(s, item.Id));
    }
    [Fact]
    public void QualityAndAffixCraftingHaveExactCostsCapsAndChosenSlot()
    {
        var s = new CharacterState { Gold = 10000, Alloy = 1000 }; var id = s.Inventory[0].Id; var before = EquipmentRules.Stat(s, AffixKind.Attack);
        for (var i = 0; i < 3; i++) s = EquipmentRules.Upgrade(s, id);
        Assert.Equal(9400, s.Gold); Assert.Equal(940, s.Alloy); Assert.True(EquipmentRules.Stat(s, AffixKind.Attack) > before);
        Assert.Throws<InvalidOperationException>(() => EquipmentRules.Upgrade(s, id));
        s = EquipmentRules.ReplaceAffix(s, id, 0, AffixKind.Attack); Assert.Equal(9325, s.Gold); Assert.Equal(925, s.Alloy);
        Assert.Throws<InvalidOperationException>(() => EquipmentRules.ReplaceAffix(s, id, 1, AffixKind.Armor));
        Assert.Throws<InvalidOperationException>(() => EquipmentRules.ReplaceAffix(s, id, 0, AffixKind.FocusRegeneration));
        Assert.Throws<InvalidOperationException>(() => EquipmentRules.Upgrade(s with { Gold = 0 }, s.Inventory[1].Id));
        CharacterRules.Validate(s);
    }
}
