using FamilyOS.Maui.Services;

namespace FamilyOS.Maui.Pages;

/// <summary>API-backed household overview. Membership and roles come from the server.</summary>
public sealed class FamilyPage : ContentPage
{
    private readonly FamilyApiClient _family;
    private readonly VerticalStackLayout _members;
    private readonly Label _name;
    private readonly Label _details;
    private readonly Label _status;
    private readonly Button _refresh;
    private bool _loading;

    public FamilyPage(FamilyApiClient family)
    {
        _family = family;
        Title = "Family";

        _name = new Label { Text = "Your family", FontSize = 26, FontAttributes = FontAttributes.Bold };
        _name.SetDynamicResource(VisualElement.StyleProperty, "TextDisplay");
        _details = new Label { FontSize = 13 };
        _details.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
        _status = new Label { Text = "Loading household…", FontSize = 13 };
        _status.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
        _refresh = new Button { Text = "Refresh" };
        _refresh.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
        _refresh.Clicked += async (_, _) => await LoadAsync();

        var header = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            Children = { _name, _refresh }
        };
        Grid.SetColumn(_refresh, 1);
        _members = new VerticalStackLayout { Spacing = 10 };

        var content = new VerticalStackLayout
        {
            Padding = new Thickness(20, 12),
            Spacing = 14,
            Children = { header, _details, _status, _members }
        };
        Content = new RefreshView
        {
            Content = new ScrollView { Content = content },
            Command = new Command(async () => await LoadAsync())
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_members.Children.Count == 0 && !_loading)
            await LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (_loading) return;
        _loading = true;
        _refresh.IsEnabled = false;
        _status.Text = "Loading household…";
        try
        {
            var family = await _family.GetFamilyAsync();
            _name.Text = family.Name;
            var meta = new List<string>();
            if (!string.IsNullOrWhiteSpace(family.TimeZone)) meta.Add(family.TimeZone);
            if (!string.IsNullOrWhiteSpace(family.Currency)) meta.Add(family.Currency);
            _details.Text = meta.Count == 0 ? "Household overview" : string.Join(" · ", meta);
            _members.Children.Clear();

            var members = family.Members.Where(m => m.IsActive).OrderBy(RoleOrder).ThenBy(m => m.DisplayName).ToList();
            _status.Text = $"{members.Count} active member{(members.Count == 1 ? "" : "s")}";
            if (members.Count == 0)
                AddInfoCard("No active members", "Family members will appear here when they join the household.");
            foreach (var member in members) AddMember(member);
        }
        catch (OperationCanceledException)
        {
            _status.Text = "The request timed out. Pull to refresh and try again.";
        }
        catch (FamilyApiException ex)
        {
            _status.Text = ex.Message;
        }
        catch (HttpRequestException)
        {
            _status.Text = "FamilyOS could not be reached. Check your connection and try again.";
        }
        catch (Exception)
        {
            _status.Text = "Something went wrong while loading your family.";
        }
        finally
        {
            _loading = false;
            _refresh.IsEnabled = true;
        }
    }

    private void AddMember(FamilyMemberDto member)
    {
        var initials = string.Join("", member.DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(2).Select(part => part[0])).ToUpperInvariant();
        var avatar = new Border
        {
            WidthRequest = 44,
            HeightRequest = 44,
            StrokeThickness = 0,
            BackgroundColor = Color.FromArgb("#E8EEF9"),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 22 },
            Content = new Label
            {
                Text = string.IsNullOrWhiteSpace(initials) ? "?" : initials,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                TextColor = Color.FromArgb("#29466F"),
                FontAttributes = FontAttributes.Bold
            }
        };

        var identity = new VerticalStackLayout { Spacing = 3, VerticalOptions = LayoutOptions.Center };
        var name = new Label { Text = member.DisplayName, FontSize = 16, FontAttributes = FontAttributes.Bold };
        name.SetDynamicResource(VisualElement.StyleProperty, "TextBodyStrong");
        var role = new Label { Text = member.Role, FontSize = 12 };
        role.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
        identity.Children.Add(name);
        identity.Children.Add(role);

        var row = new HorizontalStackLayout { Spacing = 12, Children = { avatar, identity } };
        var card = new Border { Padding = 12, Content = row };
        card.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");
        _members.Children.Add(card);
    }

    private static int RoleOrder(FamilyMemberDto member) => member.Role switch
    {
        "Owner" => 0,
        "Adult" => 1,
        "Teen" => 2,
        "Child" => 3,
        _ => 4
    };

    private void AddInfoCard(string title, string detail)
    {
        var stack = new VerticalStackLayout { Spacing = 5 };
        var heading = new Label { Text = title, FontSize = 16, FontAttributes = FontAttributes.Bold };
        heading.SetDynamicResource(VisualElement.StyleProperty, "TextBodyStrong");
        var body = new Label { Text = detail, FontSize = 14 };
        body.SetDynamicResource(VisualElement.StyleProperty, "TextBody");
        stack.Children.Add(heading);
        stack.Children.Add(body);
        var card = new Border { Padding = 14, Content = stack };
        card.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");
        _members.Children.Add(card);
    }
}
