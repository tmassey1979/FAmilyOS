using System.Net;
using FamilyOS.Maui.Services;

namespace FamilyOS.Maui.Pages;

/// <summary>Household management: members, roles, invites, and family settings.</summary>
public sealed partial class FamilyPage : ContentPage
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
    private FamilyDto? _familyDto;

    public FamilyPage(FamilyApiClient family, MeApiClient me)
    {
        _family = family;
        WireProfile(me);
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
        _createInvite = new Button { Text = "Create invite" };
        _createInvite.SetDynamicResource(VisualElement.StyleProperty, "FoPrimaryButtonStyle");
        _createInvite.Clicked += async (_, _) => await CreateInviteAsync();

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16),
                Spacing = 16,
                Children =
                {
                    header,
                    _details,
                    _status,
                    _editSettings,
                    new Label { Text = "Members", FontAttributes = FontAttributes.Bold, FontSize = 16 },
                    _members,
                    new Label { Text = "Invites", FontAttributes = FontAttributes.Bold, FontSize = 16 },
                    _invites,
                    _createInvite
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (_loading) return;
        _loading = true;
        _status.Text = "Loading household…";
        try
        {
            var family = await _family.GetMyFamilyAsync();
            _familyDto = family;
            _currentMemberId = family.CurrentMemberId;
            var me = family.Members.FirstOrDefault(m => m.Id == family.CurrentMemberId);
            _isOwner = string.Equals(me?.Role, "Owner", StringComparison.OrdinalIgnoreCase);
            _isAdultOrOwner = _isOwner || string.Equals(me?.Role, "Adult", StringComparison.OrdinalIgnoreCase);
            _name.Text = family.Name;
            _details.Text = $"{family.Members.Count(m => m.IsActive)} members · {family.TimeZone ?? "no timezone"} · {family.Currency ?? "USD"}";
            _editSettings.IsVisible = _isOwner;
            _createInvite.IsVisible = _isAdultOrOwner;
            _members.Children.Clear();
            foreach (var member in family.Members.OrderByDescending(m => m.Id == family.CurrentMemberId).ThenBy(m => m.DisplayName))
                AddMember(member);
            await LoadInvitesAsync();
            _status.Text = "Up to date";
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
        }
        finally
        {
            _loading = false;
        }
    }

    private void AddMember(FamilyMemberDto member)
    {
        var isYou = member.Id == _currentMemberId;
        var initials = string.Concat(member.DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(p => p[0])).ToUpperInvariant();
        if (initials.Length > 2) initials = initials[..2];

        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12,
            Padding = new Thickness(12),
        };

        var avatar = BuildMemberAvatar(member, isYou, initials);
        var info = new VerticalStackLayout
        {
            Spacing = 2,
            Children =
            {
                new Label { Text = isYou ? $"{member.DisplayName} (you)" : member.DisplayName, FontAttributes = FontAttributes.Bold },
                new Label { Text = member.Role + (member.IsActive ? "" : " · inactive"), FontSize = 12, TextColor = Colors.Gray }
            }
        };

        var actions = new VerticalStackLayout { Spacing = 4 };
        if (isYou)
            actions.Children.Add(BuildSelfProfileActions(member));
        else if (_isOwner && member.IsActive)
        {
            var roleButton = new Button { Text = "Role", FontSize = 12, Padding = new Thickness(8, 4) };
            roleButton.Clicked += async (_, _) => await ChangeMemberRoleAsync(member);
            var deactivateButton = new Button { Text = "Remove", FontSize = 12, Padding = new Thickness(8, 4) };
            deactivateButton.Clicked += async (_, _) => await DeactivateMemberAsync(member);
            actions.Children.Add(roleButton);
            actions.Children.Add(deactivateButton);
        }

        row.Children.Add(avatar);
        Grid.SetColumn(info, 1);
        row.Children.Add(info);
        Grid.SetColumn(actions, 2);
        row.Children.Add(actions);

        var frame = new Border { StrokeThickness = 1, Padding = 0, Content = row };
        _members.Children.Add(frame);
    }

    private async Task EditHouseholdAsync()
    {
        if (_familyDto is null || !_isOwner) return;
        var name = await DisplayPromptAsync("Household name", "Name", initialValue: _familyDto.Name);
        if (name is null) return;
        var timeZone = await DisplayPromptAsync("Time zone", "IANA time zone", initialValue: _familyDto.TimeZone ?? "");
        if (timeZone is null) return;
        var currency = await DisplayPromptAsync("Currency", "ISO currency code", initialValue: _familyDto.Currency ?? "");
        if (currency is null) return;
        try
        {
            await _family.UpdateHouseholdAsync(
                string.IsNullOrWhiteSpace(name) ? null : name.Trim(),
                string.IsNullOrWhiteSpace(timeZone) ? _familyDto.TimeZone : timeZone.Trim(),
                string.IsNullOrWhiteSpace(currency) ? _familyDto.Currency : currency.Trim().ToUpperInvariant());
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Update failed", ex.Message, "OK");
        }
    }

    private async Task ChangeMemberRoleAsync(FamilyMemberDto member)
    {
        var roles = new[] { "Owner", "Adult", "Teen", "Child" };
        var selected = await DisplayActionSheet("Change role", "Cancel", null, roles);
        if (selected is null or "Cancel") return;
        try
        {
            await _family.UpdateMemberRoleAsync(member.Id, selected);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Role change failed", ex.Message, "OK");
        }
    }

    private async Task DeactivateMemberAsync(FamilyMemberDto member)
    {
        if (!await DisplayAlert("Remove member", $"Remove {member.DisplayName} from the household?", "Remove", "Cancel"))
            return;
        try
        {
            await _family.RemoveMemberAsync(member.Id);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Remove failed", ex.Message, "OK");
        }
    }

    private async Task LoadInvitesAsync()
    {
        _invites.Children.Clear();
        if (!_isAdultOrOwner) return;
        try
        {
            var invites = await _family.GetInvitesAsync();
            foreach (var invite in invites.Where(i => i.IsOpen))
            {
                var row = new VerticalStackLayout
                {
                    Spacing = 4,
                    Children =
                    {
                        new Label { Text = $"{invite.Role} · {invite.Code}", FontAttributes = FontAttributes.Bold },
                        new Label { Text = $"Expires {invite.ExpiresAtUtc:u}", FontSize = 12, TextColor = Colors.Gray }
                    }
                };
                var share = new Button { Text = "Share", FontSize = 12 };
                share.Clicked += async (_, _) =>
                {
                    var household = _familyDto?.Name ?? "our family";
                    await Share.Default.RequestAsync(new ShareTextRequest
                    {
                        Title = "FamilyOS invite",
                        Text = $"Join {household} on FamilyOS with code {invite.Code}"
                    });
                };
                var revoke = new Button { Text = "Revoke", FontSize = 12 };
                revoke.Clicked += async (_, _) => await RevokeInviteAsync(invite);
                row.Children.Add(new HorizontalStackLayout { Spacing = 8, Children = { share, revoke } });
                _invites.Children.Add(row);
            }
            if (_invites.Children.Count == 0)
                _invites.Children.Add(new Label { Text = "No open invites", FontSize = 13, TextColor = Colors.Gray });
        }
        catch (Exception ex)
        {
            _invites.Children.Add(new Label { Text = ex.Message, FontSize = 12, TextColor = Colors.OrangeRed });
        }
    }

    private async Task CreateInviteAsync()
    {
        var roles = new[] { "Adult", "Teen", "Child" };
        var role = await DisplayActionSheet("Invite role", "Cancel", null, roles);
        if (role is null or "Cancel") return;
        var email = await DisplayPromptAsync("Email hint (optional)", "Email");
        if (email is null) return;
        try
        {
            var invite = await _family.CreateInviteAsync(role, string.IsNullOrWhiteSpace(email) ? null : email.Trim());
            var household = _familyDto?.Name ?? "our family";
            await DisplayAlert("Invite created", $"Code {invite.Code} for {household}", "OK");
            await LoadInvitesAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Invite failed", ex.Message, "OK");
        }
    }

    private async Task RevokeInviteAsync(FamilyInviteDto invite)
    {
        if (!await DisplayAlert("Revoke invite", $"Revoke code {invite.Code}?", "Revoke", "Cancel"))
            return;
        try { await _family.RevokeInviteAsync(invite.Id); await LoadInvitesAsync(); }
        catch (Exception ex) { await DisplayAlert("Revoke failed", ex.Message, "OK"); }
    }
}
