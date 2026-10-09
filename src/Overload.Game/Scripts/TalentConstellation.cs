using Godot;
using Overload.Domain;

namespace Overload.Game;

/// <summary>A visual view of the actual three prerequisite chains, with native focusable nodes.</summary>
public partial class TalentConstellation : Control
{
    public CharacterState State { get; init; } = new();
    public Action<string>? Learn { get; init; }
    public int TextPercent { get; init; } = 100;
    private readonly List<Button> nodes = [];
    private readonly List<Label> captions=[];
    private readonly string[] descriptions = ["+5% damage", "+5 stagger", "+4 reach", "+15% damage / −10% Life", "+5% Life", "+10 Armor", "+5 Resistance", "+20% Life / −10% damage", "+2 Focus / second", "Recovery −1 tick", "+5 maximum Focus", "+3 Focus / sec / −5% Life"];
    private readonly string[] names = ["Force", "Ward", "Flow"];
    public override void _Ready()
    {
        CustomMinimumSize = new(0, 366);
        var ids = FrameRules.Talents(State.Frame);
        for (var i = 0; i < ids.Length; i++)
        {
            var index = i; var id = ids[i]; var rank = i % 4; var learned = State.Talents.Contains(id);
            var available = !learned && (rank == 0 || State.Talents.Contains(ids[i - 1])) && State.Talents.Count < FoundationRules.TalentBudget(State) && (rank != 3 || State.Talents.Count(t => t.EndsWith(".4", StringComparison.Ordinal)) < 2);
            var button = new Button { Disabled = !available, TooltipText = $"{names[i/4]} {rank+1}\n{descriptions[i]}\n" + (learned ? "Learned" : available ? "Spend 1 talent point" : "Requires the previous node and an available point"), AccessibilityName = descriptions[i] + (learned ? ". Learned" : ". Talent node") };
            button.AddThemeFontSizeOverride("font_size", (int)(13 * TextPercent / 100f));
            button.Text = "";
            var art=new RelicIcon {Index=i/4==0?0:i/4==1?1:10,Accent=learned?new("8ab99c"):available?RpgTheme.Gold:new("4c453b"),Modulate=learned||available?Colors.White:new(.3f,.3f,.3f)};
            button.AddChild(art);art.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);art.OffsetLeft=4;art.OffsetTop=4;art.OffsetRight=-4;art.OffsetBottom=-4;
            if (learned) button.AddThemeStyleboxOverride("disabled", RpgTheme.Box(new("80baa4"), new("1a2822"), 4));
            if (learned) button.AddThemeColorOverride("font_disabled_color", new Color("b7dbc7"));
            button.Pressed += () => Learn?.Invoke(ids[index]); AddChild(button); nodes.Add(button);
            var caption=new Label {Text=(learned?"LEARNED\n":available?"LEARN • 1 POINT\n":"LOCKED\n")+descriptions[i],AutowrapMode=TextServer.AutowrapMode.WordSmart,MouseFilter=MouseFilterEnum.Ignore};
            caption.AddThemeFontSizeOverride("font_size",(int)(12*TextPercent/100f));caption.AddThemeColorOverride("font_color",learned?new("a6cfb9"):available?RpgTheme.Ink:new("81786a"));AddChild(caption);captions.Add(caption);
        }
        Resized += Arrange; Arrange();
    }
    private Vector2 NodePoint(int index) => new(index / 4 * Size.X / 3+46, 72 + index % 4 * 82);
    private void Arrange()
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            nodes[i].Size=new(60,60);nodes[i].Position=NodePoint(i)-nodes[i].Size/2;
            captions[i].Position=NodePoint(i)+new Vector2(44,-22);captions[i].Size=new(Math.Max(100,Size.X/3-108),60);
        }
        QueueRedraw();
    }
    public override void _Draw()
    {
        var ids = FrameRules.Talents(State.Frame);
        for (var branch = 0; branch < 3; branch++)
        {
            var center = new Vector2((branch + .5f) * Size.X / 3, 22);
            var text = names[branch].ToUpperInvariant(); var size = (int)(17 * TextPercent / 100f);
            DrawString(RpgTheme.Heading, center - new Vector2(RpgTheme.Heading.GetStringSize(text, fontSize: size).X / 2, 0), text, fontSize: size, modulate: RpgTheme.Gold);
            for (var rank = 1; rank < 4; rank++)
            {
                var index = branch * 4 + rank; var color = State.Talents.Contains(ids[index - 1]) ? new Color("8ab99c") : new Color("51483a");
                var from = NodePoint(index - 1); var to = NodePoint(index);
                DrawLine(from, to, color, 2); DrawCircle((from + to) / 2, 4, color);
            }
        }
    }
}
