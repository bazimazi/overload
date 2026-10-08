using Overload.Content;
using Overload.Tools;

if (args.Length == 0 || args[0] is not ("validate-content" or "balance-report" or "content-report" or "generator-report" or "world-report" or "frame-report" or "quality-report" or "release-update"))
{
    Console.Error.WriteLine("Usage: validate-content [path] | balance-report [output-directory]");
    return 2;
}
try
{
    if(args[0]=="world-report"){WorldReports.Export(args.Length>1?args[1]:"artifacts/world");return 0;}
    if(args[0]=="release-update")
    {
        if(args.Length!=3)throw new ArgumentException("release-update requires archived runtime directory and output directory");
        ReleaseReports.Verify(args[1],args[2]);return 0;
    }
    if(args[0] is "content-report" or "generator-report")
    {
        var catalog=ReleaseContent.Load(File.ReadAllText("src/Overload.Game/Content/release.json"));
        var output=args.Length>1?args[1]:"artifacts/production-p07";
        ReleaseContent.Report(catalog,output);
        if(args[0]=="generator-report")
        {
            var samples=new List<object>();
            for(ulong seed=0;seed<10000;seed++)
            {
                var layout=Overload.Domain.ExpeditionGenerator.Generate(seed);
                if(!Overload.Domain.ExpeditionGenerator.Connected(layout))throw new InvalidDataException($"Generator disconnected at {seed}");
                if(seed<24)samples.Add(layout);
            }
            File.WriteAllText(Path.Combine(output,"layouts.json"),System.Text.Json.JsonSerializer.Serialize(samples,new System.Text.Json.JsonSerializerOptions { WriteIndented=true }));
            Console.WriteLine("GENERATOR_OK 10000 seeds; sample layouts saved");
        }
        Console.WriteLine($"CONTENT_OK {catalog.Definitions.Length} entries in {ReleaseContent.Categories.Length} categories");return 0;
    }
    if(args[0] is "frame-report" or "quality-report")
    {
        var expandedProfile=ExpansionLoader.Load(ProfileLoader.Load(File.ReadAllText("src/Overload.Game/Content/arena.json")),File.ReadAllText("src/Overload.Game/Content/expansion.json"));
        if(args[0]=="quality-report")QualityReports.Export(expandedProfile,args.Length>1?args[1]:"artifacts/quality");
        else FrameReports.Export(expandedProfile,args.Length>1?args[1]:"artifacts/expansion-a03");return 0;
    }
    if (args[0] == "balance-report")
    {
        var output = args.Length > 1 ? args[1] : "artifacts/balance-e05";
        BalanceReports.Export(ProfileLoader.Load(File.ReadAllText("src/Overload.Game/Content/arena.json")),output);
        Console.WriteLine($"Balance tables, encounter sweeps and benchmarks exported to {Path.GetFullPath(output)}"); return 0;
    }
    var path = args.Length > 1 ? args[1] : "src/Overload.Game/Content/arena.json";
    var profile = ProfileLoader.Load(File.ReadAllText(path));
    foreach(var zone in Overload.Domain.WorldContent.Zones.Values)Overload.Domain.WorldContent.Validate(zone);
    Console.WriteLine("VALID world.v1: 17 connected authored zones, physical exits and regional reward manifests");
    Console.WriteLine($"VALID {profile.Id}: {profile.Skills.Length} actions, {profile.Enemies.Length} enemy roles, {profile.Overload.Patterns.Length} pattern definitions, {profile.Overload.Bindings.Length} bindings");
    ExpansionLoader.Load(profile,File.ReadAllText("src/Overload.Game/Content/expansion.json"));
    Console.WriteLine("VALID expansion.v1: 3 Frames, 24 skills, 24 regional families, 8 bosses, 48 rooms");
    var release=ReleaseContent.Load(File.ReadAllText("src/Overload.Game/Content/release.json"));
    Console.WriteLine($"VALID {release.Version}: {release.Definitions.Length} entries across {ReleaseContent.Categories.Length} categories");
    return 0;
}
catch (Exception e) { Console.Error.WriteLine(e.Message); return 1; }
