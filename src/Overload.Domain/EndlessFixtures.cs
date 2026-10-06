using System.Numerics;

namespace Overload.Domain;
/// <summary>Explicit isolated test/practice profiles. Never used to grant progression to a saved character.</summary>
public static class EndlessFixtures
{
    public static CharacterState Reference(BigInteger tier)
    {
        if (tier < 1) throw new ArgumentOutOfRangeException(nameof(tier));
        return FoundationRules.AddXp(new CharacterState { TotalXp=0,ValidatedLevel=1,FractureUnlocked=true,
            HighestClearedTier=tier-1,HighestUnlockedTier=tier,AttunementGrade=BigInteger.Max(1,tier-1),
            Chapter=EndlessRules.ChapterAt(tier),Gold=200*tier*10,Alloy=100*tier*10 }, Progression.TotalXp(Progression.ReferenceLevel(tier)));
    }
}
