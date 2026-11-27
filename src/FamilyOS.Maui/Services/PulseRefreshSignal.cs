namespace FamilyOS.Maui.Services;

/// <summary>
/// Broadcasts task changes that should refresh the Family Pulse dashboard.
/// </summary>
public sealed class PulseRefreshSignal
{
    public event EventHandler? RefreshRequested;

    public void RequestRefresh()
    {
        RefreshRequested?.Invoke(this, EventArgs.Empty);
    }
}
