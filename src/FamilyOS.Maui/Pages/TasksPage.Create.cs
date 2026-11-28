using FamilyOS.Maui.Services;
using System.Globalization;

namespace FamilyOS.Maui.Pages;

public sealed partial class TasksPage
{
    private async Task CreateTaskAsync()
    {
        try
        {
            var family = await _family.GetMyFamilyAsync();
            var members = family.Members.Where(m => m.IsActive).OrderBy(m => m.DisplayName).ToList();
            string? title = null;
            string? notes = null;
            Guid? assigneeId = null;
            DateTime? due = null;

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                title = await DisplayPromptAsync("New task", "Title", accept: "Next", cancel: "Cancel", maxLength: 120);
                if (string.IsNullOrWhiteSpace(title)) return;
                notes = await DisplayPromptAsync("New task", "Notes (optional)", accept: "Next", cancel: "Skip", maxLength: 500);
                if (members.Count > 0)
                {
                    var names = members.Select(m => m.DisplayName).ToArray();
                    var pick = await DisplayActionSheet("Assign to", "Unassigned", null, names);
                    if (pick is not null && pick != "Unassigned")
                    {
                        var match = members.FirstOrDefault(m => m.DisplayName == pick);
                        assigneeId = match?.Id;
                    }
                }
                var dueRaw = await DisplayPromptAsync("New task", "Due date (yyyy-MM-dd) optional", accept: "Create", cancel: "Skip", maxLength: 20);
                if (!string.IsNullOrWhiteSpace(dueRaw) && DateTime.TryParse(dueRaw, out var parsed))
                    due = parsed.Date;
            });

            if (string.IsNullOrWhiteSpace(title)) return;
            await _tasks.CreateTaskAsync(title.Trim(), notes, assigneeId, due);
            _pulseRefresh.RequestRefresh();
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Could not create task", ex.Message, "OK");
        }
    }

    private Button CreateRecurrenceButton()
    {
        var button = new Button { Text = "New recurring task", FontSize = 13, HeightRequest = 40 };
        button.SetDynamicResource(VisualElement.StyleProperty, "FoGhostButtonStyle");
        button.Clicked += async (_, _) => await CreateRecurrenceAsync();
        return button;
    }

    private async Task CreateRecurrenceAsync()
    {
        try
        {
            var title = await DisplayPromptAsync("Recurring task", "Title", accept: "Create", cancel: "Cancel", maxLength: 120);
            if (string.IsNullOrWhiteSpace(title)) return;
            await _tasks.CreateRecurrenceAsync(title.Trim(), "Weekly");
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Could not create recurrence", ex.Message, "OK");
        }
    }
}
