namespace Overload.Domain;
public static class CovenantRules
{
    public static CharacterState Unlock(CharacterState s)
    {
        if(s.Mode!=ProfileMode.Standard||!s.ElsewhereOwned||!s.CovenantMastery||s.RedCovenantOwned||s.Seals.Any(v=>v<30))
            throw new InvalidOperationException("Red Covenant requires an earned first oath, its mastery challenge, and 30 Seals from every region");
        return s with { RedCovenantOwned=true,Seals=[..s.Seals.Select(v=>v-30)] };
    }
    public static CharacterState Select(CharacterState s, string? oath) => oath switch
    {
        null=>s with { ElsewhereSelected=false,RedCovenantSelected=false },
        "oath.elsewhere" when s.ElsewhereOwned=>s with { ElsewhereSelected=true,RedCovenantSelected=false },
        "oath.red-covenant" when s.RedCovenantOwned=>s with { ElsewhereSelected=false,RedCovenantSelected=true },
        _=>throw new InvalidOperationException("Oath is not owned")
    };
}
