using System.Net;
using FamilyOS.Maui.Services;

namespace FamilyOS.Maui.Pages;

/// <summary>API-backed household overview. Membership and roles come from the server.</summary>
public sealed class FamilyPage : ContentPage
{
    private readonly FamilyApiClient _family;
    private readonly VerticalStackLayout _members;
    private readonly VerticalStackLayout _invites;
    private readonly Button _createInvite;
    private readonly Label _name;
    private readonly Label _details;
    private readonly Label _status;
    private readonly Button _refresh;
    private bool _loading;
    private Guid _currentMemberId;
    private bool _isOwner;

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
        _invites = new VerticalStackLayout { Spacing = 8 };
        _createInvite = new Button { Text = "Create invitation" };
        _createInvite.Clicked += async (_, _) => await CreateInviteAsync();
        var inviteHeading = new Label { Text = "Invitations", FontSize = 20, FontAttributes = FontAttributes.Bold };
        inviteHeading.SetDynamicResource(VisualElement.StyleProperty, "TextDisplay");
        var inviteSection = new VerticalStackLayout { Spacing = 10, Children = { inviteHeading, _createInvite, _invites } };

        var content = new VerticalStackLayout
        {
            Padding = new Thickness(20, 12),
            Spacing = 14,
            Children = { header, _details, _status, _members, inviteSection }
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
            _currentMemberId = family.CurrentMemberId;
            _isOwner = family.Members.FirstOrDefault(m => m.Id == _currentMemberId)?.Role == "Owner";
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
            await LoadInvitesAsync();
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

        var memberContent = new VerticalStackLayout { Spacing = 10 };
        memberContent.Children.Add(new HorizontalStackLayout { Spacing = 12, Children = { avatar, identity } });
        if (_isOwner && member.Id != _currentMemberId)
        {
            var actions = new HorizontalStackLayout { Spacing = 8 };
            var roleButton = new Button { Text = "Change role", FontSize = 12 };
            roleButton.Clicked += async (_, _) => await ChangeMemberRoleAsync(member);
            var deactivateButton = new Button { Text = "Remove", FontSize = 12 };
            deactivateButton.Clicked += async (_, _) => await DeactivateMemberAsync(member);
            actions.Children.Add(roleButton);
            actions.Children.Add(deactivateButton);
            memberContent.Children.Add(actions);
        }
        var card = new Border { Padding = 12, Content = memberContent };
        card.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");
        _members.Children.Add(card);
    }


    private async Task ChangeMemberRoleAsync(FamilyMemberDto member)
    {
        var choices = new[] { "Adult", "Teen", "Child", "Owner" };
        var selected = await DisplayActionSheetAsync($"Change {member.DisplayName}'s role", "Cancel", null, choices);
        if (string.IsNullOrWhiteSpace(selected) || selected == "Cancel" || selected == member.Role) return;
        var warning = selected == "Owner"
            ? "This transfers ownership to this member and changes your role to Adult. Continue?"
            : $"Change {member.DisplayName}'s role to {selected}?";
        if (!await DisplayAlertAsync("Confirm role change", warning, "Change role", "Cancel")) return;
        try
        {
            await _family.ChangeMemberRoleAsync(member.Id, selected);
            await LoadAsync();
        }
        catch (FamilyApiException ex) { await DisplayAlertAsync("Could not change role", ex.Message, "OK"); }
        catch { await DisplayAlertAsync("Could not change role", "Try again.", "OK"); }
    }

    private async Task DeactivateMemberAsync(FamilyMemberDto member)
    {
        if (!await DisplayAlertAsync("Remove family member?",
            $"{member.DisplayName} will lose access to this household. This does not delete their account.",
            "Remove", "Cancel")) return;
        try
        {
            await _family.DeactivateMemberAsync(member.Id);
            await LoadAsync();
        }
        catch (FamilyApiException ex) { await DisplayAlertAsync("Could not remove member", ex.Message, "OK"); }
        catch { await DisplayAlertAsync("Could not remove member", "Try again.", "OK"); }
    }

