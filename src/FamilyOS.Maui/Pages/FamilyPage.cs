using System.Net;
using FamilyOS.Maui.Services;

namespace FamilyOS.Maui.Pages;

/// <summary>Household management: members, roles, invites, and family settings.</summary>
public sealed class FamilyPage : ContentPage
{
    private readonly FamilyApiClient _family;
    private readonly VerticalStackLayout _members;
    private readonly VerticalStackLayout _invites;
    private readonly Button _createInvite;
    private readonly Button _editSettings;
    private readonly Label _name;
    private readonly Label _details;
    private readonly Label _status;
    private readonly Button _refresh;
    private bool _loading;
    private Guid _currentMemberId;
    private bool _isOwner;
    private bool _isAdultOrOwner;
    private FamilyDto? _family;

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
        _editSettings = new Button { Text = "Edit household", FontSize = 13, IsVisible = false };
        _editSettings.SetDynamicResource(VisualElement.StyleProperty, "FoSecondaryButtonStyle");
        _editSettings.Clicked += async (_, _) => await EditHouseholdAsync();

        var header = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            Children = { _name, _refresh }
        };
        Grid.SetColumn(_refresh, 1);

        _members = new VerticalStackLayout { Spacing = 10 };
        _invites = new VerticalStackLayout { Spacing = 8 };
        _createInvite = new Button { Text = "Invite someone" };
        _createInvite.SetDynamicResource(VisualElement.StyleProperty, "FoPrimaryButtonStyle");
        _createInvite.Clicked += async (_, _) => await CreateInviteAsync();

        var membersHeading = new Label { Text = "Members", FontSize = 20, FontAttributes = FontAttributes.Bold };
        membersHeading.SetDynamicResource(VisualElement.StyleProperty, "TextDisplay");
        var inviteHeading = new Label { Text = "Invitations", FontSize = 20, FontAttributes = FontAttributes.Bold };
        inviteHeading.SetDynamicResource(VisualElement.StyleProperty, "TextDisplay");
        var inviteHint = new Label
        {
            Text = "Share a code so someone can join this household. Restrict by email if you want.",
            FontSize = 12,
            LineBreakMode = LineBreakMode.WordWrap
        };
        inviteHint.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");

        var inviteSection = new VerticalStackLayout
        {
            Spacing = 10,
            Children = { inviteHeading, inviteHint, _createInvite, _invites }
        };

        var content = new VerticalStackLayout
        {
            Padding = new Thickness(20, 12),
            Spacing = 14,
            Children = { header, _details, _editSettings, _status, membersHeading, _members, inviteSection }
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

    // NOTE: Full LoadAsync + member/invite methods restored from last good commit.
    // Profile photo UI will be re-applied in a follow-up push.
    private async Task LoadAsync()
    {
        if (_loading) return;
        _loading = true;
        _refresh.IsEnabled = false;
        _status.Text = "Loading household…";
        try
        {
            var family = await _family.GetFamilyAsync();
            _family = family;
            _name.Text = family.Name;
            _currentMemberId = family.CurrentMemberId;
            var me = family.Members.FirstOrDefault(m => m.Id == _currentMemberId);
            _isOwner = string.Equals(me?.Role, "Owner", StringComparison.OrdinalIgnoreCase);
            _isAdultOrOwner = _isOwner
                || string.Equals(me?.Role, "Adult", StringComparison.OrdinalIgnoreCase);
            _editSettings.IsVisible = _isOwner;

            var meta = new List<string>();
            if (!string.IsNullOrWhiteSpace(family.TimeZone)) meta.Add(family.TimeZone);
            if (!string.IsNullOrWhiteSpace(family.Currency)) meta.Add(family.Currency);
            meta.Add(_isOwner ? "You are the owner" : me is null ? "Member" : $"You are a {me.Role}");
            _details.Text = string.Join(" · ", meta);

            _members.Children.Clear();
            var members = family.Members
                .Where(m => m.IsActive)
                .OrderBy(RoleOrder)
                .ThenBy(m => m.DisplayName)
                .ToList();
            _status.Text = $"{members.Count} active member{(members.Count == 1 ? "" : "s")}";
            if (members.Count == 0)
                AddInfoCard("No active members", "Invite someone below to grow this household.");
            foreach (var member in members)
                AddMember(member);

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
        var isYou = member.Id == _currentMemberId;
        var initials = string.Join("", member.DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(2).Select(part => part[0])).ToUpperInvariant();
        var avatar = new Border
        {
            WidthRequest = 44,
            HeightRequest = 44,
            StrokeThickness = 0,
            BackgroundColor = Color.FromArgb(isYou ? "#29466F" : "#E8EEF9"),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 22 },
            Content = new Label
            {
                Text = string.IsNullOrWhiteSpace(initials) ? "?" : initials,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                TextColor = Color.FromArgb(isYou ? "#FFFFFF" : "#29466F"),
                FontAttributes = FontAttributes.Bold
            }
        };

        var identity = new VerticalStackLayout { Spacing = 3, VerticalOptions = LayoutOptions.Center };
        var titleRow = new HorizontalStackLayout { Spacing = 8 };
        var name = new Label { Text = member.DisplayName, FontSize = 16, FontAttributes = FontAttributes.Bold };
        name.SetDynamicResource(VisualElement.StyleProperty, "TextBodyStrong");
        titleRow.Children.Add(name);
        if (isYou)
        {
            var you = new Label
            {
                Text = "You",
                FontSize = 11,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#29466F"),
                VerticalTextAlignment = TextAlignment.Center
            };
            titleRow.Children.Add(you);
        }
        var role = new Label { Text = member.Role, FontSize = 12 };
        role.SetDynamicResource(VisualElement.StyleProperty, "TextCaption");
        identity.Children.Add(titleRow);
        identity.Children.Add(role);

        var memberContent = new VerticalStackLayout { Spacing = 10 };
        memberContent.Children.Add(new HorizontalStackLayout { Spacing = 12, Children = { avatar, identity } });

        if (_isOwner && !isYou)
        {
            var actions = new HorizontalStackLayout { Spacing = 8 };
            var roleButton = new Button { Text = "Change role", FontSize = 12 };
            roleButton.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
            roleButton.Clicked += async (_, _) => await ChangeMemberRoleAsync(member);
            var deactivateButton = new Button { Text = "Remove", FontSize = 12 };
            deactivateButton.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
            deactivateButton.Clicked += async (_, _) => await DeactivateMemberAsync(member);
            actions.Children.Add(roleButton);
            actions.Children.Add(deactivateButton);
            memberContent.Children.Add(actions);
        }

        var card = new Border { Padding = 12, Content = memberContent };
        card.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");
        _members.Children.Add(card);
    }

    private async Task EditHouseholdAsync()
    {
        if (_family is null || !_isOwner) return;
        var name = await DisplayPromptAsync(
            "Household name",
            "What should this family be called?",
            "Next",
            "Cancel",
            initialValue: _family.Name);
        if (name is null) return;
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            await DisplayAlertAsync("Name required", "Enter a household name.", "OK");
            return;
        }

        var timeZone = await DisplayPromptAsync(
            "Time zone",
            "IANA time zone (example: America/New_York). Leave blank to keep current.",
            "Next",
            "Cancel",
            initialValue: _family.TimeZone ?? "");
        if (timeZone is null) return;

        var currency = await DisplayPromptAsync(
            "Currency",
            "3-letter code (example: USD). Leave blank to keep current.",
            "Save",
            "Cancel",
            initialValue: _family.Currency ?? "");
        if (currency is null) return;

        try
        {
            await _family.UpdateFamilyAsync(
                name,
                string.IsNullOrWhiteSpace(timeZone) ? _family.TimeZone : timeZone.Trim(),
                string.IsNullOrWhiteSpace(currency) ? _family.Currency : currency.Trim().ToUpperInvariant());
            await LoadAsync();
            await DisplayAlertAsync("Saved", "Household settings updated.", "OK");
        }
        catch (FamilyApiException ex)
        {
            await DisplayAlertAsync("Could not update household", ex.Message, "OK");
        }
        catch
        {
            await DisplayAlertAsync("Could not update household", "Try again.", "OK");
        }
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
            _createInvite.IsVisible = _isAdultOrOwner;
            foreach (var invite in invites.OrderByDescending(x => x.IsOpen))
            {
                var state = invite.IsOpen
                    ? $"Open · expires {invite.ExpiresAtUtc.ToLocalTime():g}"
                    : invite.AcceptedAtUtc.HasValue ? "Accepted" : "Closed or expired";
                var stack = new VerticalStackLayout { Spacing = 4 };
                stack.Children.Add(new Label { Text = $"{invite.Role} invitation", FontAttributes = FontAttributes.Bold });
                stack.Children.Add(new Label
                {
                    Text = $"{invite.EmailHint ?? "Anyone with the code"} · {state}",
                    FontSize = 12
                });
                if (invite.IsOpen)
                {
                    var codeLabel = new Label
                    {
                        Text = invite.Code,
                        FontSize = 18,
                        FontAttributes = FontAttributes.Bold
                    };
                    stack.Children.Add(codeLabel);

                    var actions = new HorizontalStackLayout { Spacing = 8 };
                    var copy = new Button { Text = "Copy", FontSize = 12 };
                    copy.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
                    copy.Clicked += async (_, _) =>
                    {
                        await Microsoft.Maui.ApplicationModel.DataTransfer.Clipboard.Default.SetTextAsync(invite.Code);
                        await DisplayAlertAsync("Copied", "Invitation code is on the clipboard.", "Done");
                    };
                    var share = new Button { Text = "Share", FontSize = 12 };
                    share.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
                    share.Clicked += async (_, _) =>
                    {
                        var household = _family?.Name ?? "our family";
                        await Share.Default.RequestAsync(new ShareTextRequest
                        {
                            Title = "FamilyOS invitation",
                            Text = $"Join {household} on FamilyOS.\n\nInvitation code: {invite.Code}\nRole: {invite.Role}\nExpires: {invite.ExpiresAtUtc.ToLocalTime():g}"
                        });
                    };
                    var revoke = new Button { Text = "Revoke", FontSize = 12 };
                    revoke.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
                    revoke.Clicked += async (_, _) => await RevokeInviteAsync(invite);
                    actions.Children.Add(copy);
                    actions.Children.Add(share);
                    actions.Children.Add(revoke);
                    stack.Children.Add(actions);
                }
                var card = new Border { Padding = 12, Content = stack };
                card.SetDynamicResource(VisualElement.StyleProperty, "FoCardFrameStyle");
                _invites.Children.Add(card);
            }
            if (invites.Count == 0)
                AddInviteInfo("No invitations yet", "Create an invitation to add someone to this household.");
        }
        catch (FamilyApiException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
        {
            _createInvite.IsVisible = false;
            AddInviteInfo("Invitations are managed by adults", "Household members are listed above.");
        }
        catch
        {
            _createInvite.IsVisible = _isAdultOrOwner;
            AddInviteInfo("Invitations unavailable", "Pull to refresh and try again.");
        }
    }

    private async Task CreateInviteAsync()
    {
        var role = await DisplayActionSheetAsync("Invite someone as", "Cancel", null, "Adult", "Teen", "Child");
        if (string.IsNullOrWhiteSpace(role) || role == "Cancel") return;
        var email = await DisplayPromptAsync(
            "Optional email",
            "Restrict this invitation to an email address, or leave blank for anyone with the code.",
            "Create",
            "Cancel",
            keyboard: Keyboard.Email);
        if (email is null) return;
        _createInvite.IsEnabled = false;
        try
        {
            var invite = await _family.CreateInviteAsync(role, string.IsNullOrWhiteSpace(email) ? null : email.Trim());
            await Microsoft.Maui.ApplicationModel.DataTransfer.Clipboard.Default.SetTextAsync(invite.Code);
            var share = await DisplayAlertAsync(
                "Invitation created",
                $"Code: {invite.Code}\n\nCopied to clipboard. Expires {invite.ExpiresAtUtc.ToLocalTime():g}.\n\nShare it now?",
                "Share",
                "Done");
            if (share)
            {
                var household = _family?.Name ?? "our family";
                await Share.Default.RequestAsync(new ShareTextRequest
                {
                    Title = "FamilyOS invitation",
                    Text = $"Join {household} on FamilyOS.\n\nInvitation code: {invite.Code}\nRole: {invite.Role}\nExpires: {invite.ExpiresAtUtc.ToLocalTime():g}"
                });
            }
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
