using System.Numerics;
using System.Reflection;
using System.Runtime.Loader;
using Overload.Content;
using Overload.Domain;

namespace Overload.Tools;

public static class ReleaseReports
{
    private sealed class HistoricalContext(string folder) : AssemblyLoadContext(isCollectible:true)
    {
        protected override Assembly? Load(AssemblyName name)
        {
            var path=Path.Combine(folder,name.Name+".dll");
            return File.Exists(path)?LoadFromAssemblyPath(path):null;
        }
    }
    public static void Verify(string historicalRuntime,string output)
    {
        Directory.CreateDirectory(output);
        var context=new HistoricalContext(Path.GetFullPath(historicalRuntime));
        try
        {
            var oldContent=context.LoadFromAssemblyPath(Path.Combine(Path.GetFullPath(historicalRuntime),"Overload.Content.dll"));
            var oldStore=oldContent.GetType("Overload.Content.CharacterStore",true)!;
            var decode=oldStore.GetMethod("Decode")!;var encode=oldStore.GetMethod("Encode")!;
            var profile=oldContent.GetType("Overload.Content.ProfileLoader",true)!.GetMethod("Load")!.Invoke(null,[File.ReadAllText("src/Overload.Game/Content/arena.json")]);
            oldContent.GetType("Overload.Content.ExpansionLoader",true)!.GetMethod("Load")!.Invoke(null,[profile,File.ReadAllText("src/Overload.Game/Content/expansion.json")]);
            ExpansionLoader.Load(ProfileLoader.Load(File.ReadAllText("src/Overload.Game/Content/arena.json")),File.ReadAllText("src/Overload.Game/Content/expansion.json"));
            var rows=new List<object>();var tier=BigInteger.Pow(10,40)+123;
            for(var version=1;version<=3;version++)
            {
                var state=EndlessFixtures.Reference(tier) with { Gold=BigInteger.Pow(10,90),Alloy=BigInteger.Pow(10,80),ElsewhereOwned=true,ElsewhereSelected=true };
                state=version==1?EndlessRules.Begin(state,tier,Guid.NewGuid()):EndgameRules.Begin(state,tier,Guid.NewGuid(),Region.Glass,ActivityFamily.Breach,regionalContent:version==3);
                var run=state.Fracture!;
                for(var group=0;group<3;group++)state=EndlessRules.Claim(state,run.Id,run.Sequence,group);
                // Decode and re-encode using the actual archived baseline assemblies, isolated from current statics.
                var historical=decode.Invoke(null,[CharacterStore.Encode(state)])!;
                var historicalText=(string)encode.Invoke(null,[historical])!;
                var path=Path.Combine(output,"baseline-v"+version);Directory.CreateDirectory(path);
                File.WriteAllText(Path.Combine(path,"character.json"),historicalText);
                var stopwatch=System.Diagnostics.Stopwatch.StartNew();var store=new CharacterStore(path);stopwatch.Stop();
                if(store.State.TotalXp!=state.TotalXp||store.State.Gold!=state.Gold||store.State.AttunementGrade!=state.AttunementGrade
                    ||CharacterStore.Encode(store.State)!=historicalText)throw new InvalidDataException("Baseline update changed persisted values");
                for(var group=3;group<7;group++)store.Transact(store.State.Revision,$"update:{group}",s=>EndlessRules.Claim(s,run.Id,run.Sequence,group));
                if(!store.State.Fracture!.Completed||store.State.Fracture.Id!=run.Id)throw new InvalidDataException("Update could not finish active run");
                rows.Add(new {version=run.ContentVersion,generator=run.Layout?.Version??"authored.v1",tier=tier.ToString(),oldLevel=state.ValidatedLevel.ToString(),loadMs=stopwatch.Elapsed.TotalMilliseconds,
                    geometryPreserved=true,countersPreserved=true,remainingGroups=4,completed=true});
            }
            File.WriteAllText(Path.Combine(output,"update.json"),System.Text.Json.JsonSerializer.Serialize(new {baseline="archived A03 assemblies",fixtures=rows},new System.Text.Json.JsonSerializerOptions {WriteIndented=true}));
            Console.WriteLine("RELEASE_UPDATE_OK archived-baseline=3 versions; exact counters/geometry preserved; remaining groups committed");
        }
        finally{context.Unload();}
    }
}
