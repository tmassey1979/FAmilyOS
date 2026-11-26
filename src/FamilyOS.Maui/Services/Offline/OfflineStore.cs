using System.Text.Json;

namespace FamilyOS.Maui.Services.Offline;

public sealed class CacheEntry
{
    public string Path { get; set; } = "";
    public string Json { get; set; } = "";
    public DateTime SavedAtUtc { get; set; }
}

public sealed class QueuedMutation
{
    public string Id { get; set; } = "";
    public string Path { get; set; } = "";
    public string Method { get; set; } = "POST";
    public string? Body { get; set; }
    public DateTime EnqueuedAtUtc { get; set; }
    public int Attempts { get; set; }
}

/// <summary>
/// Preference-backed GET cache + mutation queue (parity with Expo offline #52 / #44).
/// </summary>
public sealed class OfflineStore
{
    private const string MetaKey = "familyos.cache.meta.v1";
    private const string QueueKey = "familyos.mutationQueue.v1";
    private const string CachePrefix = "familyos.cache.v1:";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly List<Action<int>> _queueListeners = new();
    private readonly List<Action<bool>> _onlineListeners = new();
    private readonly object _gate = new();

    public bool IsOnline { get; private set; } = true;

    public void MarkOnline()
    {
        if (IsOnline) return;
        IsOnline = true;
        NotifyOnline();
    }

    public void MarkOffline()
    {
        if (!IsOnline) return;
        IsOnline = false;
        NotifyOnline();
    }

    public void SubscribeQueueCount(Action<int> listener)
    {
        lock (_gate) _queueListeners.Add(listener);
        listener(GetQueue().Count);
    }

    public void UnsubscribeQueueCount(Action<int> listener)
    {
        lock (_gate) _queueListeners.Remove(listener);
    }

    public void SubscribeOnline(Action<bool> listener)
    {
        lock (_gate) _onlineListeners.Add(listener);
        listener(IsOnline);
    }

    public void UnsubscribeOnline(Action<bool> listener)
    {
        lock (_gate) _onlineListeners.Remove(listener);
    }

    private void NotifyQueue()
    {
        var count = GetQueue().Count;
        List<Action<int>> copy;
        lock (_gate) copy = _queueListeners.ToList();
        foreach (var l in copy)
        {
            try { l(count); } catch { /* ignore */ }
        }
    }

    private void NotifyOnline()
    {
        var online = IsOnline;
        List<Action<bool>> copy;
        lock (_gate) copy = _onlineListeners.ToList();
        foreach (var l in copy)
        {
            try { l(online); } catch { /* ignore */ }
        }
    }

    public CacheEntry? GetCache(string path)
    {
        var raw = Preferences.Default.Get(CacheKey(path), string.Empty);
        if (string.IsNullOrWhiteSpace(raw)) return null;
        try { return JsonSerializer.Deserialize<CacheEntry>(raw, JsonOptions); }
        catch { return null; }
    }

    public void SetCache(string path, string json)
    {
        var entry = new CacheEntry
        {
            Path = path,
            Json = json,
            SavedAtUtc = DateTime.UtcNow
        };
        Preferences.Default.Set(CacheKey(path), JsonSerializer.Serialize(entry, JsonOptions));

        var meta = LoadMeta();
        if (!meta.Contains(path))
        {
            meta.Add(path);
            if (meta.Count > 80)
                meta = meta.Skip(meta.Count - 80).ToList();
            SaveMeta(meta);
        }
    }

    public List<QueuedMutation> GetQueue()
    {
        var raw = Preferences.Default.Get(QueueKey, string.Empty);
        if (string.IsNullOrWhiteSpace(raw)) return new();
        try
        {
            return JsonSerializer.Deserialize<List<QueuedMutation>>(raw, JsonOptions) ?? new();
        }
        catch
        {
            return new();
        }
    }

    public QueuedMutation Enqueue(string path, string method, string? body)
    {
        var item = new QueuedMutation
        {
            Id = $"q_{DateTime.UtcNow.Ticks:x}_{Guid.NewGuid().ToString("N")[..6]}",
            Path = path,
            Method = method.ToUpperInvariant(),
            Body = body,
            EnqueuedAtUtc = DateTime.UtcNow,
            Attempts = 0
        };
        var q = GetQueue();
        q.Add(item);
        if (q.Count > 100)
            q = q.Skip(q.Count - 100).ToList();
        SaveQueue(q);
        return item;
    }

    public void RemoveFromQueue(string id)
    {
        var q = GetQueue().Where(x => x.Id != id).ToList();
        SaveQueue(q);
    }

    public void BumpAttempt(string id)
    {
        var q = GetQueue();
        foreach (var item in q.Where(x => x.Id == id))
            item.Attempts++;
        SaveQueue(q);
    }

    public void ClearQueue() => SaveQueue(new List<QueuedMutation>());

    private void SaveQueue(List<QueuedMutation> items)
    {
        Preferences.Default.Set(QueueKey, JsonSerializer.Serialize(items, JsonOptions));
        NotifyQueue();
    }

    private static string CacheKey(string path) => CachePrefix + path;

    private static List<string> LoadMeta()
    {
        var raw = Preferences.Default.Get(MetaKey, string.Empty);
        if (string.IsNullOrWhiteSpace(raw)) return new();
        try { return JsonSerializer.Deserialize<List<string>>(raw) ?? new(); }
        catch { return new(); }
    }

    private static void SaveMeta(List<string> meta) =>
        Preferences.Default.Set(MetaKey, JsonSerializer.Serialize(meta));
}
