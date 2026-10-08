using Godot;
using Overload.Domain;

namespace Overload.Game;

public partial class WorldView
{
    public ZoneDefinition? Exploration { get; private set; }
    public WorldProgress? Progress { get; set; }
    public Func<string,bool>? MapClaimed { get; set; }
    public Func<string,bool>? MapRequired { get; set; }
    public Vector2 Viewer { get; set; }
    private Texture2D? landmarks;
    public void Configure(ZoneDefinition zone)
    {
        Configure([]);Exploration=zone;Geometry=zone.Geometry;region=zone.Region;
        landmarks??=GD.Load<Texture2D>("res://Assets/Pixel/world-landmarks.png");
        foreach(var body in GetChildren().OfType<StaticBody2D>()){body.CollisionLayer=0;body.QueueFree();}
        CurrentWalls=[..Geometry.Collision.Select(b=>new Rect2(b.X,b.Y,b.Width,b.Height))];Navigation=Geometry.Navigation();
        foreach(var rect in CurrentWalls)
        {
            var body=new StaticBody2D {Position=rect.GetCenter(),CollisionLayer=1,CollisionMask=0};
            body.AddChild(new CollisionShape2D {Shape=new RectangleShape2D {Size=rect.Size}});AddChild(body);
        }
        QueueRedraw();
    }
    private bool Claimed(string id)=>MapClaimed?.Invoke(id)??Progress?.Claims.Contains(id)==true;
    private void DrawExploration()
    {
        var z=Exploration!;var restored=Progress?.Resolved.Contains(z.Region)==true;
        var ground=z.Kind=="hearth"?new Color("282d2b"):z.Region switch {Region.Ash=>new Color("302c29"),Region.Glass=>new("253c3a"),Region.Hollow=>new("34343e"),_=>new("393035")};
        var edge=z.Region switch {Region.Ash=>new Color("94775a"),Region.Glass=>new("65918a"),Region.Hollow=>new("8a829b"),_=>new("987f75")};
        var phase=restored?new Color("70bfac"):z.Region==Region.Ash?new Color("d58b52"):edge;
        var b=z.Geometry.Bounds;DrawRect(new(b.X-64,b.Y-64,b.Width+128,b.Height+128),ground.Darkened(.4f));
        DrawRect(new(b.X,b.Y,b.Width,b.Height),ground);
        // A reusable quiet floor kit. Only visible tiles are submitted; decoration has no collision or RNG authority.
        var view=new Rect2(Viewer-new Vector2(400,260),new(800,520));
        var floorTexture=backgrounds[z.Kind=="hearth"?"hearth":z.Region.ToString().ToLowerInvariant()];
        var floorSource=new Rect2(floorTexture.GetWidth()*.26f,floorTexture.GetHeight()*.42f,floorTexture.GetWidth()*.16f,floorTexture.GetHeight()*.28f);
        for(var y=Math.Max(0,(int)view.Position.Y/96*96);y<Math.Min(b.Height,view.End.Y+96);y+=96)
            for(var x=Math.Max(0,(int)view.Position.X/96*96);x<Math.Min(b.Width,view.End.X+96);x+=96)
                DrawTextureRectRegion(floorTexture,new(x,y,96,96),floorSource,new Color(.58f,.58f,.58f));
        for(var y=Math.Max(0,(int)view.Position.Y/48*48);y<Math.Min(b.Height,view.End.Y+48);y+=48)
            for(var x=Math.Max(0,(int)view.Position.X/48*48);x<Math.Min(b.Width,view.End.X+48);x+=48)
            {
                var tone=((x/48*17+y/48*31)%7)/160f;
                DrawRect(new(x+1,y+1,46,46),new Color(ground.Lightened(tone),.08f));
                if(z.Region==Region.Glass)DrawLine(new(x+8,y+32),new(x+36,y+29),edge.Darkened(.5f));
                else if(z.Region==Region.Hollow)DrawRect(new(x+8,y+8,3,3),edge.Darkened(.45f));
            }
        if(z.Kind=="wild")
        {
            // Continuous material cues make both sides of the fork recognizable and lead to the eastern junction.
            foreach(var y in new[]{300,820})
            {DrawRect(new(160,y-22,1600,44),ground.Lightened(.08f));DrawLine(new(160,y-25),new(1760,y-25),phase.Darkened(.45f),3);DrawLine(new(160,y+25),new(1760,y+25),edge.Darkened(.55f),2);}
            if(z.Region==Region.Ash)for(var x=180;x<1730;x+=96){DrawRect(new(x,260,64,8),edge);DrawRect(new(x,268,64,4),phase.Darkened(.3f));}
            if(z.Region==Region.Glass)for(var x=180;x<1720;x+=36)DrawLine(new(x,800),new(x+10,815),edge.Darkened(.3f),2);
        }
        foreach(var block in z.Geometry.Blocks)
        {
            var rect=new Rect2(block.X,block.Y,block.Width,block.Height);if(!rect.Intersects(view))continue;
            DrawRect(new(rect.Position+new Vector2(5,8),rect.Size),new Color(0,0,0,.35f));
            DrawRect(rect,ground.Darkened(.5f));DrawRect(new(rect.Position+new Vector2(4,4),rect.Size-new Vector2(8,8)),edge.Darkened(.65f));
            for(var y=Math.Max(rect.Position.Y+4,MathF.Floor(view.Position.Y/48)*48);y<Math.Min(rect.End.Y-4,view.End.Y+48);y+=48)
                for(var x=Math.Max(rect.Position.X+4,MathF.Floor(view.Position.X/48)*48);x<Math.Min(rect.End.X-4,view.End.X+48);x+=48)
                    DrawTextureRectRegion(floorTexture,new(x,y,Math.Min(48,rect.End.X-4-x),Math.Min(48,rect.End.Y-4-y)),floorSource,new Color(.25f,.25f,.25f));
            DrawRect(rect,edge.Darkened(.2f),false,3);
            for(var y=rect.Position.Y+24;y<rect.End.Y;y+=32)DrawLine(new(rect.Position.X+4,y),new(rect.End.X-4,y),ground.Darkened(.3f),2);
            if(z.Region==Region.Ash){DrawRect(new(rect.Position+new Vector2(12,14),new(rect.Size.X-24,10)),phase.Darkened(.3f));for(var x=rect.Position.X+24;x<rect.End.X-16;x+=48)DrawRect(new(x,rect.Position.Y+36,16,32),ground.Darkened(.8f));}
            if(z.Region==Region.Glass){DrawRect(new(rect.Position+new Vector2(14,14),rect.Size-new Vector2(28,28)),new Color("41666b"));DrawLine(rect.Position+new Vector2(20,20),rect.End-new Vector2(20,20),edge.Lightened(.2f),2);}
            if(z.Region==Region.Hollow)for(var x=rect.Position.X+12;x<rect.End.X-8;x+=20)DrawRect(new(x,rect.Position.Y+12,8,rect.Size.Y-24),edge.Darkened(.25f));
            if(z.Region==Region.Crown){var c=rect.GetCenter();DrawPolyline([c+new Vector2(-30,12),c+new Vector2(-28,-22),c+new Vector2(-10,-9),c+new Vector2(0,-38),c+new Vector2(12,-9),c+new Vector2(28,-22),c+new Vector2(30,12)],edge,3);}
        }
        if(landmarks is not null&&z.Kind=="wild")
        {
            var block=z.Geometry.Blocks[0];var width=landmarks.GetWidth()/2;var height=landmarks.GetHeight()/2;var n=(int)z.Region;
            var side=Math.Min(block.Width-20,block.Height-12);
            DrawTextureRectRegion(landmarks,new(block.X+(block.Width-side)/2,block.Y+block.Height-side,side,side),new(n%2*width,n/2*height,width,height),restored?new Color(.82f,1,.95f):Colors.White);
        }
        foreach(var exit in z.Exits)
        {
            var p=new Vector2(exit.Position.X,exit.Position.Y);if(!view.HasPoint(p))continue;
            var open=Progress is null||WorldRules.ExitOpen(Progress,exit);var color=open?new Color("b8dccb"):new("bd826c");
            DrawArc(p,25,0,Mathf.Tau,32,new Color(color,.6f),2);DrawPolyline([p+new Vector2(-12,0),p+new Vector2(-12,-35),p+new Vector2(12,-35),p+new Vector2(12,0)],color,3);
            DrawString(ThemeDB.FallbackFont,p+new Vector2(-45,-44),exit.Name,fontSize:10,modulate:color);
        }
        foreach(var e in z.Encounters.Where(e=>!Claimed(e.Id)&&(e.Boss>=0||MapRequired?.Invoke(e.Id)==true)))
        {
            var p=new Vector2(e.Position.X,e.Position.Y);if(!view.HasPoint(p))continue;
            DrawArc(p,36,0,Mathf.Tau,32,new Color("cda171",.6f),2);DrawString(ThemeDB.FallbackFont,p+new Vector2(-30,-48),e.Boss>=0?"RULER":"MARKED ELITE",fontSize:10,modulate:new("e4bf7d"));
        }
        foreach(var site in z.Sites)
        {
            var p=new Vector2(site.Position.X,site.Position.Y);if(!view.HasPoint(p))continue;
            var active=Claimed(site.Id)||Progress?.Waypoints.Contains(site.Id)==true;var color=active?new Color("8ee4bd"):new("c9b58b");
            if(site.Kind=="npc")
            {if(Progress is not null&&!WorldRules.Satisfied(Progress,site.Requires))continue;var sprite=PixelAtlas.Load(site.Id.Contains("iven")?"revenant":site.Id.Contains("sen")?"threadseer":"warden");sprite.Draw(this,2,sprite.Idle,p,new Color(.85f,.85f,.8f),.85f);}
            else if(site.Kind is "waypoint" or "refuge")
            {DrawArc(p,20,0,Mathf.Tau,32,color,2);DrawRect(new(p-new Vector2(5,27),new(10,27)),edge);DrawCircle(p-new Vector2(0,27),4,color);
                if(active)for(var i=0;i<4;i++){var q=p+new Vector2(28+i*18,14+(i%2)*14);var survivor=PixelAtlas.Load("threadseer");survivor.Draw(this,2,survivor.Idle,q,new Color(.9f,.78f,.6f),.5f);}}
            else {DrawRect(new(p-new Vector2(12,16),new(24,16)),edge.Darkened(.35f));DrawLine(p-new Vector2(8,18),p+new Vector2(8,-18),color,2);}
            DrawString(ThemeDB.FallbackFont,p+new Vector2(-50,-38),site.Name,fontSize:10,modulate:color);
        }
        for(var i=0;i<16;i++){var x=Viewer.X-320+(i*137%640);var y=Viewer.Y-180+Mathf.PosMod(i*61-time*6,360);DrawRect(new(new Vector2(x,y).Round(),Vector2.One),new Color(phase,.22f));}
    }
}
