using Godot;
using Overload.Domain;

namespace Overload.Game;

/// <summary>Raised architecture sorts at its feet, matching the world collision footprint.</summary>
public partial class WorldStructure : Node2D
{
    public Rect2 Footprint { get; init; }
    public Region Region { get; init; }
    private Texture2D texture=null!;
    private PixelAtlas props=null!;
    private Color stone,light;
    private float age;
    public override void _Ready()
    {
        texture=GD.Load<Texture2D>($"res://Assets/Pixel/{Region.ToString().ToLowerInvariant()}.png");props=PixelAtlas.Load("props");
        stone=Region switch{Region.Ash=>new("67513b"),Region.Glass=>new("375f57"),Region.Hollow=>new("5b5269"),_=>new("6e5450")};
        light=Region switch{Region.Ash=>new("e39350"),Region.Glass=>new("85caba"),Region.Hollow=>new("aa93d1"),_=>new("cfad78")};
    }
    public override void _Process(double delta){age+=(float)Math.Min(delta,.05);QueueRedraw();}
    public override void _Draw()
    {
        var rect=Footprint;
        var source=new Rect2(texture.GetWidth()*.26f,texture.GetHeight()*.42f,texture.GetWidth()*.16f,texture.GetHeight()*.28f);
        DrawRect(rect,new Color("111314"));
        for(var y=rect.Position.Y;y<rect.End.Y-26;y+=48)
            for(var x=rect.Position.X;x<rect.End.X;x+=48)
                DrawTextureRectRegion(texture,new(x,y,Math.Min(48,rect.End.X-x),Math.Min(48,rect.End.Y-26-y)),source,new(.66f,.61f,.57f));
        DrawRect(rect.Grow(-5),new Color(stone,.13f));
        DrawRect(new(rect.Position.X,rect.End.Y-26,rect.Size.X,26),stone.Darkened(.46f));
        DrawLine(rect.Position,new(rect.End.X,rect.Position.Y),stone.Lightened(.1f),3);
        DrawLine(new(rect.Position.X,rect.End.Y-26),new(rect.End.X,rect.End.Y-26),stone.Lightened(.22f),3);
        DrawLine(new(rect.Position.X,rect.End.Y-24),new(rect.End.X,rect.End.Y-24),new Color("151310"),1);
        var column=Region switch{Region.Ash=>1,Region.Glass=>2,Region.Hollow=>3,_=>0};
        for(var x=rect.Position.X+18;x<rect.End.X-8;x+=64)
        {
            var foot=new Vector2(x,rect.End.Y+2);
            props.Draw(this,column,1,foot,Colors.White,.65f);
            var window=foot+new Vector2(27,-20);
            DrawRect(new(window-new Vector2(7,5),new(14,13)),new Color("121111"));
            DrawArc(window-new Vector2(0,5),7,Mathf.Pi,Mathf.Tau,16,stone,2);
            DrawLine(window-new Vector2(0,11),window+new Vector2(0,6),stone.Darkened(.2f),2);
            DrawRect(new(window+new Vector2(-4,-3),new(3,7)),new Color(light,.45f+.08f*MathF.Sin(age*2+x)));
            if(Region==Region.Ash||Region==Region.Crown)
            {
                var torch=foot-new Vector2(0,36);DrawCircle(torch,14,new Color(light,.055f));
                DrawRect(new(torch-new Vector2(1,2),new(2,4)),light);
                DrawLine(torch+new Vector2(0,3),torch+new Vector2(0,8),stone,2);
            }
        }
        if(rect.Size.Y>90)
        {
            var center=rect.GetCenter();
            if(Region==Region.Glass)
            {
                for(var i=0;i<4;i++){var p=center+new Vector2((i-1.5f)*22,0);DrawColoredPolygon([p+new Vector2(-8,10),p+new Vector2(0,-34-i%2*12),p+new Vector2(9,9)],stone.Lightened(.1f));DrawLine(p+new Vector2(0,-30),p+new Vector2(1,7),light.Darkened(.2f),1);}
            }
            else
            {
                props.Draw(this,column,2,center+new Vector2(0,12),new Color(.9f,.85f,.8f),1.5f);
                for(var i=0;i<3;i++){var p=center+new Vector2(46+i*22,-26);DrawRect(new(p,new(12,20)),stone.Darkened(.35f));DrawLine(p+new Vector2(3,3),p+new Vector2(9,3),light.Darkened(.4f));}
            }
        }
        if(rect.Size.Y>rect.Size.X*2.2f)
        {
            for(var y=rect.Position.Y+26;y<rect.End.Y;y+=110)
                WorldArt.Draw(this,Region,true,new(rect.GetCenter().X-rect.Size.X/2,y-74,rect.Size.X,96),new(.82f,.82f,.82f));
        }
        else
        {
            var rise=Math.Min(64,rect.Size.Y*.25f);
            WorldArt.Draw(this,Region,false,new(rect.Position-new Vector2(8,rise),rect.Size+new Vector2(16,rise+6)),new(.92f,.9f,.87f));
        }
    }
}
