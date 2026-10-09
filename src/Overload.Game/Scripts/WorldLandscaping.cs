using Godot;
using Overload.Domain;

namespace Overload.Game;

/// <summary>Material-specific scenery follows the existing navigable geography and collision footprints.</summary>
public partial class WorldView
{
    private readonly Dictionary<(Vector2,Vector2),Vector2[]> roadPaths=[];
    internal IReadOnlyCollection<Vector2[]> PavedRoads=>roadPaths.Values;
    private void Road(Vector2[] points,Rect2 view,Color ground,Color edge,Color phase,bool upper)
    {
        var routed=new List<Vector2>();
        for(var i=1;i<points.Length;i++)
        {
            var key=(points[i-1],points[i]);
            if(!roadPaths.TryGetValue(key,out var path))
            {
                var waypoints=Navigation.FindPath(new(key.Item1.X,key.Item1.Y),new(key.Item2.X,key.Item2.Y),36);
                roadPaths[key]=path=waypoints.Length==0?[]:[key.Item1,..waypoints.Select(p=>new Vector2(p.X,p.Y))];
            }
            if(path.Length==0)continue;
            if(routed.Count>0&&routed[^1]!=path[0]){DrawRoadSegments([..routed],view,ground,edge,phase,upper);routed.Clear();}
            foreach(var p in path)if(routed.Count==0||routed[^1]!=p)routed.Add(p);
        }
        DrawRoadSegments([..routed],view,ground,edge,phase,upper);
    }
    private void DrawRoadSegments(Vector2[] points,Rect2 view,Color ground,Color edge,Color phase,bool upper)
    {
        for(var i=1;i<points.Length;i++)
        {
            var a=points[i-1];var b=points[i];
            if(!new Rect2(a.Min(b)-new Vector2(44,44),(a-b).Abs()+new Vector2(88,88)).Intersects(view))continue;
            DrawLine(a+new Vector2(3,6),b+new Vector2(3,6),ground.Darkened(.55f),72);
            DrawLine(a,b,edge.Darkened(.55f),64);
            DrawLine(a,b,ground.Lightened(.12f),58);
            var length=a.DistanceTo(b);var tangent=a.DirectionTo(b);var side=tangent.Orthogonal();
            for(var d=8f;d<length;d+=upper?18:28)
            {
                var p=(a+tangent*d).Round();if(!view.Grow(35).HasPoint(p))continue;
                DrawLine(p-side*27,p+side*27,edge.Darkened(.5f),2);
                if(upper)
                {
                    DrawRect(new(p+side*24-new Vector2(1,1),new(2,2)),edge);
                    DrawRect(new(p-side*24-new Vector2(1,1),new(2,2)),edge);
                }
                else DrawLine(p-side*26+tangent*2,p-side*6+tangent*2,ground.Lightened(.25f),1);
            }
            if(upper)
            {
                DrawLine(a+side*34,b+side*34,edge.Darkened(.3f),3);
                DrawLine(a-side*34,b-side*34,phase.Darkened(.4f),2);
            }
        }
    }
    private void DrawWorldRoads(ZoneDefinition z,Rect2 view,Color ground,Color edge,Color phase)
    {
        if(z.Kind=="wild")
        {
            Road([new(120,560),new(300,560),new(600,300),new(1540,300),new(1740,560),new(1790,560)],view,ground,edge,phase,true);
            // Glass/Crown's western block reaches y=950: their lower road goes below it.
            var lower=z.Region is Region.Glass or Region.Crown?1000:850;
            Road(z.Region is Region.Glass or Region.Crown?[new(300,560),new(180,700),new(180,1000),new(690,lower),new(1210,lower),new(1600,810),new(1740,560)]:[new(300,560),new(270,700),new(690,lower),new(1210,lower),new(1600,810),new(1740,560)],view,ground,edge,phase,false);
            Road([new(940,lower),new(940,1020)],view,ground,edge,phase,false);
            Signpost(new(470,540),z.Region==Region.Ash?"GANTRY":"UPPER ROAD","REFUGE / LOWER ROAD",edge);
            Signpost(new(1510,370),WorldContent.Places[(int)z.Region][1].ToUpperInvariant(),WorldContent.Places[(int)z.Region][2].ToUpperInvariant(),edge);
        }
        else if(z.Kind=="hearth")
        {
            foreach(var exit in z.Exits)Road([new(640,384),new(exit.Position.X,exit.Position.Y)],view,ground,edge,phase,false);
            DrawArc(new(640,384),78,0,Mathf.Tau,64,edge.Darkened(.4f),3);
            DrawArc(new(640,384),72,0,Mathf.Tau,64,edge.Darkened(.6f),1);
        }
    }
    private void Signpost(Vector2 p,string upper,string lower,Color wood)
    {
        if(p.DistanceTo(Viewer)>450)return;
        DrawLine(p,p-new Vector2(0,40),wood.Darkened(.3f),4);
        DrawColoredPolygon([p+new Vector2(-20,-38),p+new Vector2(34,-38),p+new Vector2(42,-32),p+new Vector2(34,-26),p+new Vector2(-20,-26)],wood.Darkened(.35f));
        DrawLine(p+new Vector2(-18,-37),p+new Vector2(33,-37),wood,1);
        DrawString(ThemeDB.FallbackFont,p+new Vector2(-30,-49),upper,fontSize:8,modulate:new("e4bf7d"));
        DrawString(ThemeDB.FallbackFont,p+new Vector2(-30,16),lower,fontSize:8,modulate:new("9ab4b1"));
    }
    private void DrawGroundLife(ZoneDefinition z,Rect2 view,Color ground,Color edge,Color phase,bool restored)
    {
        for(var y=Math.Max(30,(int)view.Position.Y/96*96);y<Math.Min(z.Geometry.Bounds.Height-30,view.End.Y+96);y+=96)
            for(var x=Math.Max(30,(int)view.Position.X/96*96);x<Math.Min(z.Geometry.Bounds.Width-30,view.End.X+96);x+=96)
            {
                var hash=Math.Abs(x*17+y*31);var p=new Vector2(x+hash%37,y+hash%29);
                if(z.Geometry.Blocks.Any(b=>new Rect2(b.X-12,b.Y-12,b.Width+24,b.Height+24).HasPoint(p))||z.Sites.Any(s=>p.DistanceTo(new(s.Position.X,s.Position.Y))<50))continue;
                if(hash%5==0)
                {
                    DrawLine(p,p+new Vector2(12,-2),ground.Lightened(.2f),1);
                    DrawLine(p+new Vector2(3,2),p+new Vector2(17,0),ground.Darkened(.5f),2);
                }
                if(z.Region==Region.Glass)
                {
                    for(var i=0;i<5;i++){var a=p+new Vector2(i*3,0);DrawLine(a,a+new Vector2(MathF.Sin(time+i)*2,-7-i%3*4),edge.Darkened(.45f),1);}
                    if(hash%4==0)DrawArc(p+new Vector2(16,12),12,0,Mathf.Pi,16,new Color(edge,.22f),1);
                }
                else if(z.Region==Region.Hollow&&hash%3==0)
                {DrawRect(new(p,new(7,4)),edge.Darkened(.15f));DrawLine(p+new Vector2(1,1),p+new Vector2(5,1),ground,1);}
                else if(hash%3==0)
                {DrawRect(new(p,new(4,3)),edge.Darkened(.5f));DrawRect(new(p+new Vector2(8,4),new(2,2)),ground.Lightened(.15f));}
            }
        foreach(var refuge in z.Sites.Where(s=>s.Kind=="refuge"))
        {
            var p=new Vector2(refuge.Position.X,refuge.Position.Y);if(!view.Grow(80).HasPoint(p))continue;
            var active=Claimed(refuge.Id);var color=active?new Color("efc087"):edge.Darkened(.3f);
            DrawArc(p,44,0,Mathf.Tau,48,new Color(color,.35f),2);
            for(var i=0;i<3;i++)
            {
                var q=p+new Vector2(-48+i*48,36);DrawRect(new(q-new Vector2(6,4),new(12,8)),ground.Darkened(.4f));
                DrawPolyline([q+new Vector2(-11,1),q+new Vector2(0,-16),q+new Vector2(11,1)],color,2);
                if(active){DrawCircle(q-new Vector2(0,4),14,new Color(color,.1f));DrawRect(new(q-new Vector2(1,5),new(2,3)),color);}
            }
        }
    }
    private void DrawWorldArchitecture(Rect2 rect,Region region,Rect2 view,Color ground,Color edge,Color phase)
    {
        DrawRect(new(rect.Position+new Vector2(8,12),rect.Size),new Color(0,0,0,.45f));
        DrawRect(rect,ground.Darkened(.6f));
        var roof=new Rect2(rect.Position,rect.Size-new Vector2(0,24));
        DrawRect(roof,edge.Darkened(.66f));DrawRect(roof.Grow(-5),ground.Darkened(.18f));
        DrawLine(rect.Position,rect.Position+new Vector2(rect.Size.X,0),edge.Darkened(.1f),3);
        var faceY=rect.End.Y-24;
        DrawRect(new(rect.Position.X,faceY,rect.Size.X,24),edge.Darkened(.5f));
        DrawLine(new(rect.Position.X,faceY),new(rect.End.X,faceY),edge,2);
        for(var x=rect.Position.X+10;x<rect.End.X-10;x+=36)
        {
            if(!view.Grow(40).HasPoint(new(x,faceY)))continue;
            DrawRect(new(x,faceY+6,20,12),ground.Darkened(.65f));
            DrawLine(new(x+2,faceY+7),new(x+17,faceY+7),region==Region.Ash?phase:edge.Darkened(.1f),1);
        }
        for(var y=roof.Position.Y+12;y<roof.End.Y-8;y+=24)
            if(y>=view.Position.Y-24&&y<view.End.Y+24)DrawLine(new(roof.Position.X+7,y),new(roof.End.X-7,y),edge.Darkened(.57f),1);
        if(region==Region.Ash)
        {
            for(var x=roof.Position.X+20;x<roof.End.X-16;x+=70)
            {
                var p=new Vector2(x,roof.Position.Y+40);if(!view.Grow(60).HasPoint(p))continue;
                DrawRect(new(p-new Vector2(9,15),new(18,30)),ground.Darkened(.7f));DrawRect(new(p-new Vector2(12,16),new(24,5)),edge.Darkened(.2f));
                for(var i=0;i<4;i++){var rise=Mathf.PosMod(time*12+i*12+x,46);DrawCircle(p+new Vector2(MathF.Sin(time+i)*5,-22-rise),4+rise/7,new Color(phase,.06f*(1-rise/46)));}
            }
            DrawLine(roof.Position+new Vector2(8,10),new(roof.End.X-8,roof.Position.Y+10),phase.Darkened(.4f),5);
        }
        else if(region==Region.Glass)
        {
            DrawRect(roof.Grow(-12),new Color("315453"));
            for(var y=Math.Max(roof.Position.Y+16,view.Position.Y);y<Math.Min(roof.End.Y-12,view.End.Y);y+=20)
                DrawLine(new(roof.Position.X+15,y),new(roof.End.X-15,y-4),new Color("87b7ac",.22f),1);
        }
        else if(region==Region.Hollow)
        {
            for(var x=roof.Position.X+12;x<roof.End.X-8;x+=24)
            {DrawRect(new(x,roof.Position.Y+14,12,Math.Max(1,roof.Size.Y-26)),edge.Darkened(.3f));DrawLine(new(x+3,roof.Position.Y+17),new(x+3,roof.End.Y-12),edge.Darkened(.08f),1);}
        }
        else
        {
            var c=roof.GetCenter();DrawPolyline([c+new Vector2(-30,12),c+new Vector2(-28,-22),c+new Vector2(-10,-9),c+new Vector2(0,-38),c+new Vector2(12,-9),c+new Vector2(28,-22),c+new Vector2(30,12)],edge,3);
            DrawLine(roof.Position+new Vector2(15,10),roof.End-new Vector2(17,15),ground.Darkened(.7f),3);
        }
        var props=PixelAtlas.Load("props");
        var column=region switch{Region.Ash=>1,Region.Glass=>2,Region.Hollow=>3,_=>0};
        for(var x=rect.Position.X+20;x<rect.End.X-20;x+=80)
        {var p=new Vector2(x,rect.End.Y-2);if(view.Grow(40).HasPoint(p))props.Draw(this,column,0,p,Colors.White,.8f);}
    }
}
