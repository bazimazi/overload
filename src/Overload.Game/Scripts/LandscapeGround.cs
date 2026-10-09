using Godot;
using Overload.Domain;

namespace Overload.Game;

/// <summary>One world-aligned surface blends materials without submitting a floor tile grid.</summary>
public partial class LandscapeGround : Node2D
{
    private ShaderMaterial surface=null!;
    private Rect2 bounds;
    public override void _Ready()
    {
        surface=new ShaderMaterial {Shader=GD.Load<Shader>("res://Assets/Materials/world-material.gdshader")};
        foreach(var name in new[]{"earth","moss","stone"})surface.SetShaderParameter(name+"_material",GD.Load<Texture2D>($"res://Assets/Materials/{name}.png"));
        Material=surface;Hide();
    }
    public void Configure(ZoneDefinition? zone)
    {
        if(zone is null){Hide();return;}
        var b=zone.Geometry.Bounds;bounds=new(b.X,b.Y,b.Width,b.Height);
        surface.SetShaderParameter("bounds",new Vector4(b.X,b.Y,b.Width,b.Height));
        surface.SetShaderParameter("biome",(int)zone.Region);
        surface.SetShaderParameter("outdoors",zone.Kind is "wild" or "frontier");
        Show();QueueRedraw();
    }
    public void ObservePlayer(Vector2 position)=>surface.SetShaderParameter("player_position",position);
    public override void _Draw()=>DrawRect(bounds.Grow(64),Colors.White);
}
