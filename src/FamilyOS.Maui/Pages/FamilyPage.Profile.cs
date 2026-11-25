using FamilyOS.Maui.Services;

namespace FamilyOS.Maui.Pages;

public sealed partial class FamilyPage
{
    private MeApiClient _me = null!;

    private void WireProfile(MeApiClient me) => _me = me;
    private MeApiClient Me => _me;

    private static View BuildMemberAvatar(FamilyMemberDto member, bool isYou, string initials)
    {
        if (!string.IsNullOrWhiteSpace(member.AvatarUrl))
        {
            var path = member.AvatarUrl!;
            var abs = path.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? path
                : "https://api.18.226.226.118.sslip.io/" + path.TrimStart('/');
            return new Border
            {
                WidthRequest = 44,
                HeightRequest = 44,
                StrokeThickness = 0,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 22 },
                Content = new Image
                {
                    Source = ImageSource.FromUri(new Uri(abs)),
                    Aspect = Aspect.AspectFill,
                    WidthRequest = 44,
                    HeightRequest = 44
                }
            };
        }

        return new Border
        {
            WidthRequest = 44,
            HeightRequest = 44,
            StrokeThickness = 0,
            BackgroundColor = Color.FromArgb(isYou ? "#29466F" : "#E8EEF9"),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 22 },
            Content = new Label
            {
                Text = initials.Length > 2 ? initials[..2] : initials,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                TextColor = Color.FromArgb(isYou ? "#FFFFFF" : "#29466F"),
                FontAttributes = FontAttributes.Bold
            }
        };
    }

    private View BuildSelfProfileActions(FamilyMemberDto member)
    {
        var profileActions = new HorizontalStackLayout { Spacing = 8 };
        var changePhoto = new Button { Text = "Change photo", FontSize = 12 };
        changePhoto.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
        changePhoto.Clicked += async (_, _) => await ChangeMyPhotoAsync();
        var editName = new Button { Text = "Edit name", FontSize = 12 };
        editName.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
        editName.Clicked += async (_, _) => await EditMyNameAsync(member);
        profileActions.Children.Add(changePhoto);
        profileActions.Children.Add(editName);
        if (!string.IsNullOrWhiteSpace(member.AvatarUrl))
        {
            var removePhoto = new Button { Text = "Remove photo", FontSize = 12 };
            removePhoto.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
            removePhoto.Clicked += async (_, _) => await ClearMyPhotoAsync();
            profileActions.Children.Add(removePhoto);
        }
        return profileActions;
    }

    private async Task ChangeMyPhotoAsync()
    {
        try
        {
            var choice = await DisplayActionSheetAsync("Profile photo", "Cancel", null, "Take photo", "Choose from library");
            if (choice is null or "Cancel") return;
            FileResult? file = choice == "Take photo"
                ? await MediaPicker.Default.CapturePhotoAsync()
                : await MediaPicker.Default.PickPhotoAsync();
            if (file is null) return;
            await using var stream = await file.OpenReadAsync();
            var contentType = string.IsNullOrWhiteSpace(file.ContentType)
                ? (file.FileName?.EndsWith(".png", StringComparison.OrdinalIgnoreCase) == true ? "image/png" : "image/jpeg")
                : file.ContentType;
            await Me.UploadAvatarAsync(stream, file.FileName ?? "avatar.jpg", contentType);
            await LoadAsync();
            await DisplayAlertAsync("Photo updated", "Your profile photo was saved.", "OK");
        }
        catch (FeatureNotSupportedException)
        {
            await DisplayAlertAsync("Not available", "Camera or photo library is not available on this device.", "OK");
        }
        catch (PermissionException)
        {
            await DisplayAlertAsync("Permission needed", "Allow camera or photo access to update your profile picture.", "OK");
        }
        catch (FamilyApiException ex)
        {
            await DisplayAlertAsync("Could not upload photo", ex.Message, "OK");
        }
        catch
        {
            await DisplayAlertAsync("Could not upload photo", "Try again with a smaller JPEG or PNG.", "OK");
        }
    }

    private async Task ClearMyPhotoAsync()
    {
        if (!await DisplayAlertAsync("Remove photo?", "Your profile will show initials instead.", "Remove", "Cancel"))
            return;
        try
        {
            await Me.ClearAvatarAsync();
            await LoadAsync();
        }
        catch (FamilyApiException ex)
        {
            await DisplayAlertAsync("Could not remove photo", ex.Message, "OK");
        }
        catch
        {
            await DisplayAlertAsync("Could not remove photo", "Try again.", "OK");
        }
    }

    private async Task EditMyNameAsync(FamilyMemberDto member)
    {
        var name = await DisplayPromptAsync(
            "Display name",
            "How should family members see you?",
            "Save",
            "Cancel",
            initialValue: member.DisplayName);
        if (name is null) return;
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            await DisplayAlertAsync("Name required", "Enter a display name.", "OK");
            return;
        }
        try
        {
            await Me.UpdateProfileAsync(name);
            await LoadAsync();
        }
        catch (FamilyApiException ex)
        {
            await DisplayAlertAsync("Could not update name", ex.Message, "OK");
        }
        catch
        {
            await DisplayAlertAsync("Could not update name", "Try again.", "OK");
        }
    }
}
