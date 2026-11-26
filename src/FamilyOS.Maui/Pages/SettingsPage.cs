using FamilyOS.Maui.Services;

namespace FamilyOS.Maui.Pages;

/// <summary>Account overview and quiet-hours controls.</summary>
public sealed class SettingsPage : ContentPage
{
    private readonly MeApiClient _me;
    private readonly FamilyApiClient _family;
    private readonly Label _account = new() { FontSize = 14 };
    private readonly Label _profileName = new() { FontSize = 20, FontAttributes = FontAttributes.Bold };
    private readonly Image _profilePhoto = new()
    {
        WidthRequest = 76,
        HeightRequest = 76,
        Aspect = Aspect.AspectFill,
        Clip = new Microsoft.Maui.Controls.Shapes.EllipseGeometry
        {
            Center = new Point(38, 38),
            RadiusX = 38,
            RadiusY = 38
        }
    };
    private ProfileDto? _profile;
    private readonly Label _household = new() { FontSize = 14 };
    private readonly Label _status = new() { FontSize = 12 };
    private readonly Label _googleStatus = new() { FontSize = 13 };
    private readonly Button _googleConnect = new() { Text = "Connect Google Calendar" };
    private readonly Button _googleSync = new() { Text = "Sync now" };
    private readonly Button _googleDisconnect = new() { Text = "Disconnect" };
    private readonly Switch _enabled = new();
    private readonly TimePicker _start = new() { Time = new TimeSpan(22, 0, 0), Format = "h:mm tt", WidthRequest = 120 };
    private readonly TimePicker _end = new() { Time = new TimeSpan(7, 0, 0), Format = "h:mm tt", WidthRequest = 120 };
    private readonly Button _save = new() { Text = "Save quiet hours" };
    private readonly Button _refresh = new() { Text = "Refresh" };
    private readonly Button _resetFamily = new() { Text = "Reset family data", TextColor = Colors.White, BackgroundColor = (Color)Application.Current!.Resources["AccentDanger"], IsVisible = false };
    private bool _busy;

