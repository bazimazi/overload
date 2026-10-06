using Overload.Domain;
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
        ClearMenu("NEW CHARACTER", "Choose a Frame", "Each character has its own progression, equipment and earned oaths.");
        foreach(var frame in Enum.GetValues<FrameId>())
        {
            Text(frame+": "+string.Join(", ",FrameRules.Skills(frame).Take(4)),15,gold);
            AddButton("Begin Standard "+frame,()=>{arena.OpenCharacter(true,true,frame);Title();},frame==FrameId.Warden);

        }
        AddButton("Compare accelerated training builds",TrainingFrames);
        AddButton("Back",Title);goBack=Title;
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
