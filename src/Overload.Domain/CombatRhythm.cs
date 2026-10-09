namespace Overload.Domain;

public sealed partial class PlayerCombat
{
    // A hit, not a click or a secondary effect, earns one charge and one Focus receipt.
    private readonly Dictionary<long,SourceKind> eligibleBasics = [];
    private readonly HashSet<long> rewardedBasics = [];
    private readonly Queue<long> basicHistory = [];
    private long surgeExpiresAt;
    public bool CombatRhythmEnabled { get; private set; }
    public int SurgeCharges { get; private set; }
    public int SurgeRemainingTicks => SurgeCharges == 0 ? 0 : (int)Math.Max(0, surgeExpiresAt - Tick);
    public int BasicFocusReceipts { get; private set; }
    public int SurgedActions { get; private set; }
    public System.Numerics.BigInteger RestoreLife(int percent)
    {
        if(Dead||percent is <1 or >20)return 0;
        var before=Life;Life=System.Numerics.BigInteger.Min(MaximumLife,Life+MaximumLife*percent/100);
        if(Life>before){revision++;if(ReservedPermille>0)HealedUnderReservation=true;}
        return Life-before;
    }
    private void ResetCombatRhythm()
    {
        eligibleBasics.Clear(); rewardedBasics.Clear(); basicHistory.Clear();
        SurgeCharges = BasicFocusReceipts = SurgedActions = 0; surgeExpiresAt = 0;
    }
    private void CommitCombatRhythm(ActionExecution execution)
    {
        if (!CombatRhythmEnabled) return;
        if (execution.DamagePercent > 100) { SurgeCharges = 0; surgeExpiresAt = 0; SurgedActions++; }
        if (execution.Definition.Id is not (SkillId.Cleave or SkillId.Needle or SkillId.ShardShot)
            || execution.Source is not (SourceKind.BasePlayerAction or SourceKind.OverloadedPlayerAction)) return;
        eligibleBasics.Add(execution.RootActionId,execution.Source); basicHistory.Enqueue(execution.RootActionId);
        if (basicHistory.Count > 128)
        { var retired = basicHistory.Dequeue(); eligibleBasics.Remove(retired); rewardedBasics.Remove(retired); }
    }
    public bool ObserveBasicHit(EffectProvenance provenance)
    {
        if (!CombatRhythmEnabled || Dead || provenance.EffectId != 0 || provenance.Depth != 0
            || !eligibleBasics.TryGetValue(provenance.RootActionId,out var source) || source!=provenance.Source || !rewardedBasics.Add(provenance.RootActionId)) return false;
        if (!RedCovenantActive) Focus = Math.Min(MaximumFocus, Focus + 8000);
        SurgeCharges = Math.Min(3, SurgeCharges + 1); surgeExpiresAt = Tick + 360;
        BasicFocusReceipts++; revision++; return true;
    }
}
