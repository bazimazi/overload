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
            // Reuse the authored material rather than stretching a flat strip across the landscape.
            var texture=backgrounds[region?.ToString().ToLowerInvariant()??"court"];
            var source=new Rect2(texture.GetWidth()*.36f,texture.GetHeight()*.51f,texture.GetWidth()*.13f,texture.GetHeight()*.13f);
            for(var distance=0f;distance<length;distance+=32)
            {
                var point=a+tangent*distance;if(!view.Grow(70).HasPoint(point))continue;
                DrawSetTransform(point,tangent.Angle());
                DrawTextureRectRegion(texture,new(0,-27,Math.Min(32,length-distance),54),source,new Color(.69f,.63f,.55f));
                DrawSetTransform(Vector2.Zero);
            }
            for(var d=8f;d<length;d+=upper?18:28)
            {
                var p=(a+tangent*d).Round();if(!view.Grow(35).HasPoint(p))continue;
                DrawLine(p-side*27,p+side*27,new Color(edge.Darkened(.55f),.65f),1);
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
                for(var distance=14f;distance<length;distance+=64)
                {
                    var post=a+tangent*distance;if(!view.Grow(60).HasPoint(post))continue;
                    foreach(var sign in new[]{-1,1})
                    {
                        var point=post+side*(sign*33);
                        DrawRect(new(point-new Vector2(3,8),new(6,10)),ground.Darkened(.5f));
                        DrawRect(new(point-new Vector2(4,9),new(8,3)),edge.Darkened(.2f));
                        DrawRect(new(point-new Vector2(1,7),new(2,2)),phase.Lightened(.25f));
                    }
                }
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
        DrawLine(p+new Vector2(2,2),p+new Vector2(2,-32),new Color(0,0,0,.3f),3);
        DrawLine(p,p-new Vector2(0,34),wood.Darkened(.5f),3);
        DrawColoredPolygon([p+new Vector2(-14,-32),p+new Vector2(18,-32),p+new Vector2(24,-27),p+new Vector2(18,-22),p+new Vector2(-14,-22)],wood.Darkened(.4f));
        DrawLine(p+new Vector2(-12,-31),p+new Vector2(17,-31),wood,1);
        DrawLine(p+new Vector2(-10,-24),p+new Vector2(15,-24),wood.Darkened(.65f),1);
        DrawCircle(p-new Vector2(0,27),1,wood.Lightened(.3f));
        WorldNameplate(p-new Vector2(0,43),upper,new("e4bf7d"),p,100);
        WorldNameplate(p+new Vector2(0,20),lower,new("9ab4b1"),p,100);
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
                if(hash%7==0)WorldArt.Draw(this,z.Region,true,new(p-new Vector2(16,32),new(32,40)),new(.72f,.72f,.72f));
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
}
