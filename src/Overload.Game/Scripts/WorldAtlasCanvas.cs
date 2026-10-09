using Godot;
using Overload.Domain;

namespace Overload.Game;

/// <summary>The painted atlas hosts real discovered zone nodes and safe-waypoint selection.</summary>
public partial class WorldAtlasCanvas : Control
{
    public WorldProgress Progress { get; init; } = new();
    public string SelectedZone { get; init; } = "hearth";
    public Action<string>? SelectZone { get; init; }
    public Action<Region>? SelectRegion { get; init; }
    public Region SelectedRegion { get; init; }
    public int TextPercent { get; init; } = 100;
    private Texture2D atlas = null!;
    private readonly Dictionary<string, Button> markers = [];
    private readonly Vector2[] roots = [new(.28f,.67f),new(.72f,.67f),new(.28f,.34f),new(.72f,.34f)];
    private Vector2 Point(string id)
    {
        if(id=="hearth")return Size*new Vector2(.5f,.51f);
        var zone=WorldContent.Zone(id);var root=roots[(int)zone.Region];
        var rank=int.Parse(id.Split('.')[1]);var side=root.X<.5f?-1:1;var vertical=root.Y<.5f?-1:1;
        var offset=rank switch {1=>new Vector2(side*.12f,-vertical*.01f),2=>new Vector2(side*.15f,vertical*.16f),3=>new Vector2(0,vertical*.2f),_=>Vector2.Zero};
        return Size*(root+offset);
    }
    public override void _Ready()
    {
        CustomMinimumSize=new(0,350);TextureFilter=TextureFilterEnum.Linear;
        atlas=GD.Load<Texture2D>("res://Assets/UI/palimpsest-atlas.png");
        foreach(var zone in WorldContent.Zones.Values)
        {
            if(SelectRegion is not null&&zone.Id!="hearth"&&!zone.Id.EndsWith(".0",StringComparison.Ordinal))continue;
            var known=SelectRegion is not null||Progress.Discovered.Contains(zone.Id)||zone.Id=="hearth";
            var selected=SelectRegion is not null?zone.Region==SelectedRegion&&zone.Id!="hearth":zone.Id==SelectedZone;
            var b=new Button {Text=zone.Id=="hearth"?"◆":zone.Kind=="boss"?"♜":zone.Kind=="dungeon"?"◇":"●",Disabled=!known||zone.Id=="hearth"&&SelectRegion is not null,TooltipText=known?zone.Name+"\n"+(Progress.Visited.Contains(zone.Id)?"Visited":"Discovered"):"Uncharted road",AccessibilityName=known?zone.Name:"Uncharted road"};
            b.AddThemeStyleboxOverride("normal",RpgTheme.Box(selected?new("e6cc8c"):known?RpgTheme.Gold:new("4a4439"),new("15120e"),2));
            b.AddThemeStyleboxOverride("disabled",RpgTheme.Box(new("4a4439"),new("15120e"),2));
            b.AddThemeColorOverride("font_color",selected?new("fff0b6"):RpgTheme.Gold);
            var id=zone.Id;b.Pressed+=()=>{if(SelectRegion is not null)SelectRegion(zone.Region);else SelectZone?.Invoke(id);};
            AddChild(b);markers[id]=b;
        }
        Resized+=Arrange;Arrange();
    }
    private void Arrange()
    {
        foreach(var (id,b) in markers){b.Size=new(30,30);b.Position=Point(id)-b.Size/2;}
        QueueRedraw();
    }
    public override void _Draw()
    {
        DrawTextureRect(atlas,new(Vector2.Zero,Size),false,new(.65f,.62f,.57f));
        DrawRect(new(Vector2.Zero,Size),RpgTheme.Gold.Darkened(.35f),false,2);
        foreach(var zone in WorldContent.Zones.Values)
        {
            if(SelectRegion is not null&&zone.Id!="hearth"&&!zone.Id.EndsWith(".0",StringComparison.Ordinal))continue;
            foreach(var exit in zone.Exits)
            {
                if(!markers.ContainsKey(exit.Destination))continue;
                var a=Point(zone.Id);var b=Point(exit.Destination);
                if(string.CompareOrdinal(zone.Id,exit.Destination)>0)continue;
                var known=SelectRegion is not null||Progress.Discovered.Contains(zone.Id)&&Progress.Discovered.Contains(exit.Destination);
                DrawLine(a,b,new Color(0,0,0,.7f),5);
                if(known)DrawLine(a,b,new Color("b3996b",.7f),1.5f);
                else for(var i=0;i<10;i+=2)DrawLine(a.Lerp(b,i/10f),a.Lerp(b,(i+1)/10f),new Color("766650",.6f));
            }
        }
        for(var i=0;i<4;i++)
        {
            var p=Size*roots[i]+new Vector2(0,roots[i].Y<.5f?36:-26);var text=Arena.RegionName((Region)i).ToUpperInvariant();
            var size=(int)(14*TextPercent/100f);var width=RpgTheme.Heading.GetStringSize(text,fontSize:size).X;
            DrawString(RpgTheme.Heading,p-new Vector2(width/2,0)+Vector2.One,text,fontSize:size,modulate:Colors.Black);
            DrawString(RpgTheme.Heading,p-new Vector2(width/2,0),text,fontSize:size,modulate:RpgTheme.Ink);
        }
        var current=SelectRegion is null?SelectedZone:WorldContent.Id(SelectedRegion,0);
        var point=Point(current);DrawArc(point,22,0,Mathf.Tau,48,new Color("e4c785"),1.5f);
        DrawString(RpgTheme.Heading,new(16,Size.Y-14),SelectRegion is null?"THE ROADS THAT REMAIN":"THE FRACTURE ATLAS",fontSize:13,modulate:RpgTheme.Gold);
    }
}
