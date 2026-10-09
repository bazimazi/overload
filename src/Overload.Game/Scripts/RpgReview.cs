using Godot;
using Overload.Domain;
using System.Numerics;

namespace Overload.Game;

public partial class Arena
{
    private async void ReviewRpg()
    {
        var rendered=OS.GetCmdlineUserArgs().Contains("--rpg-review");var checks=0;
        var directory=System.IO.Path.Combine(ProjectSettings.GlobalizePath("res://"),"../../artifacts/rpg");Directory.CreateDirectory(directory);
        void Check(bool valid,string label){if(!valid)throw new InvalidOperationException(label);checks++;GD.Print("RPG PASS: "+label);}
        async Task Frames(int count){for(var i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);}
        async Task Shot(string name)
        {
            await Frames(16);if(rendered)Hud.CheckQualityLayout();
            if(!rendered)return;
            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            using var image=GetViewport().GetTexture().GetImage();image.SavePng(System.IO.Path.Combine(directory,name+".png"));
        }
        try
        {
            GetWindow().Size=new(1280,720);await Frames(4);
            ReturnToTitle();
            Check(UpdateCharacter(s=>WorldRules.Enroll(FoundationRules.AddXp(FrameRules.Create(FrameId.Warden),Progression.TotalXp(30))) with {CharacterId=s.CharacterId,Gold=5000,Alloy=500}),"isolated level-30 Standard profile");
            ReturnToTitle();await Shot("hearth");
            foreach(var view in new[]{"quickstart","services","arsenal","item","character","skills","talents","memories","loadout","oaths","settings","atlas","frames"})
            {Hud.ShowRpgPanel(view);await Shot(view);}
            Hud.ShowRpgPanel("arsenal");
            Check(Hud.RpgInventoryItemButtons()==6,"six real inventory items have selectable artwork");
            Hud.ShowRpgPanel("talents");Hud.LearnRpgTalent();
            Check(Character!.State.Talents.Contains(FrameRules.Talents(Character.State.Frame)[0]),"native talent node commits the real prerequisite transaction");
            Hud.ShowRpgPanel("skills");Hud.TrainRpgSkill();
            Check(Character.State.SkillRanks.GetValueOrDefault(SkillId.Cleave)==1,"native skill card spends a real skill point");
            ReloadCharacterForCheck();Check(Character.State.Talents.Count==1&&Character.State.SkillRanks.GetValueOrDefault(SkillId.Cleave)==1,"panel choices survive a save reload");
            foreach(var size in rendered?new[]{new Vector2I(960,540),new(1280,720),new(1920,1080)}:new[]{new Vector2I(1280,720)})
            {
                if(rendered){DisplayServer.WindowSetSize(size);await Frames(8);}
                foreach(var text in new[]{100,125})
                {
                    if(Audio.TextPercent!=text)Audio.ToggleText();
                    foreach(var view in new[]{"quickstart","services","arsenal","item","talents","skills","character","frames"})
                    {Hud.ShowRpgPanel(view);await Shot($"{view}-{size.X}-{text}");if(view is "skills" or "frames")Check(Hud.RpgFocusedActionVisible(),$"{view} primary action is visible and usable at {size.X} / {text}% text");}
                }
            }
            if(rendered)DisplayServer.WindowSetSize(new(1280,720));if(Audio.TextPercent!=100)Audio.ToggleText();await Frames(8);
            ReturnToTitle();StartWorld();await Frames(4);OpenRegionalMap();await Shot("world-atlas");CloseWorldMenu();
            LocalMapVisible=true;
            if(rendered){await Frames(4);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);using var image=GetViewport().GetTexture().GetImage();image.SavePng(System.IO.Path.Combine(directory,"local-map.png"));}
            LocalMapVisible=false;ReturnToTitle();StartEncounter(2);
            PlayerState.ChangeMaximumLife(BigInteger.Pow(10,20));PlayerState.Reset();Player.Position=new(300,220);Player.TeleportVisual();
            var original=Player.Position;Controls.Observe(new InputEventMouseMotion {Position=worldContainer.Position+(new Godot.Vector2(185,265)-CameraOrigin)*WorldScale});BeginPointerTravel();
            Check(PointerDestination is not null,"mouse destination routes through the actual navigation");
            await Frames(75);Check(Player.Position.DistanceTo(original)>40,"pointer travel moves the real collision body");
            Input.ActionPress("move_right");await Frames(3);Input.ActionRelease("move_right");Check(PointerDestination is null,"keyboard movement cancels mouse travel");
            Input.ParseInputEvent(new InputEventKey {PhysicalKeycode=Key.C,Keycode=Key.C,Pressed=true});Input.FlushBufferedEvents();await Frames(2);
            Check(Paused&&Hud.MenuVisible,"character shortcut opens an inspection panel and pauses combat");var stopped=PlayerState.Tick;await Frames(12);Check(PlayerState.Tick==stopped,"field inspection stops authoritative combat clocks");await Shot("field-character");
            Input.ParseInputEvent(new InputEventKey {PhysicalKeycode=Key.C,Keycode=Key.C,Pressed=false});Input.FlushBufferedEvents();
            Input.ParseInputEvent(new InputEventKey {PhysicalKeycode=Key.I,Keycode=Key.I,Pressed=true});Input.FlushBufferedEvents();await Frames(2);await Shot("field-arsenal");
            Input.ParseInputEvent(new InputEventKey {PhysicalKeycode=Key.I,Keycode=Key.I,Pressed=false});Input.FlushBufferedEvents();arenaInspectionEnd();
            Paused=true;ArrivalTime=0;PublishPredictions(Godot.Vector2.Right,Godot.Vector2.Zero);
            if(rendered){await Frames(4);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);using var image=GetViewport().GetTexture().GetImage();image.SavePng(System.IO.Path.Combine(directory,"combat.png"));}
            ReturnToTitle();
            if(rendered)
            {
                StartWorld();
                foreach(var region in Enum.GetValues<Region>())
                {
                    WorldTransaction("rpg.scenery:"+Guid.NewGuid(),s=>WorldRules.Enter(s,WorldContent.Id(region,0),new(3780,870)));EnterWorldZone(true);
                    PlayerState.ChangeMaximumLife(BigInteger.Pow(10,20));PlayerState.Reset();await Frames(15);Paused=true;ArrivalTime=0;Hud.HideMenu();
                    await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);using var image=GetViewport().GetTexture().GetImage();image.SavePng(System.IO.Path.Combine(directory,"world-"+region+".png"));
                    Paused=false;
                }
            }
            GD.Print($"OVERLOAD_RPG_OK {checks} checks; rendered={rendered}; isolated profiles");QuitGame();
        }
        catch(Exception error){GD.PushError("RPG REVIEW: "+error);QuitGame(1);}
    }
    private void arenaInspectionEnd(){Paused=false;Audio.SetPaused(false);Hud.HideMenu();Controls.ClearBuffer();}
}

