using Godot;
using Overload.Domain;

namespace Overload.Game;

public partial class WorldView
{
    public ZoneDefinition? Exploration { get; private set; }
    private WorldProgress? progress;
    public WorldProgress? Progress { get=>progress; set {progress=value;terrainDirty=true;} }
    public Func<string,bool>? MapClaimed { get; set; }
    public Func<string,bool>? MapRequired { get; set; }
    public Vector2 Viewer { get; set; }
    public void Configure(ZoneDefinition zone)
    {
        Configure([]);Exploration=zone;Geometry=zone.Geometry;region=zone.Region;
        surface.Configure(zone);
        foreach(var body in GetChildren().OfType<StaticBody2D>()){body.CollisionLayer=0;body.QueueFree();}
        CurrentWalls=[..Geometry.Collision.Select(b=>new Rect2(b.X,b.Y,b.Width,b.Height))];Navigation=Geometry.Navigation();
        foreach(var rect in CurrentWalls)
        {
            var body=new StaticBody2D {Position=rect.GetCenter(),CollisionLayer=1,CollisionMask=0};
            body.AddChild(new CollisionShape2D {Shape=new RectangleShape2D {Size=rect.Size}});AddChild(body);
        }
        foreach(var block in Geometry.Blocks)AddChild(new WorldStructure {Footprint=new(block.X,block.Y,block.Width,block.Height),Region=zone.Region,Outdoors=zone.Kind is "wild" or "frontier",Settlement=zone.Kind=="hearth",ZIndex=(block.Y+block.Height)/4});
        QueueRedraw();
    }
    private bool Claimed(string id)=>MapClaimed?.Invoke(id)??Progress?.Claims.Contains(id)==true;
    private void DrawExplorationGround()
    {
        var z=Exploration!;var restored=Progress?.Resolved.Contains(z.Region)==true;
        var ground=z.Kind=="hearth"?new Color("282d2b"):z.Region switch {Region.Ash=>new Color("302c29"),Region.Glass=>new("253c3a"),Region.Hollow=>new("34343e"),_=>new("393035")};
        var edge=z.Region switch {Region.Ash=>new Color("94775a"),Region.Glass=>new("65918a"),Region.Hollow=>new("8a829b"),_=>new("987f75")};
        var phase=restored?new Color("70bfac"):z.Region==Region.Ash?new Color("d58b52"):edge;
        // A reusable quiet floor kit. Only visible tiles are submitted; decoration has no collision or RNG authority.
        var view=new Rect2(Viewer-new Vector2(400,260),new(800,520));
        DrawWorldRoads(z,view,ground,edge,phase);
        DrawGroundLife(z,view,ground,edge,phase,restored);
        DrawLandscapeScenery(z,view,edge);
        foreach(var block in z.Geometry.Blocks)
        {
            var rect=new Rect2(block.X,block.Y,block.Width,block.Height);if(!rect.Intersects(view))continue;
            DrawRect(new(rect.Position+new Vector2(7,12),rect.Size),new Color(0,0,0,.38f));
        }
    }
    private void DrawExploration()
    {
        var z=Exploration!;var restored=Progress?.Resolved.Contains(z.Region)==true;
        var edge=z.Region switch {Region.Ash=>new Color("94775a"),Region.Glass=>new("65918a"),Region.Hollow=>new("8a829b"),_=>new("987f75")};
        var phase=restored?new Color("70bfac"):z.Region==Region.Ash?new Color("d58b52"):edge;
        var view=new Rect2(Viewer-new Vector2(400,260),new(800,520));
        DrawGroundWind(z,view,edge);
        foreach(var exit in z.Exits)
        {
            var p=new Vector2(exit.Position.X,exit.Position.Y);if(!view.HasPoint(p))continue;
            var open=Progress is null||WorldRules.ExitOpen(Progress,exit);var color=open?new Color("b8dccb"):new("bd826c");
            WorldMaterials.Scenery(this,2,p+new Vector2(-19,2),44,new(.8f,.8f,.73f));
            WorldMaterials.Scenery(this,2,p+new Vector2(19,2),44,new(.8f,.8f,.73f));
            if(open)WorldMaterials.Light(this,p-new Vector2(0,17),38,new Color(.46f,.8f,.68f,.25f));
            DrawArc(p,25,0,Mathf.Tau,32,new Color(color,.6f),2);DrawPolyline([p+new Vector2(-12,0),p+new Vector2(-12,-35),p+new Vector2(12,-35),p+new Vector2(12,0)],color,3);
            WorldNameplate(p-new Vector2(0,47),exit.Name,color,p);
        }
        foreach(var e in z.Encounters.Where(e=>!Claimed(e.Id)&&(e.Boss>=0||MapRequired?.Invoke(e.Id)==true)))
        {
            var p=new Vector2(e.Position.X,e.Position.Y);if(!view.HasPoint(p))continue;
            DrawArc(p,36,0,Mathf.Tau,32,new Color("cda171",.6f),2);WorldNameplate(p-new Vector2(0,48),e.Boss>=0?"RULER":"MARKED ELITE",new("e4bf7d"),p,180);
        }
        foreach(var site in z.Sites)
        {
            var p=new Vector2(site.Position.X,site.Position.Y);if(!view.HasPoint(p))continue;
            var active=Claimed(site.Id)||Progress?.Waypoints.Contains(site.Id)==true;var color=active?new Color("8ee4bd"):new("c9b58b");
            if(site.Kind=="npc")
            {
                if(Progress is not null&&!WorldRules.Satisfied(Progress,site.Requires))continue;
                var index=site.Id.Contains("iven")?1:site.Id.Contains("sen")?2:0;
                DrawSetTransformMatrix(new Transform2D(Vector2.Right,new Vector2(-.48f,-.24f),p+new Vector2(3,4)));
                WorldMaterials.Sprite(this,"villagers",index,Vector2.Zero,41,new Color(0,0,0,.3f));DrawSetTransform(Vector2.Zero);
                WorldMaterials.Sprite(this,"villagers",index,p,41,new(.87f,.85f,.78f));
            }
            else if(site.Kind is "waypoint" or "refuge")
            {WorldMaterials.Light(this,p-new Vector2(0,15),58,active?new Color(.8f,.63f,.32f,.28f):new Color(.36f,.5f,.44f,.16f));DrawSetTransform(p,0,new(1,.4f));DrawArc(Vector2.Zero,20,0,Mathf.Tau,32,new Color(color,.65f),2);DrawSetTransform(Vector2.Zero);WorldArt.Draw(this,z.Region,true,new(p-new Vector2(20,43),new(40,48)),active?Colors.White:new(.75f,.75f,.7f));
                if(active)for(var i=0;i<4;i++){var q=p+new Vector2(28+i*18,14+(i%2)*14);var survivor=PixelAtlas.Load("threadseer");survivor.Draw(this,2,survivor.Idle,q,new Color(.9f,.78f,.6f),.5f);}}
            else {DrawRect(new(p-new Vector2(12,16),new(24,16)),edge.Darkened(.35f));DrawLine(p-new Vector2(8,18),p+new Vector2(8,-18),color,2);}
            WorldNameplate(p-new Vector2(0,50),site.Name,color,p);
        }
        for(var i=0;i<16;i++){var x=Viewer.X-320+(i*137%640);var y=Viewer.Y-180+Mathf.PosMod(i*61-time*6,360);DrawRect(new(new Vector2(x,y).Round(),Vector2.One),new Color(phase,.22f));}
    }
    private void WorldNameplate(Vector2 position,string caption,Color color,Vector2 landmark,float range=120)
    {
        if(Viewer.DistanceTo(landmark)>range)return;
        var font=ThemeDB.FallbackFont;const int size=8;
        var width=font.GetStringSize(caption,fontSize:size).X;
        var rect=new Rect2(position-new Vector2(width/2+5,10),new(width+10,15));
        DrawRect(rect,new Color("151410",.88f));DrawLine(rect.Position+new Vector2(2,14),rect.End-new Vector2(2,1),new Color(color,.55f));
        DrawString(font,position-new Vector2(width/2,0),caption,fontSize:size,modulate:color);
    }
}
