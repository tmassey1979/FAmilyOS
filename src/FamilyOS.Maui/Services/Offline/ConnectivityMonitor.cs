using Microsoft.Maui.Networking;

namespace FamilyOS.Maui.Services.Offline;

/// <summary>
/// Watches network connectivity, updates OfflineStore, and flushes the mutation queue on reconnect.
/// </summary>
public sealed class ConnectivityMonitor
{
    private readonly OfflineStore _store;
    private readonly OfflineFlushService _flush;
    private int _started;

    public ConnectivityMonitor(OfflineStore store, OfflineFlushService flush)
    {
        _store = store;
        _flush = flush;
    }

    public void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
            return;

        ApplyCurrent();
        Connectivity.ConnectivityChanged += OnConnectivityChanged;
    }

    public void Stop()
    {
        if (Interlocked.Exchange(ref _started, 0) == 0)
            return;
        Connectivity.ConnectivityChanged -= OnConnectivityChanged;
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e) =>
        MainThread.BeginInvokeOnMainThread(ApplyCurrent);

    private void ApplyCurrent()
    {
        var online = Connectivity.NetworkAccess == NetworkAccess.Internet;
        if (online)
        {
            _store.MarkOnline();
            _ = SafeFlushAsync();
        }
        else
        {
            _store.MarkOffline();
        }
    }

    private async Task SafeFlushAsync()
    {
        try { await _flush.FlushAsync().ConfigureAwait(false); }
        catch { /* best-effort */ }
    }
}
