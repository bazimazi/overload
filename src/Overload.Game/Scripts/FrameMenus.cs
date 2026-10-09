using Overload.Domain;
using Godot;
namespace Overload.Game;
public partial class ArenaHud
{
    public void CovenantRetry()
    {
        ClearMenu("RED COVENANT MASTERY", "Demonstrate the reservoir", arena.TrialStage==0?"Reserve at least 20% maximum Life before defeating the guardians.":"While hurt, use Flask with an active reservation, then defeat the guardians. Expiry alone cannot heal.");
        AddButton("Retry this encounter",arena.RetrySpecial,true);AddButton("Return to Hearth",arena.ReturnToTitle);goBack=arena.ReturnToTitle;
    }
    public void NewFrameMenu()
    {
        ClearMenu("NEW CHARACTER", "Choose your class", "Begin at level 1 in safe Hearth. Every class can complete the whole adventure.");
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 16); options.AddChild(row);
        foreach(var frame in Enum.GetValues<FrameId>())
        {
            var card=new VBoxContainer { SizeFlagsHorizontal=SizeFlags.ExpandFill };card.AddThemeConstantOverride("separation",10);row.AddChild(card);
            card.AddChild(new FramePortrait {Frame=frame,CustomMinimumSize=new(0,Size.Y<650?80:175)});
            var name=new Label { Text=frame.ToString(),HorizontalAlignment=HorizontalAlignment.Center };name.AddThemeColorOverride("font_color",gold);card.AddChild(name);
            var fantasy=frame switch { FrameId.Warden=>"Melee fighter · close range\nSweep groups and break guards.\nGood first choice.",FrameId.Threadseer=>"Spellcaster · ranged control\nPlace fire and storm zones.\nKeep enemies at a distance.",_=>"Ranged fighter · spirit companion\nFire shards and command echoes.\nMove between volleys." };
            if(Size.Y<650)fantasy=frame switch{FrameId.Warden=>"Melee · close range\nSweep and stagger groups.",FrameId.Threadseer=>"Spellcaster · ranged\nPlace fire and storm zones.",_=>"Ranged · companion\nShoot and command echoes."};
            var description=new Label { Text=fantasy, AutowrapMode=TextServer.AutowrapMode.WordSmart, HorizontalAlignment=HorizontalAlignment.Center };description.AddThemeFontSizeOverride("font_size",FontSize(14));description.AddThemeColorOverride("font_color",muted);card.AddChild(description);
            var button=new Button { Text="Begin "+frame,CustomMinimumSize=new(0,44) };button.AddThemeFontSizeOverride("font_size",FontSize(16));button.Pressed+=()=>{arena.OpenCharacter(true,true,frame);if(arena.Character is not null)arena.StartWorld();else Title();};card.AddChild(button);if(frame==FrameId.Warden)FocusAfterLayout(button);
        }
        AddButton("Continue an existing character",SavedFrames);
        AddButton("Compare accelerated training builds",TrainingFrames);
        AddButton("Back",Title);goBack=Title;
    }
    private void SavedFrames()
    {
        ClearMenu("YOUR JOURNEYS", "Characters", "Choose a saved Frame. Each journey retains its own checkpoint and earned rewards.");
        var first=true;
        foreach(var profile in arena.SavedCharacters())
        {
            var slot=profile.Slot;
            if(profile.State is not { } state){ Text(slot+": "+profile.Notice,14,muted);continue; }
            AddButton($"{state.Frame} / Level {CounterText.Short(state.ValidatedLevel)} / {state.Mode} / {(state.World is {} world?world.ActiveZone.Name:$"Room {state.CheckpointRoom+1}")}",()=>{if(arena.SelectCharacter(slot))Title();else SavedFrames();},first);first=false;
        }
        if(first)Text("No readable saved characters found.",16,muted);
        if(!string.IsNullOrEmpty(arena.SaveProblem))Text(arena.SaveProblem,14,gold);
        AddButton("Choose a new Frame",NewFrameMenu);AddButton("Back",Title,first);goBack=Title;
    }
    private void TrainingFrames()
    {
        ClearMenu("SEPARATE TRAINING CHARACTERS", "Compare builds", "All nine profiles use the same gear and point budgets. These accelerated characters cannot earn a Standard ritual.");
        foreach(var frame in Enum.GetValues<FrameId>())for(var i=0;i<3;i++)
        {
            var build=i;
            AddButton($"{frame} build {i+1}: {string.Join(", ",FrameRules.BuildFixture(frame,build).EquippedSkills)}",()=>{arena.OpenCharacter(true,false,frame,build);Title();},frame==FrameId.Warden&&i==0);
        }
        AddButton("Back",NewFrameMenu);goBack=NewFrameMenu;
    }

}
