using Godot;
using Overload.Domain;

namespace Overload.Game;

public static class WorldArt
{
    private static Texture2D? sheet;
    public static void Draw(CanvasItem canvas, Region region, bool detail, Rect2 destination, Color? tint=null)
    {
        sheet??=GD.Load<Texture2D>("res://Assets/Pixel/world-structures.png");
        // The generated skyline's first row ends at y=540, not the nominal y=512.
        var width=sheet.GetWidth()/4f;var split=sheet.GetHeight()*540/1024f;
        var source=new Rect2((int)region*width,detail?split:0,width,detail?sheet.GetHeight()-split:split);
        canvas.DrawTextureRectRegion(sheet,destination,source,tint??Colors.White);
    }
}
