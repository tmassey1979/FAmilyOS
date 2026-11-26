namespace FamilyOS.Maui.Controls;

/// <summary>
/// Compact offline / pending-sync strip. Subscribe to OfflineStore from the host page.
/// </summary>
public sealed class OfflineBanner : Border
{
    private readonly Label _label = new()
    {
        FontSize = 13,
        HorizontalTextAlignment = TextAlignment.Center,
        VerticalTextAlignment = TextAlignment.Center
    };

    public OfflineBanner()
    {
        IsVisible = false;
        Padding = new Thickness(12, 8);
        StrokeThickness = 0;
        BackgroundColor = Color.FromArgb("#B45309");
        Content = _label;
        _label.TextColor = Colors.White;
    }

    public void Apply(bool isOnline, int pendingCount)
    {
        if (isOnline && pendingCount <= 0)
        {
            IsVisible = false;
            return;
        }

        IsVisible = true;
        if (!isOnline && pendingCount > 0)
            _label.Text = $"Offline · {pendingCount} change(s) will sync when you reconnect";
        else if (!isOnline)
            _label.Text = "You are offline · showing saved data when available";
        else
            _label.Text = $"Online · syncing {pendingCount} queued change(s)…";
    }
}