public partial class ArenaHud
{
    public void ShowRpgPanel(string name)
    {
        switch(name)
        {
            case "arsenal":Inventory();break;case "item":InspectItem(arena.Character!.State.Inventory[0].Id);break;
            case "character":CharacterMenu();break;case "skills":SkillsMenu();break;case "talents":TalentsMenu();break;
            case "memories":Bindings();break;case "loadout":LoadoutMenu();break;case "oaths":Oaths();break;
            case "settings":Settings();break;case "atlas":Fractures();break;case "frames":NewFrameMenu();break;
            case "quickstart":QuickStart();break;case "services":TownServices();break;
        }
    }
    private IEnumerable<Node> PanelDescendants(Node node)
    {foreach(var child in node.GetChildren()){yield return child;foreach(var descendant in PanelDescendants(child))yield return descendant;}}
    public int RpgInventoryItemButtons()=>PanelDescendants(options).OfType<Button>().Count(b=>b.TooltipText.Contains("Quality",StringComparison.Ordinal));
    public bool RpgFocusedActionVisible()
    {
        var focus=GetViewport().GuiGetFocusOwner();
        var valid=focus is Button {Disabled:false}&&GetGlobalRect().Encloses(focus.GetGlobalRect())&&scroll.GetGlobalRect().Encloses(focus.GetGlobalRect());
        if(!valid)GD.Print($"RPG FOCUS: control={focus?.Name} rect={focus?.GetGlobalRect()} viewport={GetGlobalRect()} scroll={scroll.GetGlobalRect()} offset={scroll.ScrollVertical}");
        return valid;
    }
    public void LearnRpgTalent()=>PanelDescendants(options).OfType<Button>().First(b=>!b.Disabled&&b.TooltipText.Contains("+5% damage",StringComparison.Ordinal)).EmitSignal(Button.SignalName.Pressed);
    public void TrainRpgSkill()=>PanelDescendants(options).OfType<Button>().First(b=>!b.Disabled&&b.Text.StartsWith("Train",StringComparison.Ordinal)).EmitSignal(Button.SignalName.Pressed);
}
