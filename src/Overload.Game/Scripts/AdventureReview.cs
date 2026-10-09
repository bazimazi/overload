using Godot;
using Overload.Domain;
using System.Text.Json;

namespace Overload.Game;

public partial class Arena
{
    // This fixture fights with ordinary input and normal character/enemy stats. It does not grant invulnerability or kill enemies directly.
    private async void ReviewAdventure()
    {
        var campaign=OS.GetCmdlineUserArgs().Any(a=>a is "--campaign-smoke" or "--campaign-review");
        var rendered=OS.GetCmdlineUserArgs().Any(a=>a is "--adventure-review" or "--campaign-review");var checks=0;var simulationFrames=0;
        var output=System.IO.Path.Combine(ProjectSettings.GlobalizePath("res://"),campaign?"../../artifacts/campaign":"../../artifacts/adventure");Directory.CreateDirectory(output);
        var battles=new List<object>();
        void Check(bool value,string label){if(!value)throw new InvalidOperationException(label);checks++;GD.Print("ADVENTURE PASS: "+label);}
        async Task Frames(int n){for(var i=0;i<n;i++){await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);if(Playing&&!Paused&&!LocalMapVisible)simulationFrames++;}}
        void KeyEvent(Key key,bool down){Input.ParseInputEvent(new InputEventKey{Pressed=down,Keycode=key,PhysicalKeycode=key});Input.FlushBufferedEvents();}
        void Aim(Vector2 target)=>Controls.Observe(new InputEventMouseMotion {Position=worldContainer.Position+(target-CameraOrigin)*WorldScale});
        void Click(Vector2 target,bool down)
        {Aim(target);Input.ParseInputEvent(new InputEventMouseButton{Pressed=down,ButtonIndex=MouseButton.Left,Position=Controls.PointerPosition});Input.FlushBufferedEvents();}
        void Move(Vector2 direction)
        {
            Input.ActionPress("move_right",Math.Max(0,direction.X));Input.ActionPress("move_left",Math.Max(0,-direction.X));
            Input.ActionPress("move_down",Math.Max(0,direction.Y));Input.ActionPress("move_up",Math.Max(0,-direction.Y));
        }
        void Release(){Controls.Disconnected();ResetPointerTravel();}
        IEnumerable<Button> Buttons(Node node)
        {foreach(var child in node.GetChildren()){if(child is Button button)yield return button;foreach(var nested in Buttons(child))yield return nested;}}
        void Press(string text)
        {var button=Buttons(Hud).FirstOrDefault(b=>b.IsVisibleInTree()&&!b.Disabled&&b.AccessibilityName.Contains(text,StringComparison.Ordinal));Check(button is not null,"visible usable button: "+text);button!.EmitSignal(Button.SignalName.Pressed);}
        async Task Shot(string name)
        {
            await Frames(6);if(!rendered)return;
            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            using var image=GetViewport().GetTexture().GetImage();image.SavePng(System.IO.Path.Combine(output,name+".png"));
        }
        async Task Fight(string label)
        {
            var boss=Enemies.Any(e=>!e.Enemy!.Dead&&EngagedEnemy(e)&&e.Enemy.Definition.Role==EnemyRole.Bellkeeper);
            if(boss)label="boss";
            ResetPointerTravel();var start=PlayerState.Tick;var hits=Effects.HitsTaken;var dealt=Effects.HitsDealt;var startingFlasks=PlayerState.FlaskCharges;var evades=0;var flaskInputs=0;var skills=0;var nextSkill=start;
            var novas=Effects.Deathbursts;var volleys=Effects.SplitVolleys;var basics=PlayerState.BasicFocusReceipts;var surges=PlayerState.SurgedActions;var globes=Effects.GlobesCollected;
            var frame=Character!.State.Frame;
            for(var guard=0;guard<3600&&!PlayerState.Dead;guard++)
            {
                var target=Enemies.Where(e=>!e.Enemy!.Dead&&EngagedEnemy(e)).OrderBy(e=>Player.Position.DistanceSquaredTo(e.Position)).FirstOrDefault();
                if(target is null)break;
                var distance=Player.Position.DistanceTo(target.Position);var direction=Player.Position.DirectionTo(target.Position);
                var reach=Balance.Skills.Single(s=>s.Id==FrameRules.Basic(frame)).Reach;
                var wanted=frame==FrameId.Warden?Math.Min(34,reach*.7f):Math.Min(155,reach*.7f);
                var danger=Enemies.Any(e=>!e.Enemy!.Dead&&e.AttackAge>=0&&e.AttackAge<e.TellTicks&&Player.Position.DistanceTo(e.Position)<e.Enemy.Definition.Reach+28);
                var movement=distance>wanted?direction:Vector2.Zero;
                if(movement.LengthSquared()>0&&!WorldNavigation.Clear(new(Player.Position.X,Player.Position.Y),new(target.Position.X,target.Position.Y),Player.Radius+1))
                {
                    var route=WorldNavigation.FindPath(new(Player.Position.X,Player.Position.Y),new(target.Position.X,target.Position.Y),Player.Radius+1);
                    var bend=route.FirstOrDefault(p=>System.Numerics.Vector2.DistanceSquared(p,new(Player.Position.X,Player.Position.Y))>16);
                    if(bend!=default)movement=Player.Position.DirectionTo(new(bend.X,bend.Y));
                }
                var floor=DangerAreas.Select(r=>r.Grow(Player.Radius+10)).FirstOrDefault(r=>r.HasPoint(Player.Position));
                if(floor.Size.X>0)
                {
                    var p=Player.Position;
                    var escapes=new[]{new Vector2(floor.Position.X-5,p.Y),new Vector2(floor.End.X+5,p.Y),new Vector2(p.X,floor.Position.Y-5),new Vector2(p.X,floor.End.Y+5)};
                    var safe=escapes.Where(q=>WorldNavigation.Clear(new(p.X,p.Y),new(q.X,q.Y),Player.Radius)).OrderBy(q=>q.DistanceSquaredTo(p)).FirstOrDefault();
                    if(safe!=Vector2.Zero)movement=p.DirectionTo(safe);
                }
                else if(target.Volley&&target.AttackAge>=0&&target.AttackAge<target.TellTicks+18)movement=direction.Orthogonal();
                if(danger&&PlayerState.Cooldown(SkillId.Traverse)==0)
                {if(floor.Size.X<=0)movement=target.Volley?(direction*.72f+direction.Orthogonal()*.7f).Normalized():-direction;Move(movement);Controls.Observe(new InputEventKey {Pressed=true,PhysicalKeycode=Key.Space,Keycode=Key.Space});evades++;}
                else if(danger&&frame!=FrameId.Warden)movement=-direction;
                Move(movement);Aim(target.Position);
                if(floor.Size.X>0){Input.ActionRelease("cleave");Input.ActionPress("preserve");}else{Input.ActionPress("cleave");Input.ActionRelease("preserve");}
                if(PlayerState.Life*2<PlayerState.MaximumLife&&PlayerState.FlaskCharges>0&&PlayerState.Cooldown(SkillId.Flask)==0)
                {Controls.Observe(new InputEventKey {Pressed=true,PhysicalKeycode=Key.F,Keycode=Key.F});flaskInputs++;}
                else if(PlayerState.Tick>=nextSkill&&floor.Size.X<=0)
                {
                    for(var choice=0;choice<3;choice++)
                    {
                        var slot=(skills+choice)%3;var id=Character.State.EquippedSkills[slot];var skill=Balance.Skills.Single(s=>s.Id==id);
                        if(PlayerState.Cooldown(id)>0||PlayerState.Focus<skill.FocusCost*1000)continue;
                        if(skill.Damage>0&&distance>skill.Reach+skill.EffectRadius+12||skill.BarrierPercent>0&&!danger&&PlayerState.Life*100>PlayerState.MaximumLife*85)continue;
                        var key=new[]{Key.Q,Key.E,Key.R}[slot];Controls.Observe(new InputEventKey {Pressed=true,PhysicalKeycode=key,Keycode=key});nextSkill=PlayerState.Tick+18;skills++;break;
                    }
                }
                if(rendered&&guard==35)await Shot(frame+"-"+label+"-combat");
                await Frames(1);
            }
            Release();await Frames(5);
            GD.Print($"ADVENTURE FIGHT RESULT: {frame} {label} ticks={PlayerState.Tick-start} hits={Effects.HitsTaken-hits} flasksUsed={startingFlasks-PlayerState.FlaskCharges} skillInputs={skills} life={PlayerState.Life} player={Player.Position} remaining={string.Join(",",Enemies.Where(e=>!e.Enemy!.Dead).Select(e=>$"{e.Enemy!.Definition.Role}@{e.Position}:{e.Enemy.Life}"))}");
            Check(!PlayerState.Dead,frame+" survives "+label+" with normal stats");
            Check(!Enemies.Any(e=>!e.Enemy!.Dead&&EngagedEnemy(e)),frame+" defeats "+label+" through normal actions");
            var seconds=(PlayerState.Tick-start)/60f;
            battles.Add(new{frame=frame.ToString(),label,boss,seconds,basicFocusReceipts=PlayerState.BasicFocusReceipts-basics,surgedActions=PlayerState.SurgedActions-surges,healthGlobes=Effects.GlobesCollected-globes,deathbursts=Effects.Deathbursts-novas,splitVolleys=Effects.SplitVolleys-volleys,hitsTaken=Effects.HitsTaken-hits,hitsDealt=Effects.HitsDealt-dealt,evadeInputs=evades,flasks=startingFlasks-PlayerState.FlaskCharges,skillInputs=skills,
                lifeRemaining=CombatMath.BarBasisPoints(PlayerState.Life,PlayerState.MaximumLife)/100f});
            GD.Print($"ADVENTURE COMBAT: {frame} {label} {seconds:0.0}s hits={Effects.HitsTaken-hits}");
        }
        async Task Walk(WorldPoint destination)
        {
            Release();Check(RequestWorldWalk(V(destination)),"normal click route accepted");
            for(var guard=0;guard<6000&&Player.Position.DistanceTo(V(destination))>2;guard++)
            {
                if(HasEngagedEnemies)
                {
                    await Fight("roadside");
                    if(Paused&&Character!.State.World!.Resolved.Count==4&&Character.State.World.Ending is null){Press("Preserve contradictions");await Frames(3);}
                    Check(RequestWorldWalk(V(destination)),"route resumes after fighting");
                }
                await Frames(1);
                if(PlayerState.Dead)throw new InvalidOperationException("Travel died");
            }
            Release();await Frames(3);Check(Player.Position.DistanceTo(V(destination))<=2,"normal movement reaches destination");
        }
        async Task Exit(string id)
        {var link=WorldZone.Exits.Single(e=>e.Id==id);await Walk(link.Position);InteractWorld();await Frames(4);Check(WorldZone.Id==link.Destination,"road enters "+link.Destination);}
        async Task ImproveBuild()
        {
            foreach(var drop in NearbyLoot.ToArray())
            {await Walk(drop.Position);Check(TryPickUpNearby(),"campaign equipment collected through normal pickup");}
            PauseForInspection();
            foreach(var item in Character!.State.Inventory.ToArray())
            {
                var worn=EquipmentRules.Find(Character.State,Character.State.Equipment[item.Slot]);
                if(item.Id==worn.Id||item.BaseValue<worn.BaseValue&&!(item.Power is not null&&worn.Power is null))continue;
                Hud.InspectItem(item.Id);Press("Equip item");await Frames(1);
            }
            for(var point=0;point<40&&FoundationRules.SkillSpent(Character.State)<FoundationRules.SkillBudget(Character.State);point++)
            {
                var id=FrameRules.Skills(Character.State.Frame).FirstOrDefault(k=>Character.State.SkillRanks.GetValueOrDefault(k)<3);
                Check(UpdateCharacter(s=>FoundationRules.Rank(s,id)),"earned campaign skill point is spent normally");
            }
            foreach(var talent in FrameRules.Talents(Character.State.Frame))
                if(Character.State.Talents.Count<FoundationRules.TalentBudget(Character.State)&&!talent.EndsWith(".4",StringComparison.Ordinal)&&!Character.State.Talents.Contains(talent))
                    Check(UpdateCharacter(s=>FoundationRules.Talent(s,talent)),"earned campaign talent is learned normally");
            CloseWorldMenu();Release();await Frames(4);
        }
        async Task CompleteCampaign()
        {
            await ImproveBuild();
            for(var region=0;region<4;region++)
            {
                if(region>0)
                {
                    await Frames(40);BeginTownPortal();await Frames(185);Check(WorldZone.Id=="hearth","campaign returns to town between regions");
                    PauseForInspection();Check(UpdateCharacter(EquipmentRules.SalvageLowRarity),"town salvage frees spare low-rarity gear");CloseWorldMenu();
                    await Exit("road."+region);await Exit("enforcer");await Walk(WorldZone.Encounters[0].Position);
                    Check(WorldClaims.Contains(WorldContent.Id((Region)region,1)+".boss"),"regional enforcer falls through ordinary combat");await ImproveBuild();
                }
                await Exit("return");await Exit("dungeon");
                foreach(var device in WorldZone.Sites.Where(s=>s.Kind=="device").ToArray())
                {await Walk(device.Position);InteractWorld();await Frames(4);Check(WorldClaims.Contains(device.Id),"conduit restores after its guards are defeated");}
                await ImproveBuild();await Walk(WorldZone.Sites.Single(s=>s.Kind=="waypoint").Position);InteractWorld();await Frames(4);
                await Exit("ruler");await Walk(WorldZone.Encounters[0].Position);
                Check(Character!.State.World!.Resolved.Contains((Region)region),"region restored through a normal ruler fight");
                await Shot("region-"+region+"-restored");await ImproveBuild();
            }
            Check(Character!.State.World!.Ending=="preserve"&&Character.State.FractureUnlocked,"four regions and an ending unlock the real endgame");
            await Frames(45);BeginTownPortal();await Frames(185);Check(WorldZone.Id=="hearth","completed campaign can return to town");
            PauseForInspection();Hud.AdventureJournal();await Shot("campaign-complete-journal");CloseWorldMenu();
        }
        try
        {
            GetWindow().Size=new(1280,720);if(rendered)DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);await Frames(4);
            foreach(var frame in campaign?new[]{FrameId.Warden}:Enum.GetValues<FrameId>())
            {
                Release();ReturnToTitle();Check(UpdateCharacter(s=>WorldRules.Enroll(FrameRules.Create(frame)) with {CharacterId=s.CharacterId}),"fresh standard "+frame);
                StartWorld();await Frames(8);Check(AdventureObjective?.Id=="meet"&&WorldDestination?.Id=="mara","first objective routes to the quest giver");
                if(frame==FrameId.Warden)await Shot("01-hearth-quest");
                if(campaign)
                {
                    var before=Player.Position;Click(new(540,470),true);await Frames(1);Click(new(540,470),false);
                    Check(PointerDestination is not null&&PlayerState.Action is null,"left clicking clear ground moves without swinging a weapon");
                    await Frames(70);Check(Player.Position.DistanceTo(before)>80,"left click moves the real collision body");
                    Click(new(340,330),true);await Frames(1);Click(new(340,330),false);
                    for(var wait=0;wait<180&&!Paused;wait++)await Frames(1);
                    Check(Paused&&Hud.MenuVisible,"clicking the quest giver approaches and opens her dialogue");
                }
                else {await Walk(new(340,360));InteractWorld();await Frames(4);Check(Paused&&Hud.MenuVisible,"talking opens quest dialogue");}
                if(frame==FrameId.Warden)await Shot("02-mara-quest");Press("Accept quest");await Frames(3);
                Check(AdventureObjective?.Id=="road"&&!Paused,"accepting quest returns to play");
                await Exit("road.0");
                if(campaign)
                {
                    await Walk(new(900,1680));var target=worldPacks["ash.0.pack.0"].OrderBy(e=>Player.Position.DistanceSquaredTo(e.Position)).First();var hits=Effects.HitsDealt;
                    Check(GetViewport().GetVisibleRect().HasPoint(WorldToWindow(target.Position-new Vector2(0,25))),"mouse target is actually visible inside the viewport");
                    Click(target.Position-new Vector2(0,25),true);await Frames(1);Click(target.Position-new Vector2(0,25),false);
                    Check(PrimaryTargeting,"clicking an enemy selects a real combat target");
                    Input.ActionPress("stand_ground");await Frames(1);Check(!PrimaryTargeting&&PointerDestination is null,"stand-ground cancels an existing approach order");Input.ActionRelease("stand_ground");
                    Click(target.Position-new Vector2(0,25),true);await Frames(1);Click(target.Position-new Vector2(0,25),false);
                    for(var wait=0;wait<180&&Effects.HitsDealt==hits;wait++)await Frames(1);
                    Check(Effects.HitsDealt>hits,"a single enemy click approaches and lands an attack");
                    Input.ActionPress("preserve");
                    var pack=worldPacks["ash.0.pack.0"].ToArray();
                    target=pack.Where(e=>!e.Enemy!.Dead).OrderBy(e=>Player.Position.DistanceSquaredTo(e.Position)).First();
                    Click(target.Position-new Vector2(0,25),true);
                    for(var wait=0;wait<480&&PlayerState.SurgeCharges<3&&!PlayerState.Dead;wait++){Aim(target.Position-new Vector2(0,25));await Frames(1);}
                    Check(PlayerState.SurgeCharges==3&&PlayerState.BasicFocusReceipts>=3,"normal basic attacks build a full Surge and Focus receipts");
                    await Shot("12-surge-ready");
                    for(var wait=0;wait<480&&pack.Count(e=>e.Enemy!.Dead)<2&&!PlayerState.Dead;wait++){Aim(target.Position-new Vector2(0,25));await Frames(1);}
                    Check(pack.Count(e=>e.Enemy!.Dead)>=2&&!PlayerState.Dead,"one held mouse order clears successive enemies with normal stats");
                    Click(target.Position-new Vector2(0,25),false);
                    var paidTarget=pack.FirstOrDefault(e=>!e.Enemy!.Dead);
                    if(paidTarget is not null)Aim(paidTarget.Position);
                    var surged=PlayerState.SurgedActions;KeyEvent(Key.Q,true);
                    for(var wait=0;wait<80&&PlayerState.SurgedActions==surged;wait++)await Frames(1);
                    KeyEvent(Key.Q,false);Input.ActionRelease("preserve");
                    Check(PlayerState.SurgedActions==surged+1,"a held damaging skill spends the earned Surge once");
                    await Shot("13-surged-skill");Release();
                }
                await Walk(new(1200,1680));await Frames(5);
                if(HasEngagedEnemies)await Fight("first-patrol");
                Check(WorldClaims.Contains("ash.0.pack.0"),frame+" first patrol reward claimed");
                Check(Character!.State.ValidatedLevel>=2&&AdventureObjective?.Id=="loot","first battle reveals loot and a skill point");
                if(frame==FrameId.Warden)await Shot("03-patrol-loot");
                if(frame==FrameId.Threadseer)
                {
                    await Walk(new(1060,1680));Aim(new(1200,1630));
                    Input.ParseInputEvent(new InputEventMouseButton {ButtonIndex=MouseButton.Left,Pressed=true,Position=Controls.PointerPosition});Input.FlushBufferedEvents();await Frames(1);
                    Check(PointerDestination is not null,"clicking a loot label starts normal movement toward it");
                    Check(PlayerState.Action is null,"loot click does not trigger an unwanted basic attack");
                    Input.ParseInputEvent(new InputEventMouseButton {ButtonIndex=MouseButton.Left,Pressed=false,Position=Controls.PointerPosition});Input.FlushBufferedEvents();await Frames(120);
                    Check(Character.State.Inventory.Any(i=>i.Name=="Mara's Weapon"),"click travel collects the selected item when close enough");Release();
                }
                else Check(TryPickUpNearby(),"real nearby pickup succeeds");var item=Character.State.Inventory.Single(i=>i.Name=="Mara's Weapon");
                Check(AdventureObjective?.Id=="equip","pickup advances the objective");
                KeyEvent(Key.I,true);await Frames(3);KeyEvent(Key.I,false);Check(Paused&&Hud.MenuVisible,"I opens full field inventory");Hud.InspectItem(item.Id);await Frames(4);
                if(frame==FrameId.Warden)await Shot("04-compare-upgrade");
                var resources=(PlayerState.Life,PlayerState.Focus,PlayerState.FlaskCharges,PlayerState.Tick);
                Press("Equip item");await Frames(3);Check(Character.State.Equipment[GearSlot.Weapon]==item.Id,"field equip updates saved equipment");
                Check(resources==(PlayerState.Life,PlayerState.Focus,PlayerState.FlaskCharges,PlayerState.Tick),"equipping preserves resources and paused combat clock");
                Check(AdventureObjective?.Id=="train","equipping guides the player to skills");
                KeyEvent(Key.K,true);await Frames(3);KeyEvent(Key.K,false);Press("Train");await Frames(3);Check(AdventureObjective?.Id=="enforcer","spending a point completes the beginner quest");
                if(frame==FrameId.Warden)await Shot("05-train-skill");CloseWorldMenu();Release();await Frames(30);
                var heldId=EquippedAction(SkillId.ChainLance);var repeats=new HashSet<long>();Aim(Player.Position+Vector2.Up*160);KeyEvent(Key.E,true);
                for(var wait=0;wait<600&&repeats.Count<2;wait++)
                {await Frames(1);if(PlayerState.Action is {} repeated&&repeated.Definition.Id==heldId)repeats.Add(repeated.RootActionId);}
                KeyEvent(Key.E,false);Check(repeats.Count==2,"holding an equipped skill repeats after its real cooldown");Release();
                for(var wait=0;wait<120&&PlayerState.Action is not null;wait++)await Frames(1);
                await Frames(3);
                // Portal interruption uses real movement. Completion and return use the ordinary 180-tick channel.
                BeginTownPortal();Check(PortalChanneling,"P starts a stationary portal channel");Move(Vector2.Down);await Frames(5);Move(Vector2.Zero);await Frames(3);Check(!PortalChanneling&&WorldZone.Id=="ash.0","movement cancels the portal");
                var returnPoint=Player.Position;BeginTownPortal();await Frames(185);Check(WorldZone.Id=="hearth"&&Character.State.World!.Adventure!.ReturnPortal is not null,"completed portal enters Hearth with a saved return point");
                BeginTownPortal();await Frames(5);Check(WorldZone.Id=="ash.0"&&Player.Position.DistanceTo(returnPoint)<2,"return portal restores the same world position");
                Check(Character.State.Equipment[GearSlot.Weapon]==item.Id,"portal preserves the chosen build");
                if(!rendered||frame==FrameId.Warden)
                {await Exit("enforcer");await Walk(WorldZone.Encounters[0].Position);if(HasEngagedEnemies)await Fight("enforcer");Check(WorldClaims.Contains("ash.1.boss"),frame+" boss killed by normal combat");if(frame==FrameId.Warden)await Shot("06-boss-reward");}
                if(!campaign&&!rendered&&frame==FrameId.Threadseer)
                {
                    await ImproveBuild();await Exit("return");await Walk(WorldZone.Encounters.Single(e=>e.Id=="ash.0.pack.1").Position);
                    Check(Effects.SplitVolleys>0,"earned Split Volley changes actual projectile attacks in combat");
                }
                if(frame==FrameId.Warden)
                {
                    if(campaign)await CompleteCampaign();
                    if(WorldZone.Id!="hearth"){await Frames(50);BeginTownPortal();await Frames(185);}Check(WorldZone.Id=="hearth","return to town after the boss");
                    PauseForInspection();Hud.AdventureJournal();Press("Begin frontier contract");await Frames(5);
                    Check(WorldZone.Id=="frontier.0"&&WorldDestination?.Id=="frontier.0.pack.7","contract begins at a fresh reach with a clear patrol route");
                    foreach(var target in AdventureRules.ContractTargets(0).Take(3))await Walk(WorldZone.Encounters.Single(e=>e.Id==target).Position);
                    await Walk(WorldZone.Sites.Single(s=>s.Kind=="waypoint").Position);InteractWorld();await Frames(5);
                    Check(Character.State.World!.Adventure!.Contract!.Completed.Count==4,"ordinary combat and camp interaction complete the contract");
                    await Frames(45);BeginTownPortal();await Frames(185);Check(WorldZone.Id=="hearth","completed survey returns to the quest giver");
                    PauseForInspection();Hud.AdventureJournal();var count=Character.State.Inventory.Length;Press("Claim legendary contract reward");await Frames(4);
                    var reward=Character.State.Inventory[^1];Check(reward.Band==5&&reward.Power is not null&&Character.State.Inventory.Length==count+1,"contract pays a usable legendary with a named power");
                    Hud.InspectItem(reward.Id);await Frames(4);if(rendered)await Shot("08-legendary-contract-reward");Press("Equip item");await Frames(3);
                    Check(Character.State.Equipment[reward.Slot]==reward.Id,"legendary reward equips in town");
                    Hud.AdventureJournal();Press("Begin frontier contract");await Frames(5);Check(WorldZone.Id=="frontier.1"&&Character.State.World!.Adventure!.Contract!.Completed.Count==0,"the next contract opens new terrain and fresh objectives");
                    if(rendered)await Shot("09-frontier-contract");
                }
                Release();ReturnToTitle();ReloadCharacterForCheck();Check(Character.State.World!.Adventure!.Milestones.Contains("trained"),"quest and build survive profile reload");
            }
            // Small-window review checks actual clickable menus with large text.
            StartWorld();PauseForInspection();Hud.AdventureJournal();GetWindow().Size=new(960,540);await Frames(10);await Shot("07-journal-960");
            Check(Buttons(Hud).Any(b=>b.IsVisibleInTree()&&b.AccessibilityName=="Return to adventure"),"small-window journal has an accessible return action");
            var pausedTick=PlayerState.Tick;await Frames(12);Check(PlayerState.Tick==pausedTick,"journal keeps the combat clock paused");
            Audio.ToggleText();Hud.InspectItem(Character!.State.Inventory[0].Id);await Frames(10);await Shot("10-compare-960-large-text");
            Check(Buttons(Hud).Any(b=>b.IsVisibleInTree()&&b.AccessibilityName=="Back to inventory"&&b.GetGlobalRect().Position.Y<Hud.Size.Y-40),"large-text comparison keeps its primary navigation visible");
            Hud.InspectItem(Character.State.Equipment[GearSlot.Weapon]);await Frames(10);await Shot("11-worn-compare-960-large-text");
            var back=Buttons(Hud).Single(b=>b.IsVisibleInTree()&&b.AccessibilityName=="Back to inventory");var rect=back.GetGlobalRect();var visible=Hud.GetGlobalRect().Encloses(rect);
            for(var parent=back.GetParent();parent is not null;parent=parent.GetParent())if(parent is Control {ClipContents:true} clip&&!clip.GetGlobalRect().Encloses(rect))visible=false;
            Check(visible,"equipped item comparison keeps its return action inside every clipped viewport");Audio.ToggleText();
            CloseWorldMenu();GetWindow().Size=new(1280,720);Release();ReturnToTitle();
            File.WriteAllText(System.IO.Path.Combine(output,rendered?"review.json":"smoke.json"),JsonSerializer.Serialize(new{checks,rendered,build=BuildId,campaign,normalCombat=true,simulatedSeconds=simulationFrames/60f,battles},new JsonSerializerOptions{WriteIndented=true}));
            GD.Print($"OVERLOAD_{(campaign?"CAMPAIGN":"ADVENTURE")}_OK checks={checks} rendered={rendered}");QuitGame();
        }
        catch(Exception e){Release();GD.PushError("ADVENTURE REVIEW: "+e);QuitGame(1);}
    }
}
