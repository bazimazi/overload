using Godot;

namespace Overload.Game;

public partial class ArenaHud
{
    private StyleBoxFlat ButtonStyle(Color background, Color border) => new()
    {
        BgColor = background, BorderColor = border, BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
        ContentMarginLeft = 16, ContentMarginRight = 16, ContentMarginTop = 8, ContentMarginBottom = 8,
        CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3, CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3
    };
    private Theme MakeTheme()
    {
        var theme = new Theme { DefaultFontSize = FontSize(18) };
        theme.SetStylebox("normal", "Button", ButtonStyle(new("1e2d33"), new("3d4c4e")));
        theme.SetStylebox("hover", "Button", ButtonStyle(new("2a4147"), gold));
        theme.SetStylebox("pressed", "Button", ButtonStyle(new("365450"), gold));
        theme.SetStylebox("focus", "Button", new StyleBoxFlat { BgColor = Colors.Transparent, BorderColor = gold, BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2 });
        theme.SetColor("font_color", "Button", ink);
        theme.SetColor("font_hover_color", "Button", new("fff0ce"));
        theme.SetStylebox("normal", "LineEdit", ButtonStyle(new("0c151c"), new("55635f")));
        theme.SetColor("font_color", "LineEdit", ink);
        theme.SetColor("font_color", "Label", ink);
        theme.SetConstant("separation", "VBoxContainer", 10);
        return theme;
    }
    private void HomeCard(GridContainer grid, string title, string detail, Action action)
    {
        var button = new Button { Name = "home-" + title.Replace(' ', '-').ToLowerInvariant(), CustomMinimumSize = new(180, 68), SizeFlagsHorizontal = SizeFlags.ExpandFill, FocusMode = FocusModeEnum.All, TooltipText = detail, AccessibilityName = title + ". " + detail };
        var stack = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        button.AddChild(stack); stack.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); stack.OffsetLeft = 12; stack.OffsetRight = -12;
        foreach (var pair in new[] { (title, 16, ink), (detail, 12, muted) })
        {
            var label = new Label { Text = pair.Item1, HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis };
            label.AddThemeFontSizeOverride("font_size", FontSize(pair.Item2)); label.AddThemeColorOverride("font_color", pair.Item3); stack.AddChild(label);
        }
        button.Pressed += () => { arena.Audio.Play("ui", "UI"); action(); }; grid.AddChild(button);
    }
    private void HomeLink(HBoxContainer row, string text, Action action)
    {
        var button = new Button { Text = text, Flat = true, FocusMode = FocusModeEnum.All, CustomMinimumSize = new(70, 32) };
        button.AddThemeFontSizeOverride("font_size", FontSize(13)); button.Pressed += () => { arena.Audio.Play("ui", "UI"); action(); }; row.AddChild(button);
    }
}
