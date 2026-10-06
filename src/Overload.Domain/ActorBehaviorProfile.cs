using System.Collections.Immutable;

namespace Overload.Domain;

public enum ActionImplementation { Base, Pursuit, Afterstrike, Crossing, Convergence, Shelter, Reprieve, Focused, Shatter, Cascade }

public sealed record PatternDefinition(string Id, ActionFamily Family, ImmutableArray<MemoryType> RequiredMemories,
    ActionCapabilities RequiredCapabilities, int StrainCost, string ImplementationId, int ExtraFocusCost = 0);
public sealed record BindingDefinition(string PatternId, int Priority);
public sealed record OverloadDefinition(int MaximumStrain, int DecayPerSecond, int DecayDelayTicks, int ExitClearTicks,
    int BindingSlots, ImmutableArray<PatternDefinition> Patterns, ImmutableArray<BindingDefinition> Bindings);
public sealed record CompiledBinding(PatternDefinition Pattern, int Priority, ActionImplementation Implementation);

/// <summary>Immutable, bounded dispatch table. Implementation IDs are an authored whitelist, never reflection names.</summary>
public sealed class ActorBehaviorProfile
{
    public ImmutableArray<CompiledBinding> Bindings { get; }
    private ActorBehaviorProfile(ImmutableArray<CompiledBinding> bindings) => Bindings = bindings;

    public static bool TryImplementation(string id, out ActionImplementation implementation)
    {
        implementation = id switch
        {
            "assault.pursuit.v1" => ActionImplementation.Pursuit,
            "assault.afterstrike.v1" => ActionImplementation.Afterstrike,
            "traverse.crossing.v1" => ActionImplementation.Crossing,
            "assault.convergence.v1" => ActionImplementation.Convergence,
            "traverse.shelter.v1" => ActionImplementation.Shelter,
            "recover.reprieve.v1" => ActionImplementation.Reprieve,
            "assault.focused.v1" => ActionImplementation.Focused,
            "assault.shatter.v1" => ActionImplementation.Shatter,
            "assault.cascade.v1" => ActionImplementation.Cascade,
            _ => ActionImplementation.Base
        };
        return implementation != ActionImplementation.Base;
    }

    public static ActorBehaviorProfile Compile(OverloadDefinition definition)
    {
        var errors = Validate(definition);
        if (errors.Count > 0) throw new ArgumentException(string.Join(Environment.NewLine, errors), nameof(definition));
        var patterns = definition.Patterns.ToDictionary(p => p.Id, StringComparer.Ordinal);
        var bindings = definition.Bindings.Select(b =>
        {
            var p = patterns[b.PatternId];
            TryImplementation(p.ImplementationId, out var implementation);
            return new CompiledBinding(p, b.Priority, implementation);
        }).OrderByDescending(b => b.Pattern.RequiredMemories.Length).ThenBy(b => b.Priority)
            .ThenBy(b => b.Pattern.Id, StringComparer.Ordinal).ToImmutableArray();
        return new(bindings);
    }

    public static List<string> Validate(OverloadDefinition? definition)
    {
        List<string> errors = [];
        if (definition is not { } d) { errors.Add("overload: definition required"); return errors; }
        if (d.MaximumStrain != 100 || d.DecayPerSecond is < 1 or > 100 || d.DecayDelayTicks is < 1 or > 3600
            || d.ExitClearTicks is < 1 or > 3600 || d.BindingSlots is < 0 or > 3)
            errors.Add("overload: invalid Strain rules or binding limit");
        if (d.Patterns.IsDefault || d.Bindings.IsDefault) { errors.Add("overload: patterns and bindings required"); return errors; }
        if (d.Patterns.Length > 64 || d.Bindings.Length > d.BindingSlots) errors.Add("overload: content or binding limit exceeded");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in d.Patterns)
        {
            if (p is null) { errors.Add("pattern: null definition"); continue; }
            if (string.IsNullOrWhiteSpace(p.Id) || !ids.Add(p.Id)) errors.Add("pattern: missing or duplicate ID");
            if (!Enum.IsDefined(p.Family) || p.RequiredMemories.IsDefaultOrEmpty || p.RequiredMemories.Length > 2
                || !p.RequiredMemories.IsDefault && (p.RequiredMemories.Any(m => !Enum.IsDefined(m)) || p.RequiredMemories.Distinct().Count() != p.RequiredMemories.Length))
                errors.Add($"{p.Id}: requires one or two distinct supported memories");
            if (p.StrainCost is < 1 or > 100 || p.ExtraFocusCost is < 0 or > 10000) errors.Add($"{p.Id}: invalid cost");
            if (!TryImplementation(p.ImplementationId, out var implementation)) errors.Add($"{p.Id}: unknown implementation ID");
            else
            {
                var family = implementation is ActionImplementation.Crossing or ActionImplementation.Shelter ? ActionFamily.Traverse
                    : implementation == ActionImplementation.Reprieve ? ActionFamily.Recover : ActionFamily.Assault;
                var required = implementation == ActionImplementation.Crossing ? ActionCapabilities.PathTraversal
                    : implementation is ActionImplementation.Shelter or ActionImplementation.Reprieve ? ActionCapabilities.None : ActionCapabilities.DirectDamage | ActionCapabilities.AimOrigin;
                if (implementation == ActionImplementation.Pursuit) required |= ActionCapabilities.GroundAdvance;
                const ActionCapabilities all = ActionCapabilities.DirectDamage | ActionCapabilities.AimOrigin | ActionCapabilities.GroundAdvance | ActionCapabilities.PathTraversal;
                if (p.Family != family || (p.RequiredCapabilities & required) != required || (p.RequiredCapabilities & ~all) != 0)
                    errors.Add($"{p.Id}: incompatible implementation family/capabilities");
            }
        }
        var equipped = new HashSet<string>(StringComparer.Ordinal);
        var signatures = new HashSet<string>(StringComparer.Ordinal);
        foreach (var b in d.Bindings)
        {
            if (b is null || string.IsNullOrWhiteSpace(b.PatternId) || !ids.Contains(b.PatternId)) { errors.Add("binding: unknown pattern"); continue; }
            if (!equipped.Add(b.PatternId) || b.Priority < 0) errors.Add($"{b.PatternId}: duplicate binding or negative priority");
            var p = d.Patterns.First(p => p?.Id == b.PatternId);
            if (p.RequiredMemories.IsDefault) continue;
            if (p.RequiredMemories.Length == 2 && d.BindingSlots < 3) errors.Add($"{b.PatternId}: two-memory bindings require the third slot");
            var signature = $"{p.Family}:{p.RequiredCapabilities}:{string.Join(',', p.RequiredMemories.Order())}";
            if (!signatures.Add(signature)) errors.Add($"{b.PatternId}: indistinguishable signature and capability domain");
        }
        return errors;
    }
}
