using FamilyOS.Maui.Controls;
using FamilyOS.Maui.Services;

namespace FamilyOS.Maui.Pages;

/// <summary>Sign in, create a household, or accept an existing family invitation.</summary>
public sealed class OnboardingPage : ContentPage
{
    private readonly IAuthSessionService _auth;
    private readonly OnboardingApiClient _api;
    private readonly StartupDiagnosticsService _diagnostics;
    private readonly Entry _familyName = new() { Placeholder = "Household name" };
    private readonly Entry _displayName = new() { Placeholder = "Your display name (optional)" };
    private readonly Entry _email = new() { Placeholder = "Email (optional)", Keyboard = Keyboard.Email };
    private readonly Entry _inviteCode = new() { Placeholder = "Invite code", CharacterSpacing = 1 };
    private readonly Entry _joinName = new() { Placeholder = "Your display name (optional)" };
    private readonly Label _status = new() { FontSize = 13 };
    private readonly Label _invitePreview = new() { FontSize = 14, IsVisible = false };
    private readonly FoButton _signIn = new() { Title = "Sign in with FamilyOS", LoadingText = "Opening sign-in…" };
    private readonly FoButton _create = new() { Title = "Create household", LoadingText = "Creating household…" };
    private readonly FoButton _preview = new() { Title = "Check invite", LoadingText = "Checking invitation…", Variant = FoButtonVariant.Secondary };
    private readonly FoButton _join = new() { Title = "Join household", LoadingText = "Joining household…" };
    private bool _signedIn;
    private bool _busy;
    private FoButton? _busyButton;
    private InvitePreviewDto? _currentInvite;

