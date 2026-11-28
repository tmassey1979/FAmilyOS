using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;

namespace FamilyOS.Maui.Controls;

/// <summary>
/// Shared Merge-2 command-center UI language for all MAUI pages.
/// Deep charcoal panels, cyan brackets, pills, compact actions, module tiles.
/// </summary>
public static class Ui
{
    public static Color C(string key, string fallback = "#94A3B8")
    {
        if (Application.Current?.Resources.TryGetValue(key, out var v) == true && v is Color c)
            return c;
        return Color.FromArgb(fallback);
    }

    public static Label Display(string text) => ThemedLabel(text, 28, FontAttributes.Bold, "TextPrimary");
    public static Label Title(string text) => ThemedLabel(text, 20, FontAttributes.Bold, "TextPrimary");
    public static Label Heading(string text) => ThemedLabel(text, 16, FontAttributes.Bold, "TextPrimary");
    public static Label Body(string text) => ThemedLabel(text, 14, FontAttributes.None, "TextPrimary");
    public static Label Caption(string text) => ThemedLabel(text, 12, FontAttributes.None, "TextMuted");

    public static Label Section(string text)
    {
        var label = ThemedLabel(text.ToUpperInvariant(), 11, FontAttributes.Bold, "AccentGlow");
        label.CharacterSpacing = 1.6;
        label.Margin = new Thickness(2, 14, 0, 6);
        return label;
    }

    public static Label ThemedLabel(string text, double size, FontAttributes attrs, string colorKey)
    {
        var label = new Label
        {
            Text = text,
            FontSize = size,
            FontAttributes = attrs,
            LineBreakMode = LineBreakMode.WordWrap
        };
        label.SetDynamicResource(Label.TextColorProperty, colorKey);
        return label;
    }

    public static View Pill(string text, string tone = "info")
    {
        var (bgKey, fgKey) = tone.ToLowerInvariant() switch
        {
            "success" => ("AccentSuccess", "TextOnAccent"),
            "warning" => ("AccentWarning", "TextOnAccent"),
            "danger" => ("AccentDanger", "TextOnAccent"),
            "violet" or "accent" => ("AccentSecondary", "TextPrimary"),
            "muted" or "neutral" => ("BgSurfaceAlt", "TextMuted"),
            _ => ("AccentPrimary", "TextOnAccent"),
        };

        var label = new Label
        {
            Text = text,
            FontSize = 11,
            FontAttributes = FontAttributes.Bold,
            VerticalTextAlignment = TextAlignment.Center,
            HorizontalTextAlignment = TextAlignment.Center
        };
        label.SetDynamicResource(Label.TextColorProperty, fgKey);

        var border = new Border
        {
            Padding = new Thickness(10, 4),
            StrokeThickness = 0,
            Content = label,
            StrokeShape = new RoundRectangle { CornerRadius = 999 }
        };
        border.SetDynamicResource(VisualElement.BackgroundColorProperty, bgKey);
        return border;
    }

    public static View PillRow(params (string text, string tone)[] pills)
    {
        var row = new FlexLayout
        {
            Wrap = FlexWrap.Wrap,
            Direction = FlexDirection.Row,
            AlignItems = FlexAlignItems.Center
        };
        foreach (var (text, tone) in pills)
        {
            var p = Pill(text, tone);
            p.Margin = new Thickness(0, 0, 6, 6);
            row.Children.Add(p);
        }
        return row;
    }

    public static Border Card(View content, bool accent = false, string? edgeTone = null)
    {
        var border = new Border
        {
            Padding = 16,
            Content = content,
            StrokeThickness = accent || edgeTone is not null ? 1.5 : 1,
            StrokeShape = new RoundRectangle { CornerRadius = 16 }
        };
        border.SetDynamicResource(VisualElement.BackgroundColorProperty, "BgSurface");
        var strokeKey = edgeTone?.ToLowerInvariant() switch
        {
            "danger" => "AccentDanger",
            "warning" => "AccentWarning",
            "success" => "AccentSuccess",
            "violet" => "AccentSecondary",
            _ when accent => "AccentPrimary",
            _ => "BorderSubtle"
        };
        border.SetDynamicResource(Border.StrokeProperty, strokeKey);
        return border;
    }

