using System.Collections.Immutable;

namespace Overload.Domain;
public sealed record RegionalRoom(int Id, Region Region, string Name, bool Boss, ImmutableArray<RoomBlock> Obstacles);
public static class RegionalContent
{
    public const string GeneratorVersion = "rooms.v2";
    public const string ExpeditionVersion = "fracture.v3";
    public static ImmutableArray<RegionalRoom> Rooms { get; private set; } = [];
    public static ImmutableArray<EnemyDefinition> Enemies { get; private set; } = [];
    public static void Configure(ImmutableArray<RegionalRoom> rooms, ImmutableArray<EnemyDefinition> enemies) { Rooms=rooms;Enemies=enemies; }
    public static GeneratedLayout Generate(ulong seed, Region region)
    {
        var random=new Random(unchecked((int)(seed^(seed>>32))));
        var available=Rooms.Where(r=>r.Region==region&&!r.Boss).ToList();
        var selected=new List<RegionalRoom>();
        for(var i=0;i<6;i++) { var n=random.Next(available.Count);selected.Add(available[n]);available.RemoveAt(n); }
        selected.Add(Rooms.Single(r=>r.Id==(int)region*12+10+(int)(seed%2)));
        var original=ExpeditionGenerator.Generate(seed,0);
        return original with { Version=GeneratorVersion,Rooms=[..selected.Select(r=>r.Id)],Obstacles=[..selected.Select(r=>r.Obstacles)],Fallback=false };
    }
    public static EnemyDefinition Boss(Region region, int variant) => Enemies.Single(e=>e.Role==EnemyRole.Bellkeeper&&e.ContentId==new[]{"boss.region.bellkeeper","boss.region.forgeheart","boss.region.mirrorregent","boss.region.reedwidow","boss.region.indexer","boss.region.nullabbot","boss.region.scarmarshal","boss.region.firstpattern"}[(int)region*2+variant]);
    public static EnemyDefinition Enemy(Region region, int index) => Enemies.Where(e=>e.Role!=EnemyRole.Bellkeeper).ElementAt((int)region*6+index);
    public static bool IsRegional(string version) => version is EndgameRules.Version or ExpeditionVersion or FractureMapRules.Version;
}
