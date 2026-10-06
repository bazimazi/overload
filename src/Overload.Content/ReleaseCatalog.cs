using System.Collections.Immutable;
using System.Text.Json;
using Overload.Domain;

namespace Overload.Content;
public sealed record ReleaseEntry(string Id,string Category,string Name,string DescriptionKey,ImmutableArray<string> References,ImmutableArray<string> Prerequisites,string? Animation=null);
public sealed record ReleaseCatalog(int SchemaVersion,string Version,ImmutableArray<ReleaseEntry> Definitions,ImmutableDictionary<string,string> Text,ImmutableArray<string> Animations)
{ public ImmutableArray<WorldRule> WorldRules { get; init; }=[]; }

public static class ReleaseContent
{
    public static readonly ImmutableArray<string> Categories=["Frame","Skill","Technique","TalentNode","Inscription","Pattern","Override","Enemy","Attack","Encounter","Room","ItemBase","Affix","LootTable","Quest","Reward","ProgressionCurve","WorldRule","BossMutation","ChapterRoute","Activity","Region","Trial","Chain","SovereignEcho"];
    public static ReleaseCatalog Load(string json)
    {
        var catalog=JsonSerializer.Deserialize<ReleaseCatalog>(json,new JsonSerializerOptions { PropertyNameCaseInsensitive=true,UnmappedMemberHandling=System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow }) ?? throw new InvalidDataException("release: empty catalog");
        var errors=Validate(catalog); if(errors.Count>0)throw new InvalidDataException(string.Join("\n",errors));WorldLaws.Configure(catalog.WorldRules);return catalog;
    }
    public static List<string> Validate(ReleaseCatalog c)
    {
        List<string> errors=[];
        if(c.SchemaVersion!=1 || c.Version is not ("release.foundation.v1" or "release.expansion.v1"))errors.Add("release.schemaVersion/version: unsupported");
        if(c.WorldRules.IsDefault || c.WorldRules.Length!=6 || c.WorldRules.Select(r=>r.Id).Distinct().Count()!=6 || c.WorldRules.Any(r=>!WorldLaws.Rules.Any(t=>t.Id==r.Id)||r.WarningTicks<60||r.WarningTicks>600||r.Period<r.WarningTicks+30||r.Period>3600||r.DamagePercent is <0 or >20))errors.Add("release.worldRules: six known rules with safe warning/period/damage budgets required");
        if(c.Definitions.IsDefaultOrEmpty || c.Text is null || c.Animations.IsDefault) { errors.Add("release: definitions, text and animations required");return errors; }
        var ids=new HashSet<string>();
        foreach(var (d,index) in c.Definitions.Select((d,i)=>(d,i)))
        {
            var path=$"release.definitions[{index}] ({d?.Id})";
            if(d is null) { errors.Add(path+": null entry");continue; }
            if(string.IsNullOrWhiteSpace(d.Id) || !d.Id.Contains('.') || !ids.Add(d.Id))errors.Add(path+".id: missing, unnamespaced or duplicate");
            if(!Categories.Contains(d.Category))errors.Add(path+".category: unknown");
            if(string.IsNullOrWhiteSpace(d.Name) || string.IsNullOrWhiteSpace(d.DescriptionKey) || !c.Text.ContainsKey(d.DescriptionKey))errors.Add(path+".descriptionKey: missing localization");
            if(d.Animation is not null && !c.Animations.Contains(d.Animation))errors.Add(path+".animation: missing animation binding");
            if(d.References.IsDefault || d.Prerequisites.IsDefault)errors.Add(path+": reference arrays required");
        }
        foreach(var category in Categories)if(!c.Definitions.Any(d=>d?.Category==category))errors.Add($"release.categories.{category}: no schema representative");
        foreach(var d in c.Definitions.Where(d=>d is not null))
            foreach(var id in (d.References.IsDefault?[]:d.References).Concat(d.Prerequisites.IsDefault?[]:d.Prerequisites))if(!ids.Contains(id))errors.Add($"release.{d.Id}.references: unknown {id}");
        var byId=c.Definitions.Where(d=>d is not null).DistinctBy(d=>d.Id).ToDictionary(d=>d.Id);var visited=new HashSet<string>();var active=new HashSet<string>();
        bool Visit(string id)
        {
            if(active.Contains(id))return false;if(!visited.Add(id))return true;active.Add(id);
            if(byId.TryGetValue(id,out var d) && !d.Prerequisites.IsDefault)foreach(var prerequisite in d.Prerequisites)if(!Visit(prerequisite))return false;
            active.Remove(id);return true;
        }
        foreach(var id in ids)if(!Visit(id)) { errors.Add($"release.{id}.prerequisites: cycle / unreachable quest");break; }
        foreach(var id in WorldLaws.Rules.Select(r=>r.Id).Concat(WorldLaws.Mutations))if(!ids.Contains(id))errors.Add($"release.mechanics: missing {id}");
        return errors;
    }
    public static void Report(ReleaseCatalog c,string directory)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllLines(Path.Combine(directory,"categories.csv"),new[]{"category,definitions"}.Concat(Categories.Select(k=>$"{k},{c.Definitions.Count(d=>d.Category==k)}")));
        File.WriteAllText(Path.Combine(directory,"content.json"),JsonSerializer.Serialize(c,new JsonSerializerOptions { WriteIndented=true }));
    }
}
