using Godot;
using Overload.Domain;

namespace Overload.Game;
public sealed record RoomDefinition(string Name, string Lesson, Rect2[] Obstacles, (EnemyRole Role, Vector2 Position)[] Spawns);
public static class RoomCatalog
{
    public static readonly RoomDefinition[] Rooms =
    [
        new("The Threshold", "Hold Cleave to attack. Move out of amber warnings; strike during recovery.", [], [(EnemyRole.Pursuer, new(410,180)), (EnemyRole.Pursuer,new(480,240))]),
        new("Pilgrim Walk", "Move 3m within 2s to store Momentum. Attack for Pursuit, or Traverse for Crossing.", [new(290,110,28,44)], [(EnemyRole.Pursuer,new(400,210)), (EnemyRole.Pursuer,new(510,130)), (EnemyRole.Brute,new(470,270))]),
        new("The Listening Hall", "Evade THROUGH a bolt to store Echo. Your next attack repeats as Afterstrike.", [new(300,235,32,35)], [(EnemyRole.Caster,new(490,150)), (EnemyRole.Caster,new(510,260))]),
        new("Split Cloister", "Three active slots: Q / E / R. Watch Focus, cooldowns and the Next prediction.", [new(270,100,28,64),new(370,230,28,55)], [(EnemyRole.Brute,new(420,180)), (EnemyRole.Caster,new(520,110)), (EnemyRole.Pursuer,new(480,280))]),
        new("The Forge Approach", "At Hearth, compare recovered gear, improve quality, or replace one affix slot.", [new(310,150,28,65)], [(EnemyRole.Brute,new(430,100)), (EnemyRole.Brute,new(460,270)), (EnemyRole.Caster,new(540,190))]),
        new("Echo Garden", "Try Convergence with both memories. Change your three bindings at Hearth.", [new(260,100,30,30),new(390,240,30,30)], [(EnemyRole.Caster,new(500,110)), (EnemyRole.Caster,new(530,260)), (EnemyRole.Pursuer,new(390,190)), (EnemyRole.Brute,new(470,190))]),
        new("Bell Antechamber", "Flask restores Life. Reprieve adds a barrier; Shelter stops ordinary bolts, not boss volleys.", [new(310,100,40,30),new(310,260,40,30)], [(EnemyRole.Brute,new(440,150)), (EnemyRole.Brute,new(440,250)), (EnemyRole.Caster,new(550,100)), (EnemyRole.Caster,new(550,280))]),
        new("The Bellkeeper", "Watch the locked sweep and volley. Strike in recovery. The bell quickens at half Life.", [], [(EnemyRole.Bellkeeper,new(450,185))])
    ];
}