    private async Task LoadInvitesAsync()
    {
        _invites.Children.Clear();
        try
        {
            var invites = await _family.GetInvitesAsync();
            _createInvite.IsVisible = true;
            foreach (var invite in invites.OrderByDescending(x => x.IsOpen))
            {
                var state = invite.IsOpen ? $"Open · expires {invite.ExpiresAtUtc.ToLocalTime():g}"
                    : invite.AcceptedAtUtc.HasValue ? "Accepted" : "Closed or expired";
                var stack = new VerticalStackLayout { Spacing = 4 };
                stack.Children.Add(new Label { Text = $"{invite.Role} invitation", FontAttributes = FontAttributes.Bold });
                stack.Children.Add(new Label { Text = $"{invite.EmailHint ?? "No email restriction"} · {state}", FontSize = 12 });
                if (invite.IsOpen)
                {
                    var actions = new HorizontalStackLayout { Spacing = 8 };
                    var copy = new Button { Text = "Copy code", FontSize = 12 };
                    copy.Clicked += async (_, _) =>
                    {
                        await Microsoft.Maui.ApplicationModel.DataTransfer.Clipboard.Default.SetTextAsync(invite.Code);
                        await DisplayAlertAsync("Invitation code copied", invite.Code, "Done");
                    };
                    var revoke = new Button { Text = "Revoke", FontSize = 12 };
                    revoke.Clicked += async (_, _) => await RevokeInviteAsync(invite);
                    actions.Children.Add(copy);
                    actions.Children.Add(revoke);
                    stack.Children.Add(actions);
                }
                var card = new Border { Padding = 12, Content = stack };
                card.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");
                _invites.Children.Add(card);
            }
            if (invites.Count == 0) AddInviteInfo("No invitations yet", "Create an invitation to add someone to this household.");
        }
        catch (FamilyApiException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
        {
            _createInvite.IsVisible = false;
            AddInviteInfo("Invitations are managed by adults", "Household members are listed above.");
        }
        catch
        {
            _createInvite.IsVisible = false;
            AddInviteInfo("Invitations unavailable", "Pull to refresh and try again.");
        }
    }

    private async Task CreateInviteAsync()
    {
        var role = await DisplayActionSheetAsync("Invite someone as", "Cancel", null, "Adult", "Teen", "Child");
        if (string.IsNullOrWhiteSpace(role) || role == "Cancel") return;
        var email = await DisplayPromptAsync("Optional email", "Restrict this invitation with an email hint, or leave blank.", "Continue", "Skip", keyboard: Keyboard.Email);
        if (email is null) return;
        _createInvite.IsEnabled = false;
        try
        {
            var invite = await _family.CreateInviteAsync(role, string.IsNullOrWhiteSpace(email) ? null : email.Trim());
            await Microsoft.Maui.ApplicationModel.Clipboard.Default.SetTextAsync(invite.Code);
            await DisplayAlertAsync("Invitation created", $"Code: {invite.Code}\n\nCopied to clipboard. Expires {invite.ExpiresAtUtc.ToLocalTime():g}.", "Done");
            await LoadInvitesAsync();
        }
        catch (FamilyApiException ex) { await DisplayAlertAsync("Could not create invitation", ex.Message, "OK"); }
        catch { await DisplayAlertAsync("Could not create invitation", "FamilyOS could not complete that request. Try again.", "OK"); }
        finally { _createInvite.IsEnabled = true; }
    }

    private async Task RevokeInviteAsync(FamilyInviteDto invite)
    {
        if (!await DisplayAlertAsync("Revoke invitation?", $"Code {invite.Code} will stop working.", "Revoke", "Keep")) return;
        try { await _family.RevokeInviteAsync(invite.Id); await LoadInvitesAsync(); }
        catch (FamilyApiException ex) { await DisplayAlertAsync("Could not revoke invitation", ex.Message, "OK"); }
        catch { await DisplayAlertAsync("Could not revoke invitation", "Try again.", "OK"); }
    }

    private void AddInviteInfo(string title, string detail)
    {
        var stack = new VerticalStackLayout { Spacing = 4 };
        stack.Children.Add(new Label { Text = title, FontAttributes = FontAttributes.Bold });
        stack.Children.Add(new Label { Text = detail, FontSize = 13 });
        var card = new Border { Padding = 12, Content = stack };
        card.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");
        _invites.Children.Add(card);
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
