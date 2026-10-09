using Godot;
using Overload.Domain;

namespace Overload.Game;

public partial class ArenaHud
{
    private string fieldSheet="";
    private HBoxContainer gameMenuDock=null!;
    private void InitializeGameDock()
    {
        gameMenuDock=new HBoxContainer {Visible=false};AddChild(gameMenuDock);
        var character=PanelButton(gameMenuDock,"Character",()=>OpenFieldPanel("character"));character.TooltipText="Inspect your Frame and combat stats ["+arena.Controls.Glyph("character")+"]";
        var inventory=PanelButton(gameMenuDock,"Arsenal",()=>OpenFieldPanel("inventory"));inventory.TooltipText="Inspect equipped gear ["+arena.Controls.Glyph("inventory")+"]";
        foreach(var button in new[]{character,inventory})
        {
            button.AddThemeFontSizeOverride("font_size",11);
            var style=ButtonStyle(new("25211b"),new("67543a"));style.ContentMarginLeft=6;style.ContentMarginRight=6;
            button.AddThemeStyleboxOverride("normal",style);
        }
        gameMenuDock.AddThemeFontSizeOverride("font_size",11);
    }
    private void UpdateGameDock()
    {
        gameMenuDock.Visible=arena.Playing&&!MenuVisible&&!arena.LocalMapVisible&&Size.X>=1200;
        gameMenuDock.Position=new(18,Size.Y-64);gameMenuDock.Size=new(154,38);
    }
    private bool HandleFieldPanelInput(InputEvent input)
    {
        if(capture is not null||input.IsEcho())return false;
        var sheet=input.IsActionPressed("character")?"character":input.IsActionPressed("inventory")?"inventory":null;
        if(sheet is null)return false;
        if(arena.Playing)OpenFieldPanel(sheet);else if(!arena.PlayerState.Dead){if(sheet=="character")CharacterMenu();else Inventory();}
        arena.Controls.ClearBuffer();GetViewport().SetInputAsHandled();return true;
    }
    private void OpenFieldPanel(string sheet)
    {
        if(arena.PlayerState.Dead)return;
        if(MenuVisible&&fieldSheet==sheet){fieldSheet="";arena.TogglePause();return;}
        arena.PauseForInspection();fieldSheet=sheet;
        if(sheet=="character")CombatCharacterSheet();else CombatGearSheet();
    }
    private void CombatCharacterSheet()
    {
        var s=arena.Character!.State;var actor=arena.PlayerState;
        ClearMenu("FIELD / CHARACTER",$"{s.Frame} • Level {CounterText.Short(s.ValidatedLevel)}","A moment to inspect the life you carry.");
        var columns=new HBoxContainer();columns.AddThemeConstantOverride("separation",18);options.AddChild(columns);
        var figure=Section(columns,"The Manyborn");figure.AddChild(new FramePortrait {Frame=s.Frame,CustomMinimumSize=new(220,230)});
        var values=Section(columns,"Combat attributes");
        StatLine(values,"Life",CounterText.Short(actor.Life/1000)+" / "+CounterText.Short(actor.MaximumLife/1000));
        StatLine(values,"Focus",$"{actor.Focus/1000} / {actor.MaximumFocus/1000}");
        StatLine(values,"Armor",EquipmentRules.Stat(s,AffixKind.Armor).ToString());
        StatLine(values,"Resistance",EquipmentRules.Stat(s,AffixKind.Resistance).ToString());
        StatLine(values,"Might",CounterText.Short(s.Might));StatLine(values,"Resolve",CounterText.Short(s.Resolve));
        BodyLabel(values,"Skills, talents and crafting are available when you return to Hearth.",13,muted);
        var grid=CardGrid(3);
        foreach(var id in s.EquippedSkills)ArtButton(grid,RpgTheme.Icon(id),SkillName(id),DescribeSkill(id),()=>{},true,true);
        AddButton("Return to battle",()=>{fieldSheet="";arena.TogglePause();},true);goBack=()=>{fieldSheet="";arena.TogglePause();};
    }
    private void CombatGearSheet()
    {
        var s=arena.Character!.State;
        ClearMenu("FIELD / EQUIPPED ARSENAL","Your equipment",$"{CounterText.Short(s.Gold)} gold • {CounterText.Short(s.Alloy)} Alloy • Attunement {CounterText.Short(s.AttunementGrade)}");
        var grid=CardGrid();
        foreach(var slot in Enum.GetValues<GearSlot>())
            if(s.Equipment.TryGetValue(slot,out var id))
            {
                var item=EquipmentRules.Find(s,id);
                ArtButton(grid,18+(int)slot,item.Name,$"{slot} • {item.BaseKind} +{item.BaseValue}\n"+string.Join(" • ",item.Affixes.Select(a=>a.Kind+" +"+a.Value)),()=>CombatInspectItem(id),true);
            }
        Text("Return to Hearth to change equipment and visit the forge.",13,muted);
        AddButton("Return to battle",()=>{fieldSheet="";arena.TogglePause();},true);goBack=()=>{fieldSheet="";arena.TogglePause();};
    }
    private void CombatInspectItem(Guid id)
    {
        var item=EquipmentRules.Find(arena.Character!.State,id);
        ClearMenu("FIELD / EQUIPMENT",item.Name,"Carried through every life.");
        ItemComparisonCard(options,item,"EQUIPPED");
        AddButton("Back to equipped gear",CombatGearSheet,true);goBack=CombatGearSheet;
    }
}

public partial class Arena
{
    public void PauseForInspection(){ResetPointerTravel();LocalMapVisible=false;Paused=true;Controls.ClearBuffer();Audio.SetPaused(true);}
}
