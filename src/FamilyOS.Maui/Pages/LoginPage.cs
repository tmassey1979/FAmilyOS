using FamilyOS.Maui.Controls;

namespace FamilyOS.Maui.Pages;

/// <summary>
/// Session gate placeholder for DEBUG / manual token entry.
/// Production path uses Keycloak via OnboardingPage.
/// </summary>
public sealed class LoginPage : ContentPage
{
    private readonly Entry _baseUrl;
    private readonly Entry _token;
    private readonly Label _status;

    public LoginPage()
    {
        Title = "Sign in";

        _baseUrl = new Entry
        {
            Placeholder = "https://api.example.com",
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
                Ui.Caption("Access token"),
                _token,
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
        if (string.IsNullOrWhiteSpace(_token.Text))
        {
            _status.Text = "Enter a JWT, or use Get started / Onboarding for Keycloak sign-in.";
            return;
        }

        try
        {
            if (Shell.Current is not null)
                await Shell.Current.GoToAsync("//home");
            else
                _status.Text = "Shell is not ready. Restart the app after signing in.";
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
        }
    }
}
