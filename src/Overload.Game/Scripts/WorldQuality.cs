using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Godot;
using Overload.Domain;

namespace Overload.Game;

public partial class Arena
{
    private readonly Stopwatch worldQualityClock=new();
    private readonly List<double> worldFrameTimes=[];
    private readonly List<object> worldHeap=[];
    private double worldPreviousFrame,worldQualitySeconds;
    private bool worldSoak;
    private int worldQualityTicks;
    private void BeginWorldQuality()
    {
        var arg=OS.GetCmdlineUserArgs().First(a=>a.StartsWith("--world-perf=",StringComparison.Ordinal)||a.StartsWith("--world-soak=",StringComparison.Ordinal));
        worldSoak=arg.StartsWith("--world-soak=",StringComparison.Ordinal);
        if(!double.TryParse(arg.Split('=')[1],CultureInfo.InvariantCulture,out worldQualitySeconds)||worldQualitySeconds<1||worldQualitySeconds>3600){GD.PushError("World quality duration must be 1..3600 seconds");QuitGame(1);return;}
        if(!worldSoak&&DisplayServer.GetName()=="headless"){GD.PushError("World performance requires a renderer");QuitGame(1);return;}
        UpdateCharacter(s=>WorldRules.Enroll(FrameRules.BuildFixture(FrameId.Threadseer,0)) with {CharacterId=s.CharacterId,Revision=s.Revision});
        WorldTransaction($"quality.entry:{Guid.NewGuid():N}",s=>WorldRules.Enter(s,"ash.0",new(540,850)));EnterWorldZone(true);
        ClearEncounterEnemies();worldPacks.Clear();
        for(var i=0;i<12;i++)Spawn(RegionalContent.Enemy(Region.Ash,i%6) with {Life=1000000000},new(450+i%4*55,780+i/4*55));
        PlayerState.ChangeMaximumLife(System.Numerics.BigInteger.Pow(10,24));PlayerState.Reset();
        if(worldSoak){Engine.PhysicsTicksPerSecond=1920;Engine.TimeScale=32;Engine.MaxPhysicsStepsPerFrame=64;Engine.MaxFps=0;}
        else {Engine.MaxFps=120;DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);}
        worldQualityClock.Start();GD.Print($"WORLD_QUALITY_BEGIN {(worldSoak?"soak":"perf")} duration={worldQualitySeconds}");
    }
    private void AdvanceWorldQuality()
    {
        if(!worldQualityClock.IsRunning)return;
        worldQualityTicks++;
        var target=new Vector2(510+MathF.Cos(worldQualityTicks/150f)*90,890+MathF.Sin(worldQualityTicks/150f)*60);
        Player.MoveAndCollide(Player.Position.DirectionTo(target)*2.4f);
        if(Enemies.Count(e=>!e.Enemy!.Dead)>12||Effects.ProjectileCount>200)throw new InvalidOperationException("World quality entity budget exceeded");
        if(worldQualityTicks%1800==0)worldHeap.Add(new {tick=worldQualityTicks,managed=GC.GetTotalMemory(false),enemies=Enemies.Count,projectiles=Effects.ProjectileCount,saveBytes=Overload.Content.CharacterStore.Encode(Character!.State).Length});
        if(worldSoak&&worldQualityTicks>=worldQualitySeconds*60)FinishWorldQuality();
        if(worldQualityTicks%30==0&&PlayerState.Action is null)PlayerState.TryStart(SkillId.ChainLance,System.Numerics.Vector2.UnitX);
    }
    private void MeasureWorldQuality()
    {
        if(!worldQualityClock.IsRunning||worldSoak)return;
        var now=worldQualityClock.Elapsed.TotalSeconds;
        if(now>=10&&worldPreviousFrame>=10)worldFrameTimes.Add((now-worldPreviousFrame)*1000);
        worldPreviousFrame=now;
        if(now>=worldQualitySeconds+10)FinishWorldQuality();
    }
    private void FinishWorldQuality()
    {
        worldQualityClock.Stop();var ordered=worldFrameTimes.Order().ToArray();
        double Percentile(double p)=>ordered.Length==0?0:ordered[Math.Min(ordered.Length-1,(int)(ordered.Length*p))];
        var output=System.IO.Path.Combine(ProjectSettings.GlobalizePath("res://"),"../../artifacts/world");Directory.CreateDirectory(output);
        File.WriteAllText(System.IO.Path.Combine(output,worldSoak?"soak.json":"performance.json"),JsonSerializer.Serialize(new {simulatedSeconds=worldQualityTicks/60.0,wallSeconds=worldQualityClock.Elapsed.TotalSeconds,samples=ordered.Length,p95Ms=Percentile(.95),p99Ms=Percentile(.99),targetPassed=worldSoak||Percentile(.95)<=16.7&&Percentile(.99)<25,renderer=RenderingServer.GetVideoAdapterName(),output=DisplayServer.WindowGetSize().ToString(),liveEnemies=Enemies.Count(e=>!e.Enemy!.Dead),heap=worldHeap,note="Isolated fixture on scrolling Ash terrain with twelve active regional actors. Human movement and combat acceptance remain separate."},new JsonSerializerOptions{WriteIndented=true}));
        GD.Print($"OVERLOAD_WORLD_QUALITY_OK mode={(worldSoak?"soak":"perf")} ticks={worldQualityTicks} p95={Percentile(.95):0.00} p99={Percentile(.99):0.00}");QuitGame();
    }
}
