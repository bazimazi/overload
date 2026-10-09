using Godot;
using Overload.Domain;

namespace Overload.Game;
public partial class ArenaHud
{
    private GearSlot? inventoryFilter;
    private void Inventory()
    {
        if (arena.Character is null) return;
        var state = arena.Character.State;
        ClearMenu("HEARTH / EQUIPMENT & FORGE", "Arsenal", $"{CounterText.Short(state.Gold)} gold  •  {CounterText.Short(state.Alloy)} Alloy  •  {state.Inventory.Length}/{EquipmentRules.Capacity} items");
        PanelTabs("Arsenal");
        var low=state.Inventory.Count(i=>i.Band<=2&&!i.Locked&&!state.Equipment.ContainsValue(i.Id));
        if(state.World?.Adventure is not null&&state.World.ActiveZone.Id=="hearth")
            PanelButton(options,$"Salvage {low} spare Common / Magic items · keep equipped, locked and Rare+ gear",()=>{arena.UpdateCharacter(EquipmentRules.SalvageLowRarity);Inventory();},low==0);
        var columns = new HBoxContainer(); columns.AddThemeConstantOverride("separation", 18); options.AddChild(columns);
        var equipped = Section(columns, state.Frame.ToString()); equipped.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        equipped.GetParent<Control>().SizeFlagsHorizontal=SizeFlags.ShrinkBegin;
        equipped.AddChild(new EquippedFigure { State = state, Inspect = InspectItem });
        StatLine(equipped, "Attunement", CounterText.Short(state.AttunementGrade));
        StatLine(equipped, "Attack power", EquipmentRules.Stat(state, AffixKind.Attack).ToString());
        StatLine(equipped, "Armor", EquipmentRules.Stat(state, AffixKind.Armor).ToString());
        var bag = Section(columns, "Satchel");
        var filters = new HBoxContainer(); bag.AddChild(filters);
        PanelButton(filters, "All", () => { inventoryFilter = null; Inventory(); });
        var filter = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        filter.AddItem("Every equipment slot");
        foreach (var slot in Enum.GetValues<GearSlot>()) filter.AddItem(slot.ToString());
        filter.Selected = inventoryFilter is {} selected ? (int)selected + 1 : 0;
        filter.ItemSelected += index => { inventoryFilter = index == 0 ? null : (GearSlot)(index - 1); Inventory(); }; filters.AddChild(filter);
        var grid = new GridContainer { Columns = Size.X>=1200?10:6, SizeFlagsHorizontal = SizeFlags.ExpandFill }; grid.AddThemeConstantOverride("h_separation", 4); grid.AddThemeConstantOverride("v_separation", 4); bag.AddChild(grid);
        var items = state.Inventory.Where(i => inventoryFilter is null || i.Slot == inventoryFilter).OrderBy(i=>state.Equipment.ContainsValue(i.Id))
            .ThenByDescending(i=>i.Band).ThenByDescending(i=>i.BaseValue).ToArray();
        Button? first = null;
        for (var i = 0; i < EquipmentRules.Capacity; i++)
        {
            var item = i < items.Length ? items[i] : null;
            var b = new Button { CustomMinimumSize = new(56, 56), SizeFlagsHorizontal = SizeFlags.ExpandFill, Disabled = item is null, TooltipText = item is null ? "Empty slot" : $"{item.Name}\n{item.Slot} • {AdventureRules.Rarity(item)} • Quality {item.Quality}/3\n{item.BaseKind} +{item.BaseValue}\n" + string.Join("\n", item.Affixes.Select(a => $"{a.Kind} +{a.Value}")), AccessibilityName = item?.Name ?? "Empty slot" };
            grid.AddChild(b);
            if (item is null) continue;
            var worn = state.Equipment.ContainsValue(item.Id);
            var art = new RelicIcon { Index = 18 + (int)item.Slot, Accent = worn ? teal : RpgTheme.GearColor(item) }; b.AddChild(art); art.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); art.OffsetLeft = 4; art.OffsetTop = 4; art.OffsetRight = -4; art.OffsetBottom = -4;
            if (worn || item.Locked) { var marker = new Label { Text = item.Locked ? "◆" : "E", Position = new(6, 4), MouseFilter = MouseFilterEnum.Ignore }; marker.AddThemeColorOverride("font_color", teal); b.AddChild(marker); }
            b.Pressed += () => InspectItem(item.Id); first ??= b;
        }
        BodyLabel(bag, "E  Equipped     ◆  Locked\nSelect an item to compare, equip, improve or salvage.", 12, muted);
        first?.GrabFocus(); AddButton(arena.Playing?"Return to adventure":"Back to Hearth", ReturnFromBuild, first is null); goBack = ReturnFromBuild;
    }
    public void InspectItem(Guid id)
    {
        var state = arena.Character!.State; var item = EquipmentRules.Find(state, id);
        ClearMenu("ARSENAL / COMPARE & FORGE", item.Name, $"{item.Slot}  •  {AdventureRules.Rarity(item)}  •  Quality {item.Quality}/3");
        PanelTabs("Arsenal");
        var worn = state.Equipment.TryGetValue(item.Slot, out var existing) ? EquipmentRules.Find(state, existing) : null;
        var quickAfter=EquipmentRules.Equip(state,id);var beforeBuild=FoundationRules.Build(arena.BaseBalance,state);var afterBuild=FoundationRules.Build(arena.BaseBalance,quickAfter);
        var beforeCombat=new PlayerCombat(beforeBuild,character:state);var afterCombat=new PlayerCombat(afterBuild,character:quickAfter);var basicId=FrameRules.Basic(state.Frame);
        var damageBefore=beforeCombat.AttackBudget(beforeBuild.Skills.Single(s=>s.Id==basicId))/1000;var damageAfter=afterCombat.AttackBudget(afterBuild.Skills.Single(s=>s.Id==basicId))/1000;
        BodyLabel(options,$"Basic damage {CounterText.Short(damageBefore)} → {CounterText.Short(damageAfter)}    Life {CounterText.Short(beforeCombat.MaximumLife/1000)} → {CounterText.Short(afterCombat.MaximumLife/1000)}    Armor {beforeBuild.Hero.Armor} → {afterBuild.Hero.Armor}",14,damageAfter>damageBefore?teal:damageAfter<damageBefore?ember:ink);
        if(item.Power is not null)BodyLabel(options,AdventureRules.PowerText(item.Power),13,gold);
        var primary=new HBoxContainer();options.AddChild(primary);
        var equip=PanelButton(primary,state.Equipment.ContainsValue(id)?"Equipped":"Equip item",()=>{arena.UpdateCharacter(s=>EquipmentRules.Equip(s,id));InspectItem(id);},state.Equipment.ContainsValue(id));
        var back=PanelButton(primary,"Back to inventory",Inventory);
        if(!equip.Disabled)equip.GrabFocus();else back.GrabFocus();
        var columns = new HBoxContainer(); columns.AddThemeConstantOverride("separation", 18); options.AddChild(columns);
        ItemComparisonCard(columns, worn, "EQUIPPED"); ItemComparisonCard(columns, item, "SELECTED", true);
        var after = EquipmentRules.Equip(state, id);
        var stats = Section(options, "When equipped");
        var beforeProfile=FoundationRules.Build(arena.BaseBalance,state);var afterProfile=FoundationRules.Build(arena.BaseBalance,after);
        var beforeActor=new PlayerCombat(beforeProfile,character:state);var afterActor=new PlayerCombat(afterProfile,character:after);
        StatLine(stats,"Maximum Life",CounterText.Short(beforeActor.MaximumLife/1000)+" → "+CounterText.Short(afterActor.MaximumLife/1000),afterActor.MaximumLife>beforeActor.MaximumLife?teal:ink);
        var basic=FrameRules.Basic(state.Frame);var beforeDamage=beforeActor.AttackBudget(beforeProfile.Skills.Single(s=>s.Id==basic));var afterDamage=afterActor.AttackBudget(afterProfile.Skills.Single(s=>s.Id==basic));
        StatLine(stats,"Basic attack",CounterText.Short(beforeDamage/1000)+" → "+CounterText.Short(afterDamage/1000),afterDamage>beforeDamage?teal:ink);
        var statsGrid = new GridContainer { Columns = Size.X<1100?2:3, SizeFlagsHorizontal = SizeFlags.ExpandFill }; stats.AddChild(statsGrid);
        foreach (var stat in Enum.GetValues<AffixKind>())
        {
            var before = EquipmentRules.Stat(state, stat); var next = EquipmentRules.Stat(after, stat); var delta = next - before;
            BodyLabel(statsGrid, $"{SkillNameText(stat.ToString())}\n{before} → {next}  ({delta:+0;-0;0})", 14, delta > 0 ? teal : delta < 0 ? ember : muted);
        }
        var actions = new GridContainer { Columns = Size.X<1200?1:2, SizeFlagsHorizontal = SizeFlags.ExpandFill }; options.AddChild(actions);
        PanelButton(actions, item.Locked ? "Unlock item" : "Lock item", () => { arena.UpdateCharacter(s => EquipmentRules.ToggleLock(s, id)); InspectItem(id); });
        var forgeAvailable=state.World?.Adventure is null?!arena.Playing:state.World.ActiveZone.Id=="hearth";
        PanelButton(actions, item.Quality < 3 ? $"Improve • {100 * (item.Quality + 1)} gold / {10 * (item.Quality + 1)} Alloy" : "Quality mastered", () => { arena.UpdateCharacter(s => EquipmentRules.Upgrade(s, id)); InspectItem(id); }, !forgeAvailable || item.Quality >= 3 || state.Gold < 100 * (item.Quality + 1) || state.Alloy < 10 * (item.Quality + 1));
        PanelButton(actions, "Replace affix • 75 gold / 15 Alloy", () => Affixes(id), !forgeAvailable || state.Gold < 75 || state.Alloy < 15);
        PanelButton(actions, "Salvage • +25 gold / +5 Alloy", () => { arena.UpdateCharacter(s => EquipmentRules.Salvage(s, id)); Inventory(); }, !forgeAvailable || item.Locked || state.Equipment.ContainsValue(id));
        if(!forgeAvailable)Text("Forge services are in Hearth. Close the inventory and press P to return to town.",14,gold);
        Issue(); AddButton("Back to Arsenal", Inventory); goBack = Inventory;
    }
    private static string SkillNameText(string value) => System.Text.RegularExpressions.Regex.Replace(value, "([a-z])([A-Z])", "$1 $2");
    private void ItemComparisonCard(Node parent, GearItem? item, string label, bool selected = false)
    {
        var box = Section(parent, label);
        if (item is null) { BodyLabel(box, "Empty equipment slot", 16, muted); return; }
        var row = new HBoxContainer(); box.AddChild(row); row.AddChild(new RelicIcon { Index = 18 + (int)item.Slot, CustomMinimumSize = new(86, 86), Accent = selected ? gold : teal });
        var detail = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; row.AddChild(detail);
        BodyLabel(detail, item.Name, 20, RpgTheme.GearColor(item), true);
        BodyLabel(detail, $"{item.Slot} • {AdventureRules.Rarity(item)}\nQuality {item.Quality}/3 • +{item.Quality * 4}% base", 13, muted);
        box.AddChild(new HSeparator()); StatLine(box, SkillNameText(item.BaseKind.ToString()), "+" + item.BaseValue);
        foreach (var affix in item.Affixes) StatLine(box, SkillNameText(affix.Kind.ToString()), "+" + affix.Value, teal);
        if(item.Power is not null)BodyLabel(box,AdventureRules.PowerText(item.Power),15,gold);
        BodyLabel(box, item.Locked ? "Locked against salvage" : "A remnant of a life worth keeping.", 12, muted);
    }
    private void Affixes(Guid id)
    {
        var item = EquipmentRules.Find(arena.Character!.State, id);
        ClearMenu("FORGE / 75 GOLD & 15 ALLOY", "Reforge an inscription", "Choose a replacement. After reforging, only that affix slot can change again.");
        var grid = CardGrid();
        for (var i = 0; i < 2; i++)
        {
            var slot = i; if (item.ReplaceableSlot is { } locked && locked != slot) continue;
            foreach (var kind in Enum.GetValues<AffixKind>().Where(k => EquipmentRules.LegalAffix(item.Slot, k) && item.Affixes[1 - slot].Kind != k))
                ArtButton(grid, 18 + (int)item.Slot, SkillNameText(kind.ToString()), $"Replace affix {slot + 1} • 75 gold / 15 Alloy", () => { arena.UpdateCharacter(s => EquipmentRules.ReplaceAffix(s, id, slot, kind)); InspectItem(id); });
        }
        AddButton("Cancel", () => InspectItem(id), true); goBack = () => InspectItem(id);
    }
}
