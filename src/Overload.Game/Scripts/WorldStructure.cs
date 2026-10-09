using Godot;
using Overload.Domain;

namespace Overload.Game;

/// <summary>Retained solid scenery occupies the authoritative footprint and fades above the player.</summary>
public partial class WorldStructure : Node2D
{
    public Rect2 Footprint {get;init;}
    public Region Region {get;init;}
    public bool Outdoors {get;init;}
    public bool Settlement {get;init;}
    private Color stone;
    private WorldStructureLight lights=null!;
    public override void _Ready()
    {
        TextureFilter=TextureFilterEnum.Linear;
        TextureRepeat=TextureRepeatEnum.Mirror;
        stone=Region switch {Region.Glass=>new(.71f,.86f,.79f),Region.Hollow=>new(.79f,.77f,.88f),Region.Crown=>new(.87f,.75f,.72f),_=>new(.85f,.81f,.73f)};
        lights=new WorldStructureLight {Footprint=Footprint,Region=Region,Outdoors=Outdoors};
        AddChild(lights);QueueRedraw();
    }
    public void Observe(Vector2 viewer,Vector2 player,double delta,bool paused)
    {
        Visible=Footprint.Grow(150).Intersects(new(viewer-new Vector2(450,300),new(900,600)));
        var roof=new Rect2(Footprint.Position-new Vector2(16,100),Footprint.Size+new Vector2(32,100));
        var hide=roof.HasPoint(player-new Vector2(0,24))&&player.Y<Footprint.End.Y;
        SelfModulate=new Color(1,1,1,Mathf.Lerp(SelfModulate.A,hide?.38f:1,1-MathF.Exp(-(float)delta*12)));
        lights.Advance(delta,paused);
    }
    public override void _Draw()
    {
        var r=Footprint;var rise=Outdoors?0:34;
        var top=new Rect2(r.Position-new Vector2(0,rise),r.Size);
        DrawColoredPolygon([r.Position+new Vector2(6,6),r.Position+new Vector2(r.Size.X+32,13),r.End+new Vector2(32,16),new(r.Position.X+22,r.End.Y+16)],new Color(0,0,0,.34f));
        if(Settlement)
        {
            WorldMaterials.Sprite(this,"settlement",r.Position.X<600?0:1,new(r.GetCenter().X,r.End.Y),r.Size.X*1.15f,stone);
            return;
        }
        DrawRect(r,new Color("171a19"));
        Vector2[] roof=[top.Position,new(top.End.X,top.Position.Y),top.End,new(top.Position.X,top.End.Y)];
        DrawPolygon(roof,new Color[]{stone.Darkened(Outdoors?.36f:.16f)},roof.Select(p=>p/(Outdoors?256:165)).ToArray(),Outdoors?WorldMaterials.Earth:WorldMaterials.Stone);
        if(!Outdoors)
        {
            DrawTextureRectRegion(WorldMaterials.Stone,new(r.Position.X,top.End.Y,r.Size.X,rise),new(0,850,1254,350),new(.25f,.27f,.24f));
            DrawLine(new(top.Position.X,top.End.Y),top.End,stone.Darkened(.5f),2);
            DrawLine(top.Position,new(top.End.X,top.Position.Y),new Color(stone,.4f),1);
        }
        for(var x=r.Position.X+12;x<r.End.X;x+=Outdoors?52:40)
        {
            DrawLine(new(x,top.End.Y+2),new(x,r.End.Y),stone.Darkened(.75f),1);
            var seed=(uint)((int)x*73856093);
            WorldMaterials.Scenery(this,Outdoors?Region==Region.Glass?1:0:2,new(x+(Outdoors?seed%17-8f:0),r.End.Y),Outdoors?24+seed%16:45,stone);
        }
        for(var y=top.Position.Y+34;y<top.End.Y-12;y+=76)
            for(var x=top.Position.X+34;x<top.End.X-12;x+=80)
            {
                var seed=(uint)((int)x*73856093)^(uint)((int)y*19349663);
                var id=Outdoors?Region is Region.Glass or Region.Hollow?4:5:seed%3==0?11:3;
                var height=Outdoors?Region is Region.Glass or Region.Hollow?76+seed%27:58+seed%24:40+seed%12;
                WorldMaterials.Scenery(this,id,new(x+(seed%25)-12,y+((seed>>8)%19)-9),height,stone);
            }
        if(Outdoors)
            for(var x=r.Position.X+32;x<r.End.X-20;x+=72)
            {
                var seed=(uint)((int)x*19349663);var lush=Region is Region.Glass or Region.Hollow;
                WorldMaterials.Scenery(this,lush?4:5,new(x,r.End.Y-12-seed%12),lush?76+seed%25:58+seed%22,stone);
                WorldMaterials.Scenery(this,Region==Region.Glass?1:0,new(x+18,r.End.Y),26+seed%12,stone);
            }
        if(!Outdoors)
        {
            for(var x=r.Position.X+38;x<r.End.X-12;x+=100)
            {
                var foot=new Vector2(x,r.End.Y-5);
                WorldMaterials.Scenery(this,8,foot,37);
                WorldMaterials.Scenery(this,9,foot-new Vector2(28,0),24,stone);
            }
        }
    }
}

public partial class WorldStructureLight : Node2D
{
    public Rect2 Footprint {get;init;}
    public Region Region {get;init;}
    public bool Outdoors {get;init;}
    private float age;
    public override void _Ready()=>Material=new CanvasItemMaterial {BlendMode=CanvasItemMaterial.BlendModeEnum.Add};
    public void Advance(double delta,bool paused){if(!paused)age+=(float)Math.Min(delta,.05);if(!Outdoors&&IsVisibleInTree())QueueRedraw();}
    public override void _Draw()
    {
        if(Outdoors)return;
        var color=Region switch {Region.Glass=>new Color(.32f,.6f,.54f),Region.Hollow=>new(.42f,.32f,.6f),_=>new(.9f,.49f,.17f)};
        for(var x=Footprint.Position.X+38;x<Footprint.End.X-12;x+=100)
            WorldMaterials.Light(this,new(x,Footprint.End.Y-24),56,color*(.34f+.025f*MathF.Sin(age*4+x)));
    }
}