    public OnboardingPage(IAuthSessionService auth, OnboardingApiClient api, StartupDiagnosticsService diagnostics)
    {
        _auth = auth;
        _api = api;
        _diagnostics = diagnostics;
        Title = "Get started";
        _status.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");

        _signIn.Clicked += async (_, _) => await SignInAsync();
        _create.Clicked += async (_, _) => await CreateHouseholdAsync();
        _preview.Clicked += async (_, _) => await PreviewInviteAsync();
        _join.Clicked += async (_, _) => await JoinHouseholdAsync();

        var heading = new Label { Text = "Welcome to FamilyOS", FontSize = 26, FontAttributes = FontAttributes.Bold };
        heading.SetDynamicResource(VisualElement.StyleProperty, "TextDisplay");
        var intro = new Label
        {
            Text = "Sign in, then create a household or join one with an invitation code.",
            FontSize = 14,
            LineBreakMode = LineBreakMode.WordWrap
        };
        intro.SetDynamicResource(VisualElement.StyleProperty, "TextBody");

        var signInCard = Card("Your account", new View[] { _signIn });
        var createCard = Card("Create a household", new View[] { _familyName, _displayName, _email, _create });
        var joinCard = Card("Join an existing household",
            new View[] { _inviteCode, _preview, _invitePreview, _joinName, _join });

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(20, 24),
                Spacing = 16,
                Children = { heading, intro, _status, signInCard, createCard, joinCard }
            }
        };
        UpdateEnabledState();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _diagnostics.RecordCheckpoint("AUTH onboarding page appeared; checking existing session");
        try
        {
            _signedIn = !string.IsNullOrWhiteSpace(await _auth.GetAccessTokenAsync());
            _diagnostics.RecordCheckpoint($"AUTH onboarding session check completed; signed_in={_signedIn}");
            _status.Text = _signedIn ? "You're signed in. Choose how to continue." : "Sign in to get started.";
        }
        catch (Exception ex)
        {
            _diagnostics.RecordCheckpoint($"AUTH onboarding session check failed; exception={ex.GetType().Name}");
            _signedIn = false;
            _status.Text = "Sign in to get started.";
        }
        UpdateEnabledState();
    }

    private async Task SignInAsync()
    {
        if (_busy) return;
        _diagnostics.RecordCheckpoint("AUTH onboarding sign-in button tapped");
        SetBusy(true, "Opening secure sign-in…", _signIn);
        try
        {
            await _auth.SignInAsync();
            _signedIn = true;
            _diagnostics.RecordCheckpoint("AUTH onboarding sign-in succeeded; UI state updated to signed_in=true");
            _status.Text = "You're signed in. Create or join a household below.";
        }
        catch (OperationCanceledException)
        {
            _diagnostics.RecordCheckpoint("AUTH onboarding sign-in cancelled");
            _status.Text = "Sign-in was cancelled.";
        }
        catch (Exception ex)
        {
            _diagnostics.RecordCheckpoint($"AUTH onboarding sign-in failed; exception={ex.GetType().Name}; http_status={(ex is HttpRequestException http && http.StatusCode.HasValue ? (int)http.StatusCode.Value : 0)}");
            _status.Text = "Sign-in did not complete. Check your connection and try again.";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task CreateHouseholdAsync()
    {
        if (_busy || !_signedIn) return;
        if (string.IsNullOrWhiteSpace(_familyName.Text))
        {
            _status.Text = "Enter a name for your household.";
            return;
        }

        _diagnostics.RecordCheckpoint("AUTH household creation started; input values omitted");
        SetBusy(true, "Creating your household…", _create);
        try
        {
            _status.Text = "Checking secure connection to FamilyOS…";
            var apiReachable = await _api.ProbeApiHealthAsync();
            if (!apiReachable)
            {
                _diagnostics.RecordCheckpoint("AUTH household creation stopped; API health probe failed; no household POST was sent");
                _status.Text = "FamilyOS could not be reached securely. No household was created. Please share the latest diagnostics.";
                return;
            }

            _status.Text = "Connection verified. Creating your household…";
            await _api.CreateFamilyAsync(
                _familyName.Text.Trim(),
                _displayName.Text,
                _email.Text,
                TimeZoneInfo.Local.Id);
            _diagnostics.RecordCheckpoint("AUTH household creation API succeeded");
            await FinishAsync("Your household is ready.");
        }
        catch (FamilyApiException ex)
        {
            _diagnostics.RecordCheckpoint($"AUTH household creation rejected; status={(int)ex.StatusCode}");
            _status.Text = ex.Message;
        }
        catch (HttpRequestException ex)
        {
            _diagnostics.RecordCheckpoint($"AUTH household creation network failure; exception={ex.GetType().Name}; http_status={(ex.StatusCode.HasValue ? (int)ex.StatusCode.Value : 0)}");
            _status.Text = "FamilyOS could not be reached. Check your connection and try again.";
        }
        catch (Exception ex)
        {
            _diagnostics.RecordCheckpoint($"AUTH household creation failed; exception={ex.GetType().Name}");
            _status.Text = "The household could not be created. Try again.";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task PreviewInviteAsync()
    {
        if (_busy) return;
        if (string.IsNullOrWhiteSpace(_inviteCode.Text))
        {
            _status.Text = "Enter the invitation code first.";
            return;
        }

        _diagnostics.RecordCheckpoint("AUTH invite preview started; invite code omitted");
        SetBusy(true, "Checking invitation…", _preview);
        try
        {
            _currentInvite = await _api.PreviewInviteAsync(_inviteCode.Text);
            _diagnostics.RecordCheckpoint($"AUTH invite preview completed; open={_currentInvite.IsOpen}; role={_currentInvite.Role}");
            _invitePreview.Text = $"{_currentInvite.FamilyName} · {_currentInvite.Role} · " +
                (_currentInvite.IsOpen ? $"Expires {_currentInvite.ExpiresAtUtc.ToLocalTime():g}" : "This invitation is closed");
            _invitePreview.IsVisible = true;
            _status.Text = _currentInvite.IsOpen
                ? "Invitation found. Sign in and join when you're ready."
                : "This invitation is expired, revoked, or already used.";
        }
        catch (FamilyApiException ex)
        {
            _currentInvite = null;
            _invitePreview.IsVisible = false;
            _status.Text = ex.Message;
        }
        catch (Exception)
        {
            _currentInvite = null;
            _invitePreview.IsVisible = false;
            _status.Text = "Could not check that invitation. Try again.";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task JoinHouseholdAsync()
    {
        if (_busy || !_signedIn) return;
        if (_currentInvite is null || !_currentInvite.IsOpen)
        {
            _status.Text = "Check a valid invitation before joining.";
            return;
        }

        _diagnostics.RecordCheckpoint("AUTH accept-invitation started; invite code omitted");
        SetBusy(true, "Joining household…", _join);
        try
        {
            await _api.AcceptInviteAsync(_inviteCode.Text, _joinName.Text);
            _diagnostics.RecordCheckpoint("AUTH accept-invitation API succeeded");
            await FinishAsync($"You've joined {_currentInvite.FamilyName}.");
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
            _status.Text = "The invitation could not be accepted. Try again.";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task FinishAsync(string message)
    {
        _status.Text = message;
        await DisplayAlertAsync("FamilyOS", message, "Continue");
        await Shell.Current.GoToAsync("//home");
    }

    private void SetBusy(bool busy, string? message = null, FoButton? activeButton = null)
    {
        if (!busy && _busyButton is not null)
        {
            _busyButton.IsLoading = false;
            _busyButton = null;
        }

        _busy = busy;
        if (busy && activeButton is not null)
        {
            _busyButton = activeButton;
            activeButton.IsLoading = true;
        }

        if (message is not null) _status.Text = message;
        UpdateEnabledState();
    }

    private void UpdateEnabledState()
    {
        _signIn.IsEnabled = !_busy && !_signedIn;
        _create.IsEnabled = !_busy && _signedIn;
        _preview.IsEnabled = !_busy;
        _join.IsEnabled = !_busy && _signedIn && _currentInvite?.IsOpen == true;
    }

    private static Border Card(string title, IEnumerable<View> children)
    {
        var heading = new Label { Text = title, FontSize = 17, FontAttributes = FontAttributes.Bold };
        heading.SetDynamicResource(VisualElement.StyleProperty, "TextBodyStrong");
        var stack = new VerticalStackLayout { Spacing = 10 };
        stack.Children.Add(heading);
        foreach (var child in children) stack.Children.Add(child);
        var border = new Border { Padding = 16, Content = stack };
        border.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");
        return border;
    }
}
