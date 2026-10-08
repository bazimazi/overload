using System.Numerics;
using Overload.Content;
using Overload.Domain;
using Xunit;

namespace Overload.Domain.Tests;

public sealed class NavigationTests
{
    [Theory]
    [InlineData(9)] [InlineData(17)]
    public void RoutesAroundCoverWithoutCuttingFootprintCorners(float radius)
    {
        var navigation = new TacticalNavigation([new(285, 120, 45, 140)]);
        var from = new Vector2(180, 192); var to = new Vector2(455, 192);
        Assert.False(navigation.Clear(from, to, radius));
        var path = navigation.FindPath(from, to, radius);
        Assert.True(path.Length > 1); Assert.Equal(to, path[^1]);
        foreach (var point in path) { Assert.True(navigation.Clear(from, point, radius)); from = point; }
    }
    [Fact]
    public void ClosedRoomAndInvalidLandingCannotProduceAWallCrossingRoute()
    {
        var navigation = new TacticalNavigation([new(285, 56, 45, 258)]);
        Assert.Empty(navigation.FindPath(new(180,192), new(455,192), 9));
        Assert.Empty(navigation.FindPath(new(180,192), new(305,192), 9));
    }
    [Fact]
    public void DirectRouteAndRepeatedQueriesRemainStable()
    {
        var navigation = new TacticalNavigation([new(300,110,28,44)]);
        var target = new Vector2(455, 220);
        Assert.Equal(new[] { target }, navigation.FindPath(new(180,220), target, 9));
        var route = navigation.FindPath(new(180,130), new(455,130), 9);
        Assert.Equal(route, navigation.FindPath(new(180,130), new(455,130), 9));
    }
    [Fact]
    public void BossTouchingThePhysicsSafeMarginCanRepath()
    {
        var navigation=new TacticalNavigation([new(285,120,45,140)]);
        var from=new Vector2(326.414f,277.1184f);var to=new Vector2(180,192);
        var route=navigation.FindPath(from,to,17);
        Assert.NotEmpty(route);Assert.Equal(to,route[^1]);
        foreach(var point in route){Assert.True(navigation.Clear(from,point,17));from=point;}
    }
    [Fact]
    public void EveryRegionalRoomProvidesOrdinaryEnemyRoutesToThePlayer()
    {
        _=ExpansionLoader.Load(ProfileLoader.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"arena.json"))),File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"expansion.json")));
        foreach(var room in RegionalContent.Rooms)
        {
            var navigation=new TacticalNavigation(room.Obstacles);
            foreach(var spawn in new[]{new Vector2(448,192),new Vector2(512,128),new Vector2(512,264)})
            {
                var target=new Vector2(140,190);var from=spawn;
                var route=navigation.FindPath(from,target,9);
                Assert.True(route.Length>0,$"{room.Name}: enemy at {spawn} cannot route to the player");
                foreach(var point in route){Assert.True(navigation.Clear(from,point,9));from=point;}
            }
        }
    }
}
