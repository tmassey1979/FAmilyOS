using FamilyOS.Maui.Services;

namespace FamilyOS.Maui.Pages;

/// <summary>Local diagnostic view for the signed-in Keycloak access token.</summary>
public sealed class AccessTokenPage : ContentPage
{
    private readonly IAuthSessionService _auth;
    private readonly Editor _token = new()
    {
        IsReadOnly = true,
        AutoSize = EditorAutoSizeOption.Disabled,
        HeightRequest = 260,
        FontFamily = "monospace",
        FontSize = 11
    };
    private string? _accessToken;
    private bool _revealed;

    public AccessTokenPage(IAuthSessionService auth)
    {
        _auth = auth;
        Title = "Access token";

        var status = new Label { FontSize = 13 };
        var reveal = new Button { Text = "Reveal token" };
        reveal.SetDynamicResource(VisualElement.StyleProperty, "FoSecondaryButtonStyle");
        reveal.Clicked += (_, _) =>
        {
            _revealed = !_revealed;
            _token.Text = _revealed ? _accessToken ?? string.Empty : MaskToken(_accessToken);
            reveal.Text = _revealed ? "Hide token" : "Reveal token";
        };

        var copy = new Button { Text = "Copy token" };
        copy.SetDynamicResource(VisualElement.StyleProperty, "FoPrimaryButtonStyle");
        copy.Clicked += async (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_accessToken))
            {
                await DisplayAlertAsync("No token", "Sign in again to obtain an access token.", "OK");
                return;
            }

            await Clipboard.Default.SetTextAsync(_accessToken);
            await DisplayAlertAsync("Copied", "The access token was copied to your clipboard. Treat it like a password.", "OK");
        };

        var refresh = new Button { Text = "Reload token" };
        refresh.SetDynamicResource(VisualElement.StyleProperty, "FoSecondaryButtonStyle");
        refresh.Clicked += async (_, _) => await LoadTokenAsync(status);

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 18,
                Spacing = 12,
                Children =
                {
                    new Label { Text = "Keycloak access token", FontSize = 24, FontAttributes = FontAttributes.Bold },
                    new Label
                    {
                        Text = "This is the bearer token currently stored by the MAUI app. Anyone who obtains it may be able to act as your account until it expires. Do not share it publicly.",
                        FontSize = 13
                    },
                    status,
                    _token,
                    new HorizontalStackLayout { Spacing = 8, Children = { reveal, copy } },
                    refresh
                }
            }
        };

        Loaded += async (_, _) => await LoadTokenAsync(status);
    }

    private async Task LoadTokenAsync(Label status)
    {
        status.Text = "Loading token from secure storage…";
        try
        {
            _accessToken = await _auth.GetAccessTokenAsync();
            _revealed = false;
            _token.Text = MaskToken(_accessToken);
            status.Text = string.IsNullOrWhiteSpace(_accessToken)
                ? "No access token is available. Sign in again."
                : $"Token loaded ({_accessToken.Length} characters).";
        }
        catch (Exception ex)
        {
            _accessToken = null;
            _token.Text = string.Empty;
            status.Text = $"Could not load token: {ex.GetType().Name}";
        }
    }

    private static string MaskToken(string? token) =>
        string.IsNullOrWhiteSpace(token) ? "No access token is stored." : new string('•', Math.Min(token.Length, 64));
}
