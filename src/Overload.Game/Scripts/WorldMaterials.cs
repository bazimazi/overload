using System.Text.Json;
using Godot;

namespace Overload.Game;

public static class WorldMaterials
{
    private static Texture2D? earth,stone;
    private static readonly Dictionary<string,(Texture2D Texture,float[][] Frames)> atlases=[];
    private static GradientTexture2D? glow;
    public static Texture2D Earth=>earth??=GD.Load<Texture2D>("res://Assets/Materials/earth.png");
    public static Texture2D Stone=>stone??=GD.Load<Texture2D>("res://Assets/Materials/stone.png");
    public static Texture2D Glow=>glow??=new GradientTexture2D
    {
        Width=128,Height=128,Fill=GradientTexture2D.FillEnum.Radial,FillFrom=new(.5f,.5f),FillTo=new(1,.5f),
        Gradient=new Gradient {Offsets=[0,.3f,.65f,1],Colors=[new(1,1,1,.7f),new(1,1,1,.32f),new(1,1,1,.08f),new(1,1,1,0)]}
    };
    public static void Scenery(CanvasItem canvas,int index,Vector2 foot,float height,Color? tint=null)
        =>Sprite(canvas,"scenery",index,foot,height,tint);
    public static void Sprite(CanvasItem canvas,string name,int index,Vector2 foot,float height,Color? tint=null)
    {
        if(!atlases.TryGetValue(name,out var atlas))
        {
            atlas=(GD.Load<Texture2D>($"res://Assets/Materials/{name}.png"),JsonSerializer.Deserialize<float[][]>(Godot.FileAccess.GetFileAsString($"res://Assets/Materials/{name}.frames.json"))!);
            atlases.Add(name,atlas);
        }
        var f=atlas.Frames[index];var scale=height/f[3];
        canvas.DrawTextureRectRegion(atlas.Texture,new(foot-new Vector2(f[4],f[5])*scale,new Vector2(f[2],f[3])*scale),new(f[0],f[1],f[2],f[3]),tint??Colors.White);
    }
    public static void Light(CanvasItem canvas,Vector2 point,float radius,Color color)
        =>canvas.DrawTextureRect(Glow,new(point-new Vector2(radius,radius*.65f),new(radius*2,radius*1.3f)),false,color);
}