    public static View ListCard(View content, string? railTone = null, bool accent = false)
    {
        var rail = new BoxView
        {
            WidthRequest = 3,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Fill
        };
        var tone = railTone?.ToLowerInvariant();
        var railKey = tone switch
        {
            "danger" => "AccentDanger",
            "warning" => "AccentWarning",
            "success" => "AccentSuccess",
            "violet" or "accent" => "AccentSecondary",
            "info" or "primary" => "AccentPrimary",
            "muted" => "BorderSubtle",
            _ => accent ? "AccentPrimary" : "BorderSubtle"
        };
        rail.SetDynamicResource(BoxView.ColorProperty, railKey);

        var inner = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 12
        };
        inner.Children.Add(rail);
        var body = new ContentView { Content = content, Padding = new Thickness(0, 2, 0, 2) };
        Grid.SetColumn(body, 1);
        inner.Children.Add(body);

        return Card(inner, accent: accent, edgeTone: tone is "danger" or "warning" or "success" ? tone : null);
    }

    public static Border TacticalPanel(View content)
    {
        var border = Card(content, accent: true);
        return border;
    }

    public static View PageHeader(string title, string? subtitle = null, params View[] trailing)
    {
        var titleLabel = Display(title);
        titleLabel.FontSize = 26;

        var left = new VerticalStackLayout { Spacing = 2, Children = { titleLabel } };
        if (!string.IsNullOrWhiteSpace(subtitle))
            left.Children.Add(Caption(subtitle!));

        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12
        };
        row.Children.Add(left);
        if (trailing.Length > 0)
        {
            var trail = new HorizontalStackLayout { Spacing = 8, VerticalOptions = LayoutOptions.Center };
            foreach (var v in trailing) trail.Children.Add(v);
            Grid.SetColumn(trail, 1);
            row.Children.Add(trail);
        }

        var accent = new BoxView { HeightRequest = 1.5, Opacity = 0.55, Margin = new Thickness(0, 10, 0, 0) };
        accent.SetDynamicResource(BoxView.ColorProperty, "AccentPrimary");

        return new VerticalStackLayout { Spacing = 0, Children = { row, accent } };
    }

    public static Button Action(string text, bool primary = false)
    {
        var btn = new Button
        {
            Text = text,
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            Padding = new Thickness(16, 0),
            MinimumHeightRequest = 40,
            CornerRadius = 12,
            Margin = new Thickness(0, 4, 8, 0)
        };

        if (primary)
        {
            btn.SetDynamicResource(Button.BackgroundColorProperty, "AccentPrimary");
            btn.SetDynamicResource(Button.TextColorProperty, "TextOnAccent");
            btn.BorderWidth = 0;
        }
        else
        {
            btn.SetDynamicResource(Button.BackgroundColorProperty, "BgSurfaceAlt");
            btn.SetDynamicResource(Button.TextColorProperty, "AccentGlow");
            btn.SetDynamicResource(Button.BorderColorProperty, "AccentPrimary");
            btn.BorderWidth = 1;
            btn.Opacity = 0.95;
        }

        return btn;
    }

    public static HorizontalStackLayout ActionRow() =>
        new() { Spacing = 0 };

    public static Button PrimaryCta(string text)
    {
        var btn = new Button
        {
            Text = text,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            MinimumHeightRequest = 52,
            CornerRadius = 14,
            Padding = new Thickness(28, 0)
        };
        btn.SetDynamicResource(Button.BackgroundColorProperty, "AccentPrimary");
        btn.SetDynamicResource(Button.TextColorProperty, "TextOnAccent");
        return btn;
    }

    public static Button SecondaryCta(string text)
    {
        var btn = new Button
        {
            Text = text,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            MinimumHeightRequest = 52,
            CornerRadius = 14,
            Padding = new Thickness(28, 0),
            BorderWidth = 1.5
        };
        btn.SetDynamicResource(Button.BackgroundColorProperty, "BgSurfaceAlt");
        btn.SetDynamicResource(Button.TextColorProperty, "AccentGlow");
        btn.SetDynamicResource(Button.BorderColorProperty, "AccentPrimary");
        return btn;
    }

    public static Button Ghost(string text)
    {
        var btn = new Button
        {
            Text = text,
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            MinimumHeightRequest = 48,
            BackgroundColor = Colors.Transparent,
            BorderWidth = 0
        };
        btn.SetDynamicResource(Button.TextColorProperty, "AccentGlow");
        return btn;
    }

    public static Button DangerAction(string text)
    {
        var btn = new Button
        {
            Text = text,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            MinimumHeightRequest = 52,
            CornerRadius = 14,
            Padding = new Thickness(28, 0)
        };
        btn.SetDynamicResource(Button.BackgroundColorProperty, "AccentDanger");
        btn.SetDynamicResource(Button.TextColorProperty, "TextOnAccent");
        return btn;
    }

    public static View NavRow(string title, string subtitle, EventHandler onTap)
    {
        var stack = new VerticalStackLayout
        {
            Spacing = 2,
            Children =
            {
                Heading(title),
                Caption(subtitle)
            }
        };
        var chevron = ThemedLabel("›", 22, FontAttributes.Bold, "AccentPrimary");
        chevron.VerticalTextAlignment = TextAlignment.Center;

        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12,
            Padding = new Thickness(4, 2)
        };
        row.Children.Add(stack);
        Grid.SetColumn(chevron, 1);
        row.Children.Add(chevron);

        var card = Card(row);
        var tap = new TapGestureRecognizer();
        tap.Tapped += (s, e) => onTap(s, e);
        card.GestureRecognizers.Add(tap);
        return card;
    }

    public static View Empty(string title, string body)
    {
        var stack = new VerticalStackLayout
        {
            Spacing = 10,
            Padding = new Thickness(16, 28),
            HorizontalOptions = LayoutOptions.Center,
            Children =
            {
                ThemedLabel("◇", 36, FontAttributes.None, "AccentPrimary"),
                Heading(title),
                Caption(body)
            }
        };
        foreach (var child in stack.Children.OfType<Label>())
            child.HorizontalTextAlignment = TextAlignment.Center;
        return Card(stack);
    }

    public static View PageScroll(params View[] children)
    {
        var stack = new VerticalStackLayout
        {
            Padding = new Thickness(16, 12, 16, 28),
            Spacing = 12
        };
        foreach (var c in children)
            stack.Children.Add(c);
        return new ScrollView { Content = stack };
    }

    public static View ModuleTile(string glyph, string label, string? badge, Func<Task> onTap)
    {
        var glyphLabel = ThemedLabel(glyph, 28, FontAttributes.None, "AccentPrimary");
        glyphLabel.HorizontalTextAlignment = TextAlignment.Center;

        View top = glyphLabel;
        if (!string.IsNullOrEmpty(badge))
        {
            var pill = Pill(badge!, "danger");
            var topGrid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                }
            };
            topGrid.Children.Add(glyphLabel);
            Grid.SetColumn(pill, 1);
            topGrid.Children.Add(pill);
            top = topGrid;
        }

        var nameLabel = Heading(label);
        nameLabel.Margin = new Thickness(0, 12, 0, 0);
        var stack = new VerticalStackLayout
        {
            Spacing = 0,
            Padding = new Thickness(4, 6),
            MinimumHeightRequest = 88,
            Children = { top, nameLabel }
        };
        var card = Card(stack, accent: !string.IsNullOrEmpty(badge));
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => { try { await onTap(); } catch { } };
        card.GestureRecognizers.Add(tap);
        return card;
    }

    public static View ChatBubble(string sender, string body, string when, bool mine = false)
    {
        var meta = Caption($"{sender}  ·  {when}");
        var text = Body(body);
        var stack = new VerticalStackLayout { Spacing = 4, Children = { meta, text } };
        var card = Card(stack, accent: mine);
        card.Margin = new Thickness(mine ? 48 : 0, 0, mine ? 0 : 48, 0);
        return card;
    }
}
