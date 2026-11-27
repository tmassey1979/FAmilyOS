using FamilyOS.Maui.Controls;
using FamilyOS.Maui.Services.Api;

namespace FamilyOS.Maui.Pages;

/// <summary>
/// Session gate. Production path uses Keycloak OIDC/PKCE.
/// DEBUG builds allow X-Dev-User for local API testing.
/// </summary>
public sealed class LoginPage : ContentPage
{
    private readonly IAuthSession _session;
    private readonly Entry _devUser;
    private readonly Entry _baseUrl;
    private readonly Entry _token;
    private readonly Label _status;

    public LoginPage(IAuthSession session)
    {
        _session = session;
        Title = "Sign in";

        _devUser = new Entry
        {
            Placeholder = "terry.owner",
            Text = "terry.owner",
            AutomationId = "devUser"
        };
        _baseUrl = new Entry
        {
            Placeholder = "https://api.example.com",
            Text = session.BaseUrl,
            AutomationId = "apiBase"
        };
        _token = new Entry
        {
            Placeholder = "Paste JWT access token",
            IsPassword = true,
            AutomationId = "accessToken"
        };
        _status = Ui.Caption("");

        var mark = Ui.ThemedLabel("◈", 48, FontAttributes.None, "AccentPrimary");
        mark.HorizontalTextAlignment = TextAlignment.Center;

        var brand = Ui.Display("Family OS");
        brand.HorizontalTextAlignment = TextAlignment.Center;

        var tagline = Ui.Caption("Household command center");
        tagline.HorizontalTextAlignment = TextAlignment.Center;

        var glow = new BoxView
        {
            HeightRequest = 2,
            WidthRequest = 64,
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 4, 0, 8),
            Opacity = 0.9
        };
        glow.SetDynamicResource(BoxView.ColorProperty, "AccentPrimary");

        var continueBtn = Ui.PrimaryCta("Continue →");
        continueBtn.Clicked += async (_, _) => await ContinueAsync();

        var form = new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                Ui.Section("Connection"),
                Ui.Caption("API base URL"),
                _baseUrl,
                Ui.Caption("Access token (preferred)"),
                _token,
                Ui.Caption("Or DEBUG dev user"),
                _devUser,
                continueBtn,
                _status
            }
        };

        var card = Ui.Card(form, accent: true);

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(24, 48, 24, 24),
                Spacing = 16,
                VerticalOptions = LayoutOptions.Center,
                Children = { mark, brand, tagline, glow, card }
            }
        };
    }

    private async Task ContinueAsync()
    {
        _session.SetBaseUrl(_baseUrl.Text?.Trim() ?? _session.BaseUrl);

        var token = _token.Text?.Trim();
        var dev = _devUser.Text?.Trim();

        if (!string.IsNullOrWhiteSpace(token))
        {
            _session.SetAccessToken(token);
            _session.SetDevUser(null);
        }
        else if (!string.IsNullOrWhiteSpace(dev))
        {
#if DEBUG
            _session.SetDevUser(dev);
            _session.SetAccessToken(null);
#else
            _status.Text = "Production builds require a JWT. Dev-user bypass is DEBUG only.";
            return;
#endif
        }
        else
        {
            _status.Text = "Enter a JWT or a DEBUG dev user.";
            return;
        }

        try
        {
            if (Application.Current is App app)
                app.GoToShell();
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
        }

        await Task.CompletedTask;
    }
}
