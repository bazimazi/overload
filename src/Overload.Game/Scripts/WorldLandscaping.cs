using Godot;
using Overload.Domain;

namespace Overload.Game;

/// <summary>Materials follow navigable routes; tall scenery belongs to authoritative solid footprints.</summary>
public partial class WorldView
{
    private Dictionary<(Vector2,Vector2),Vector2[]> roadPaths=[];
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
            if(routed.Count>0&&routed[^1]!=path[0]){DrawRoadSegments([..routed],view);routed.Clear();}
            foreach(var p in path)if(routed.Count==0||routed[^1]!=p)routed.Add(p);
        }
        DrawRoadSegments([..routed],view);
    }
    private void DrawRoadSegments(Vector2[] points,Rect2 view)
    {
        var outside=Exploration?.Kind is "wild" or "frontier";
        var tint=outside?region switch
        {
            Region.Glass=>new Color(.91f,.93f,.81f,.85f),Region.Hollow=>new Color(.93f,.87f,.93f,.85f),
            Region.Crown=>new Color(1f,.87f,.87f,.85f),_=>new Color(1.08f,1.02f,.88f,.85f)
        }:new Color(.91f,.84f,.70f,.8f);
        for(var i=1;i<points.Length;i++)
        {
            var a=points[i-1];var b=points[i];
            if(a==b||!new Rect2(a.Min(b)-new Vector2(44,44),(a-b).Abs()+new Vector2(88,88)).Intersects(view))continue;
            DrawMaterialPath(a,b,outside?WorldMaterials.Earth:WorldMaterials.Stone,tint,62);
        }
    }
    private void DrawMaterialPath(Vector2 a,Vector2 b,Texture2D texture,Color color,float width)
    {
        var side=a.DirectionTo(b).Orthogonal();var inner=width*.32f;var outer=width*.5f;var clear=new Color(color,0);
        void Band(float left,float right,Color lc,Color rc)
        {
            Vector2[] points=[a+side*left,b+side*left,b+side*right,a+side*right];
            DrawPolygon(points,new Color[]{lc,lc,rc,rc},points.Select(p=>p/230).ToArray(),texture);
        }
        Band(-outer,-inner,clear,color);Band(-inner,inner,color,color);Band(inner,outer,color,clear);
        foreach(var center in new[]{a,b})
            for(var i=0;i<16;i++)
            {
                Vector2[] triangle=[center,center+Vector2.FromAngle(i*Mathf.Tau/16)*outer,center+Vector2.FromAngle((i+1)*Mathf.Tau/16)*outer];
                DrawPolygon(triangle,new Color[]{color,clear,clear},triangle.Select(v=>v/230).ToArray(),texture);
            }
    }
    private void DrawWorldRoads(ZoneDefinition z,Rect2 view,Color ground,Color edge,Color phase)
    {
        if(z.Kind=="wild")
        {
            var factor=z.Geometry.Bounds.Width/1920f;
            Vector2[] Scale(Vector2[] points)=>[..points.Select(p=>p*factor)];
            Road(Scale([new(120,560),new(300,560),new(600,300),new(1540,300),new(1740,560),new(1790,560)]),view,ground,edge,phase,true);
            var lower=z.Region is Region.Glass or Region.Crown?1000:850;
            Road(Scale(z.Region is Region.Glass or Region.Crown?[new(300,560),new(180,700),new(180,1000),new(690,lower),new(1210,lower),new(1600,810),new(1740,560)]:[new(300,560),new(270,700),new(690,lower),new(1210,lower),new(1600,810),new(1740,560)]),view,ground,edge,phase,false);
            Road(Scale([new(940,lower),new(940,1020)]),view,ground,edge,phase,false);
            foreach(var exit in z.Exits)Road([new Vector2(1740,560)*factor,new(exit.Position.X,exit.Position.Y)],view,ground,edge,phase,false);
            Signpost(new Vector2(470,540)*factor,z.Region==Region.Ash?"GANTRY":"UPPER ROAD","REFUGE / LOWER ROAD",edge);
            Signpost(new Vector2(1510,370)*factor,WorldContent.Places[(int)z.Region][1].ToUpperInvariant(),WorldContent.Places[(int)z.Region][2].ToUpperInvariant(),edge);
        }
        else if(z.Kind=="frontier")
        {
            Road([new(100,1920),new(3000,1920),new(6034,1920)],view,ground,edge,phase,true);
            Road([new(500,1920),new(500,850),new(5300,850),new(5300,1920)],view,ground,edge,phase,false);
            Road([new(500,1920),new(500,3500),new(5300,3500),new(5300,1920)],view,ground,edge,phase,false);
            Road([new(3000,850),new(3000,3500)],view,ground,edge,phase,false);
            Signpost(new(520,1860),"EAST / UNCHARTED WILDERNESS","SIDE TRAILS / CARAVANS & RELICS",edge);
            Signpost(new(3000,1860),"TRAILKEEPER CAMP","HEARTH RETURN CONDUIT",edge);
        }
        else if(z.Kind=="hearth")
        {
            foreach(var exit in z.Exits)Road([new(640,384),new(exit.Position.X,exit.Position.Y)],view,ground,edge,phase,false);
            DrawArc(new(640,384),78,0,Mathf.Tau,64,edge.Darkened(.4f),3);
            DrawArc(new(640,384),72,0,Mathf.Tau,64,edge.Darkened(.6f),1);
        }
    }
    private void DrawLandscapeScenery(ZoneDefinition z,Rect2 view,Color edge)
    {
        if(z.Kind is not ("wild" or "frontier"))return;
        for(var y=Math.Max(100,(int)view.Position.Y/160*160);y<Math.Min(z.Geometry.Bounds.Height-80,view.End.Y+160);y+=160)
            for(var x=Math.Max(100,(int)view.Position.X/160*160);x<Math.Min(z.Geometry.Bounds.Width-80,view.End.X+160);x+=160)
            {
                var hash=(uint)(x*73856093)^(uint)(y*19349663);if(hash%3!=0)continue;
                var p=new Vector2(x+hash%61,y+(hash>>8)%47);
                if(z.Geometry.Blocks.Any(b=>new Rect2(b.X-30,b.Y-30,b.Width+60,b.Height+60).HasPoint(p))
                    ||z.Sites.Any(s=>p.DistanceTo(new(s.Position.X,s.Position.Y))<90)
                    ||roadPaths.Values.Any(path=>Enumerable.Range(1,Math.Max(0,path.Length-1)).Any(i=>Geometry2D.GetClosestPointToSegment(p,path[i-1],path[i]).DistanceTo(p)<70)))continue;
                WorldMaterials.Scenery(this,z.Region is Region.Glass or Region.Hollow?6:7,p,12+hash%9,new Color(.83f,.85f,.75f,.72f));
                if(hash%4==0)WorldMaterials.Scenery(this,3,p+new Vector2(14,3),11,new Color(.67f,.67f,.62f,.7f));
            }
    }
    private void Signpost(Vector2 p,string upper,string lower,Color wood)
    {
        if(p.DistanceTo(Viewer)>450)return;
        DrawLine(p+new Vector2(2,2),p+new Vector2(2,-32),new Color(0,0,0,.3f),3);
        DrawLine(p,p-new Vector2(0,34),wood.Darkened(.5f),3);
        DrawColoredPolygon([p+new Vector2(-14,-32),p+new Vector2(18,-32),p+new Vector2(24,-27),p+new Vector2(18,-22),p+new Vector2(-14,-22)],wood.Darkened(.4f));
        DrawLine(p+new Vector2(-12,-31),p+new Vector2(17,-31),wood,1);
        WorldNameplate(p-new Vector2(0,43),upper,new("e4bf7d"),p,100);
        WorldNameplate(p+new Vector2(0,20),lower,new("9ab4b1"),p,100);
    }
    private void DrawGroundLife(ZoneDefinition z,Rect2 view,Color ground,Color edge,Color phase,bool restored)
    {
        foreach(var refuge in z.Sites.Where(s=>s.Kind=="refuge"))
        {
            var p=new Vector2(refuge.Position.X,refuge.Position.Y);if(!view.Grow(80).HasPoint(p))continue;
            var active=Claimed(refuge.Id);var color=active?new Color("efc087"):edge.Darkened(.3f);
            DrawArc(p,44,0,Mathf.Tau,48,new Color(color,.35f),2);
            for(var i=0;i<3;i++)
            {
                var q=p+new Vector2(-48+i*48,36);
                WorldMaterials.Scenery(this,9,q,18,new(.74f,.72f,.65f));
                if(active)WorldMaterials.Light(this,q-new Vector2(0,8),36,new Color(color,.2f));
            }
        }
    }
    private void DrawGroundWind(ZoneDefinition z,Rect2 view,Color edge)
    {
        if(z.Region!=Region.Glass)return;
        for(var i=0;i<12;i++)
        {
            var p=Viewer+new Vector2(i*137%640-320,Mathf.PosMod(i*89-time*2,360)-180);
            DrawLine(p,p+new Vector2(MathF.Sin(time+i)*3,-1),new Color(edge,.18f));
        }
    }
}