    public SettingsPage(MeApiClient me, FamilyApiClient family)
    {
        _me = me;
        _family = family;
        Title = "Settings";
        _save.SetDynamicResource(VisualElement.StyleProperty, "FoPrimaryButtonStyle");
        _refresh.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
        _resetFamily.Clicked += async (_, _) => await ResetFamilyAsync();
        _status.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
        _account.SetDynamicResource(VisualElement.StyleProperty, "TextBody");
        _household.SetDynamicResource(VisualElement.StyleProperty, "TextBody");
        _save.Clicked += async (_, _) => await SaveAsync();
        _refresh.Clicked += async (_, _) => await LoadAsync();
        _googleConnect.Clicked += async (_, _) => await ConnectGoogleCalendarAsync();
        _googleSync.Clicked += async (_, _) => await SyncGoogleCalendarAsync();
        _googleDisconnect.Clicked += async (_, _) => await DisconnectGoogleCalendarAsync();

        var heading = new Label { Text = "Settings", FontSize = 26, FontAttributes = FontAttributes.Bold };
        heading.SetDynamicResource(VisualElement.StyleProperty, "TextDisplay");
        var photoFrame = new Border { WidthRequest = 80, HeightRequest = 80, StrokeThickness = 0, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 40 }, Content = _profilePhoto };
        var profileActions = new HorizontalStackLayout { Spacing = 8 };
        var changePhoto = new Button { Text = "Change picture", FontSize = 12 };
        changePhoto.SetDynamicResource(VisualElement.StyleProperty, "FoSecondaryButtonStyle");
        changePhoto.Clicked += async (_, _) => await ChangeProfilePhotoAsync();
        var editProfile = new Button { Text = "Edit name", FontSize = 12 };
        editProfile.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
        editProfile.Clicked += async (_, _) => await EditProfileNameAsync();
        profileActions.Children.Add(changePhoto);
        profileActions.Children.Add(editProfile);
        var profileCard = new Border { Padding = 16, Content = new VerticalStackLayout { Spacing = 10, Children = { new Label { Text = "My profile", FontSize = 18, FontAttributes = FontAttributes.Bold }, new HorizontalStackLayout { Spacing = 14, Children = { photoFrame, new VerticalStackLayout { VerticalOptions = LayoutOptions.Center, Children = { _profileName, new Label { Text = "Your name and photo shown to your family", FontSize = 12 } } } } }, profileActions } } };
        profileCard.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");
        var accountCard = Card("Account details", _account);
        var familyCard = Card("Household", _household);
        var quietHeading = new Label { Text = "Quiet hours", FontSize = 18, FontAttributes = FontAttributes.Bold };
        quietHeading.SetDynamicResource(VisualElement.StyleProperty, "TextTitle");
        var toggleRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            Children =
            {
                new Label { Text = "Enable quiet hours", VerticalOptions = LayoutOptions.Center },
                _enabled
            }
        };
        Grid.SetColumn(_enabled, 1);
        var timeRow = new HorizontalStackLayout
        {
            Spacing = 18,
            Children =
            {
                new VerticalStackLayout { Spacing = 4, Children = { new Label { Text = "Start" }, _start } },
                new VerticalStackLayout { Spacing = 4, Children = { new Label { Text = "End" }, _end } }
            }
        };
        var quietCard = new Border
        {
            Padding = 16,
            Content = new VerticalStackLayout
            {
                Spacing = 12,
                Children =
                {
                    quietHeading,
                    new Label { Text = "Mute non-urgent household notifications during this local-time window.", FontSize = 13 },
                    toggleRow,
                    timeRow,
                    _save
                }
            }
        };
        quietCard.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");
        var googleHeading = new Label { Text = "Google Calendar", FontSize = 18, FontAttributes = FontAttributes.Bold };
        googleHeading.SetDynamicResource(VisualElement.StyleProperty, "TextTitle");
        var googleCard = new Border
        {
            Padding = 16,
            Content = new VerticalStackLayout
            {
                Spacing = 10,
                Children =
                {
                    googleHeading,
                    new Label { Text = "Connect your personal Google Calendar to sync events with the household calendar. Your FamilyOS calendar remains available without connecting Google.", FontSize = 13 },
                    _googleStatus,
                    new HorizontalStackLayout { Spacing = 8, Children = { _googleConnect, _googleSync, _googleDisconnect } }
                }
            }
        };
        googleCard.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");
        var resetHeading = new Label { Text = "Reset family data", FontSize = 18, FontAttributes = FontAttributes.Bold };
        resetHeading.SetDynamicResource(VisualElement.StyleProperty, "TextTitle");
        var resetCard = new Border
        {
            Padding = 16,
            Content = new VerticalStackLayout
            {
                Spacing = 10,
                Children =
                {
                    resetHeading,
                    new Label { Text = "Permanently erase this household, its members and family activity. Sign-in accounts are preserved. Only the household owner can do this.", FontSize = 13 },
                    _resetFamily
                }
            }
        };
        resetCard.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");
        var toolbar = new HorizontalStackLayout { Spacing = 8, Children = { _refresh } };
        Content = new RefreshView
        {
            Content = new ScrollView
            {
                Content = new VerticalStackLayout
                {
                    Padding = new Thickness(18, 12),
                    Spacing = 14,
                    Children = { heading, toolbar, _status, profileCard, accountCard, familyCard, quietCard, googleCard, resetCard }
                }
            },
            Command = new Command(async () => await LoadAsync())
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (string.IsNullOrEmpty(_account.Text)) await LoadAsync();
    }

    private static Border Card(string title, View content)
    {
        var heading = new Label { Text = title, FontSize = 18, FontAttributes = FontAttributes.Bold };
        heading.SetDynamicResource(VisualElement.StyleProperty, "TextTitle");
        var card = new Border { Padding = 16, Content = new VerticalStackLayout { Spacing = 10, Children = { heading, content } } };
        card.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");
        return card;
    }

    private async Task LoadAsync()
    {
        if (_busy) return;
        _busy = true;
        _refresh.IsEnabled = false;
        _status.Text = "Loading settings…";
        try
        {
            var meTask = _me.GetAsync();
            var profileTask = _me.GetProfileAsync();
            var familyTask = _family.GetFamilyAsync();
            var quietTask = _me.GetQuietHoursAsync();
            var googleTask = _me.GetGoogleCalendarStatusAsync();
            await Task.WhenAll(meTask, profileTask, familyTask, quietTask);
            var me = await meTask;
            _profile = await profileTask;
            _profileName.Text = _profile.DisplayName;
            if (!string.IsNullOrWhiteSpace(_profile.AvatarUrl))
            {
                var avatarUri = _profile.AvatarUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? _profile.AvatarUrl
                    : "https://api.18.226.226.118.sslip.io/" + _profile.AvatarUrl.TrimStart('/');
                _profilePhoto.Source = ImageSource.FromUri(new Uri(avatarUri));
            }
            else
            {
                _profilePhoto.Source = null;
                _profilePhoto.BackgroundColor = (Color)Application.Current!.Resources["BgGlow"];
            }
            var family = await familyTask;
            var quiet = await quietTask;

            GoogleCalendarStatusDto? google = null;
            try
            {
                google = await googleTask;
            }
            catch (FamilyApiException)
            {
                // An optional integration must never block core account settings.
            }

            _googleStatus.Text = google is null
                ? "Google Calendar status is temporarily unavailable."
                : !google.Configured
                    ? "Google Calendar integration is not configured on the server."
                    : google.Connected
                        ? $"Connected{(string.IsNullOrWhiteSpace(google.Email) ? "" : $" as {google.Email}")}. Last sync: {(google.LastSyncAtUtc.HasValue ? google.LastSyncAtUtc.Value.ToLocalTime().ToString("g") : "not yet synced")}."
                        : "Not connected.";
            _googleConnect.IsVisible = google?.Configured == true && google.Connected == false;
            _googleSync.IsVisible = google?.Configured == true && google.Connected;
            _googleDisconnect.IsVisible = google?.Configured == true && google.Connected;
            _account.Text = $"Role: {me.Role ?? "Member"}\nMember ID: {me.MemberId?.ToString("D") ?? "Unavailable"}";
            _household.Text = $"{family.Name}\n{family.Members.Count(x => x.IsActive)} active members";
            _resetFamily.IsVisible = string.Equals(me.Role, "Owner", StringComparison.OrdinalIgnoreCase);
            _resetFamily.IsVisible = string.Equals(me.Role, "Owner", StringComparison.OrdinalIgnoreCase);
            _enabled.IsToggled = quiet.Enabled;
            _start.Time = TimeSpan.FromMinutes(quiet.StartMinutes ?? 1320);
            _end.Time = TimeSpan.FromMinutes(quiet.EndMinutes ?? 420);
            _status.Text = "Settings loaded.";
        }
        catch (FamilyApiException ex) { _status.Text = ex.Message; }
        catch (HttpRequestException) { _status.Text = "FamilyOS could not be reached. Check your connection."; }
        catch (Exception) { _status.Text = "Could not load settings. Try refreshing."; }
        finally { _busy = false; _refresh.IsEnabled = true; }
    }

    private async Task EditProfileNameAsync()
    {
        if (_profile is null) return;
        var name = await DisplayPromptAsync("Your name", "How should your family see you?", "Save", "Cancel", initialValue: _profile.DisplayName);
        if (name is null) return;
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name)) { _status.Text = "Enter a display name."; return; }
        try { await _me.UpdateProfileAsync(name); _status.Text = "Profile updated."; await LoadAsync(); }
        catch (Exception ex) { _status.Text = ex is FamilyApiException ? ex.Message : "Could not update your profile."; }
    }

    private async Task ChangeProfilePhotoAsync()
    {
        try
        {
            var choice = await DisplayActionSheetAsync("Profile picture", "Cancel", null, "Take photo", "Choose from library", "Remove photo");
            if (choice is null or "Cancel") return;
            if (choice == "Remove photo") await _me.ClearAvatarAsync();
            else
            {
                var file = choice == "Take photo" ? await MediaPicker.Default.CapturePhotoAsync() : await MediaPicker.Default.PickPhotoAsync();
                if (file is null) return;
                await using var original = await file.OpenReadAsync();
                using var buffer = new MemoryStream();
                await original.CopyToAsync(buffer);
                if (buffer.Length > 20_000_000)
                {
                    _status.Text = "Choose an image smaller than 20 MB.";
                    return;
                }
                await Navigation.PushModalAsync(new AvatarCropPage(buffer.ToArray(), async (cropped, name) =>
                {
                    await _me.UploadAvatarAsync(cropped, name, "image/jpeg");
                    _status.Text = "Profile picture updated.";
                }));
                await LoadAsync();
                return;
            }
            _status.Text = "Profile picture updated.";
            await LoadAsync();
        }
        catch (PermissionException) { _status.Text = "Allow camera or photo-library access to change your picture."; }
        catch (Exception ex) { _status.Text = ex is FamilyApiException ? ex.Message : "Could not update your profile picture."; }
    }

    private async Task ConnectGoogleCalendarAsync()
    {
        try
        {
            var result = await _me.GetGoogleCalendarAuthUrlAsync();
            await Launcher.OpenAsync(result.Url);
            _status.Text = "Finish authorization in your browser, then return here and tap Refresh.";
        }
        catch (Exception ex) { _status.Text = ex is FamilyApiException ? ex.Message : "Could not open Google authorization."; }
    }

    private async Task SyncGoogleCalendarAsync()
    {
        if (_busy) return;
        _busy = true;
        _googleSync.IsEnabled = false;
        try
        {
            var result = await _me.SyncGoogleCalendarAsync();
            _status.Text = $"Google Calendar sync complete. {result.Changed} event(s) updated.";
            _busy = false;
            await LoadAsync();
        }
        catch (Exception ex) { _status.Text = ex is FamilyApiException ? ex.Message : "Google Calendar sync failed."; }
        finally { _busy = false; _googleSync.IsEnabled = true; }
    }

    private async Task DisconnectGoogleCalendarAsync()
    {
        if (_busy) return;
        var confirm = await DisplayAlertAsync("Disconnect Google Calendar", "Stop syncing this Google account with FamilyOS?", "Disconnect", "Cancel");
        if (!confirm) return;
        _busy = true;
        _googleDisconnect.IsEnabled = false;
        try
        {
            await _me.DisconnectGoogleCalendarAsync();
            _status.Text = "Google Calendar disconnected.";
            _busy = false;
            await LoadAsync();
        }
        catch (Exception ex) { _status.Text = ex is FamilyApiException ? ex.Message : "Could not disconnect Google Calendar."; }
        finally { _busy = false; _googleDisconnect.IsEnabled = true; }
    }

    private async Task ResetFamilyAsync()
    {
        if (_busy) return;
        var typed = await DisplayPromptAsync(
            "Reset family data",
            "This permanently deletes household locations, history, tasks, messages, calendar data, invitations and other family records. All sign-in accounts are preserved.\n\nType RESET FAMILY exactly to continue.",
            "Continue", "Cancel", "RESET FAMILY");
        if (!string.Equals(typed, "RESET FAMILY", StringComparison.Ordinal)) return;

        var confirmed = await DisplayAlertAsync(
            "Permanently erase household?",
            "There is no undo. Your account will return to household setup after the reset.",
            "Erase family data", "Keep my family");
        if (!confirmed) return;

        _busy = true;
        _resetFamily.IsEnabled = false;
        _status.Text = "Resetting family data…";
        try
        {
            await _family.ResetFamilyAsync("RESET FAMILY");
            await DisplayAlertAsync("Family data reset", "Your sign-in is preserved. You can now create a new household or join one.", "Continue");
            await Shell.Current.GoToAsync(nameof(OnboardingPage));
        }
        catch (FamilyApiException ex)
        {
            _status.Text = ex.Message;
        }
        catch (Exception)
        {
            _status.Text = "Family data could not be reset. Check your connection and try again.";
        }
        finally
        {
            _busy = false;
            _resetFamily.IsEnabled = true;
        }
    }

    private async Task SaveAsync()
    {
        if (_busy) return;
        var start = (int)(_start.Time ?? TimeSpan.Zero).TotalMinutes;
        var end = (int)(_end.Time ?? TimeSpan.Zero).TotalMinutes;
        if (_enabled.IsToggled && start == end)
        {
            _status.Text = "Start and end times must be different.";
            return;
        }
        _busy = true;
        _save.IsEnabled = false;
        try
        {
            var saved = await _me.SetQuietHoursAsync(_enabled.IsToggled, start, end);
            _enabled.IsToggled = saved.Enabled;
            _start.Time = TimeSpan.FromMinutes(saved.StartMinutes ?? 1320);
            _end.Time = TimeSpan.FromMinutes(saved.EndMinutes ?? 420);
            _status.Text = "Quiet hours saved.";
        }
        catch (FamilyApiException ex) { _status.Text = ex.Message; }
        catch (Exception) { _status.Text = "Could not save quiet hours. Try again."; }
        finally { _busy = false; _save.IsEnabled = true; }
    }

}
