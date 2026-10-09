using Godot;
using Overload.Domain;

namespace Overload.Game;

public partial class ArenaHud
{
    public void AdventureJournal()
    {
        if(arena.Character?.State.World?.Adventure is not { } a)
        {ClearMenu("ADVENTURE","Quest journal","Start the connected campaign to follow its quests.");AddButton("Back",ReturnFromBuild,true);goBack=ReturnFromBuild;return;}
        var state=arena.Character.State;var quest=AdventureRules.Objective(state);
        var inTown=arena.AdventureActive&&arena.WorldZone.Id=="hearth";
        ClearMenu("QUEST JOURNAL / SAVED PROGRESS",quest?.Title??"Restore the four roads",quest?.Instruction??(arena.AdventureActive?arena.WorldObjective:"Continue your saved adventure from the character menu."));
        var info=Section(options,"Your next step");
        BodyLabel(info,quest?.Hint??"Follow the gold route on your minimap. Activate refuges to save safe travel points. Restore both dungeon conduits, then defeat the region's ruler.",17,ink);
        if(quest is not null)BodyLabel(info,"Reward: "+quest.Reward,14,gold);
        if(quest?.Id=="meet"&&inTown)AddButton("Accept Mara's first patrol quest",arena.AcceptFirstQuest,true);
        foreach(var entry in new[]{("accepted","Speak to Mara"),("road","Clear the first patrol"),("collected","Collect your first weapon"),("equipped","Equip the weapon"),("trained","Train a skill")})
            Text((a.Milestones.Contains(entry.Item1)?"✓  ":"○  ")+entry.Item2,14,a.Milestones.Contains(entry.Item1)?teal:muted);
        Text($"Regions restored: {state.World!.Resolved.Count}/4",16,gold);
        var controls=Section(options,"Survive, collect, grow");
        BodyLabel(controls,"Left click ground to move, an enemy to approach and attack, or a landmark to interact. Hold LMB to keep fighting a nearby pack. Alt + LMB attacks without approaching. WASD and right mouse also move. Hold Q / E / R to repeat skills. Space evades. F heals; Hearth and safe refuges refill flasks.",15,ink);
        BodyLabel(controls,"A landed basic attack restores 8 Focus. Three basic hits charge Surge: your next damaging skill deals 50% more damage. Land another basic hit or use Surge within 6 seconds. Focus also regenerates automatically. Secondary effects do not generate charges.",15,gold);
        BodyLabel(controls,"Red Life globes drop after every third ordinary enemy. Move nearby while hurt to collect 12% Life automatically. They do not refill flasks and disappear when you leave the area.",15,ink);
        BodyLabel(controls,"G interacts and picks up nearby gear. I compares and equips items. K spends skill points. Tab opens your local map; M opens the world map. Gold markers show your route. Amber attack shapes are dangerous; teal marks an opening.",15,ink);
        BodyLabel(controls,"P opens a town portal after 3 seconds standing still. Movement, attacks and damage interrupt it. In Hearth, P returns you to the same place. Forge and salvage services are available in Hearth's inventory.",15,ink);
        if(arena.AdventureActive)
        {
            AddButton(arena.WorldZone.Id=="hearth"?"Return through portal [P]":"Open a town portal [P]",()=>{ReturnFromBuild();arena.BeginTownPortal();},false);
            if(arena.WorldZone.Id=="hearth")
            {
                Text("Trailkeeper contracts · repeatable frontier expeditions",18,gold);
                Text("A caravan takes you to a fresh wilderness. Clear three central patrols and the camp's guards, activate the camp, then return for legendary gear, XP, 100+ gold and 10 Alloy.",15,ink);
                if(a.Contract?.Completed.Count==4)AddButton("Claim legendary contract reward",arena.CollectContractReward,true);
                else if(a.Milestones.Contains("trained"))AddButton(a.Contract is null?"Begin frontier contract":"Resume frontier contract",arena.StartFrontierContract,a.Contract is not null);
                else Text("Finish the first patrol and train a skill to unlock caravan contracts.",14,muted);
                if(a.Contract is not null)AddButton("Abandon this contract · no reward",arena.AbandonFrontierContract);
                Text("Difficulty · change safely in Hearth. Loot and XP remain the same.",15,gold);
                var row=new HBoxContainer();options.AddChild(row);
                foreach(var difficulty in Enum.GetValues<AdventureDifficulty>())PanelButton(row,difficulty+(a.Difficulty==difficulty?" ✓":""),()=>arena.ChooseDifficulty(difficulty),a.Difficulty==difficulty);
                Text("Story: gentler enemies and longer warnings. Adventurer: standard balance. Veteran: tougher enemies.",14,muted);
            }
        }
        if(a.GroundLoot.Length>0)Text($"Uncollected equipment: {a.GroundLoot.Length}. Drops stay on the ground when you travel or reload.",14,gold);
        foreach(var entry in state.Journal.TakeLast(5))Text(entry,12,muted);
        Issue();AddButton(arena.Playing?"Return to adventure":"Back",ReturnFromBuild,quest?.Id!="meet"&&a.Contract?.Completed.Count!=4);goBack=ReturnFromBuild;
    }
    private void DrawAdventureGuidance()
    {
        if(!arena.AdventureActive||MenuVisible)return;
        var quest=arena.AdventureObjective;
        if(arena.HazardUnderfoot)
            CenterWrite(new(Size.X/2,Size.Y-285),"DANGER · LEAVE THE MARKED AREA",12,ember);
        if(quest is not null&&arena.Audio.ShowTutorialHints&&!arena.Enemies.Any(e=>!e.Enemy!.Dead&&e.Enemy.Definition.Role==EnemyRole.Bellkeeper)
            &&!arena.PlayerState.ElsewhereActive&&!arena.PlayerState.RedCovenantActive)
        {
            var width=Math.Min(370,Size.X*.43f);
            Surface(new(18,148,width,74),new Color("645947"),.88f);
            Write(new(30,167),Fit("QUEST · "+quest.Title,width-24,11),11,gold);
            Write(new(30,188),Fit(quest.Hint,width-24,11),11,ink);
            Write(new(30,209),"J  Quest journal · P  Town portal",10,muted);
        }
        if(arena.PortalChanneling)
        {
            var r=new Rect2((Size.X-300)/2,Size.Y-265,300,54);Surface(r,new Color("598d83"),.94f);
            CenterWrite(r.Position+new Vector2(150,20),$"OPENING PORTAL · {(1-arena.PortalProgress)*3:0.0}s",12,teal);
            Bar(new(r.Position+new Vector2(12,32),new(276,5)),arena.PortalProgress,teal);
            CenterWrite(r.Position+new Vector2(150,49),"Stand still · attacks or damage interrupt",10,ink);
        }
        else if(arena.AdventureToastVisible)
        {
            var width=Math.Min(540,Size.X-60);var r=new Rect2((Size.X-width)/2,Size.Y-262,width,58);Surface(r,new Color("876e45"),.94f);
            CenterWrite(r.Position+new Vector2(width/2,23),Fit(arena.AdventureToastTitle,width-24,16),16,gold);
            CenterWrite(r.Position+new Vector2(width/2,45),Fit(arena.AdventureToastDetail,width-24,11),11,ink);
        }
    }
}
