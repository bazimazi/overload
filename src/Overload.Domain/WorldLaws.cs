using System.Collections.Immutable;
using System.Numerics;

namespace Overload.Domain;
public sealed record WorldRule(string Id,string Name,string Description,int WarningTicks,int Period,int DamagePercent);
public static class WorldLaws
{
    public static ImmutableArray<WorldRule> Rules { get; private set; }=[
        new("law.reverberation","Reverberation","A fixed left-side echo pulses; move into the central lane.",90,360,8),
        new("law.glass","Glass Shelter","Breakable northern cover intercepts ordinary projectiles.",90,360,0),
        new("law.vents","Migrating Vents","Vents alternate north and south; the central lane stays safe.",90,360,8),
        new("law.tides","Ash Tides","The south terrace burns after its warning.",90,420,8),
        new("law.watch","Hollow Watch","A northern gaze pulses after its warning.",90,480,8),
        new("law.crown","Crown Toll","Side pillars pulse; pass through the broad central lane.",90,420,8)];
    public static readonly ImmutableArray<string> Mutations=["mutation.anchor","mutation.split","mutation.patience","mutation.procession"];
    public static void Configure(ImmutableArray<WorldRule> rules) => Rules=rules;
    public static string MutationDescription(string id)=>id switch
    { "mutation.anchor"=>"Return mark: step away from the mark before the volley; the boss returns afterward.",
      "mutation.split"=>"Wide fan: five bolts spread wider and travel slower; use the gaps.",
      "mutation.patience"=>"Patient bell: the boss prepares each attack for an extra 0.4 seconds.",
      "mutation.procession"=>"Procession: the boss approaches sideways at its ordinary movement speed.",_=>"" };
    public static ImmutableArray<string> ForRoute(string route,BigInteger chapter)
    {
        var index=route switch { "route.ash"=>0,"route.glass"=>1,"route.hollow"=>2,_=>3 };
        return chapter<3?[Rules[index].Id]:[Rules[index].Id,Rules[(index+2)%6].Id];
    }
    public static bool Compatible(IEnumerable<string> ids)=>ids.Distinct().Count()==ids.Count()&&ids.Count()<=2&&ids.All(id=>Rules.Any(r=>r.Id==id));
    // All active rectangles leave y=160..220 clear. The central route is safe even for any allowed pair.
    public static ImmutableArray<RoomBlock> Areas(string id,long tick)=>id switch
    {
        "law.reverberation"=>[new(60,80,100,70)],
        "law.vents"=>tick/360%2==0?[new(200,80,220,64)]:[new(200,240,220,52)],
        "law.tides"=>[new(80,245,480,48)],
        "law.watch"=>[new(80,80,480,60)],
        "law.crown"=>[new(230,90,40,54),new(370,242,40,50)],
        _=>[]
    };
}
