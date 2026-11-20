using System.Text;
#if ANDROID
using Android.Content;
using Android.OS;
using Android.Provider;
#endif

namespace FamilyOS.Maui.Services;

/// <summary>
/// Local-only startup breadcrumbs. The in-progress marker survives process death.
/// A copy is mirrored to Android Documents/FamilyOS through MediaStore on Android 10+.
/// </summary>
public sealed class StartupDiagnosticsService
{
    private const long MaxLogBytes = 512 * 1024;
    private const string LogFileName = "FamilyOS-startup-diagnostics.log";
    private readonly object _gate = new();
    private readonly string _logPath;
    private readonly string _markerPath;
    private string _documentsStatus = "Documents export has not been attempted.";

    public StartupDiagnosticsService()
    {
        var directory = Path.Combine(FileSystem.AppDataDirectory, "Diagnostics");
        _logPath = Path.Combine(directory, LogFileName);
        _markerPath = Path.Combine(directory, "startup-in-progress.marker");
    }

    public void BeginLaunch()
    {
        lock (_gate)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!);
                if (File.Exists(_markerPath))
                    AppendUnsafe("CRASH SUSPECTED: previous process exited before startup completed.");
                File.WriteAllText(_markerPath, DateTimeOffset.UtcNow.ToString("O"));
                AppendUnsafe($"\n=== LAUNCH {DateTimeOffset.UtcNow:O} ===");
                AppendUnsafe($"App: {AppInfo.Name} {AppInfo.VersionString} ({AppInfo.BuildString})");
                AppendUnsafe($"Device: {DeviceInfo.Manufacturer} {DeviceInfo.Model}; {DeviceInfo.Platform} {DeviceInfo.VersionString}");
            }
            catch { /* Diagnostics must never prevent app startup. */ }
        }

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            RecordException("AppDomain.UnhandledException", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
            RecordException("TaskScheduler.UnobservedTaskException", args.Exception);
#if ANDROID
        Android.Runtime.AndroidEnvironment.UnhandledExceptionRaiser += (_, args) =>
            RecordException("AndroidEnvironment.UnhandledExceptionRaiser", args.Exception);
#endif
    }

    public void RecordCheckpoint(string name)
    {
        lock (_gate)
        {
            try { AppendUnsafe($"CHECKPOINT {DateTimeOffset.UtcNow:O} | {name}"); }
            catch { }
        }
    }

    public void RecordException(string source, Exception? exception)
    {
        lock (_gate)
        {
            try
            {
                AppendUnsafe($"\n!!! EXCEPTION {DateTimeOffset.UtcNow:O} | {source} !!!");
                AppendUnsafe(exception?.ToString() ?? "Exception details unavailable.");
            }
            catch { }
        }
    }

    public void MarkLaunchCompleted()
    {
        lock (_gate)
        {
            try
            {
                AppendUnsafe($"STARTUP COMPLETED {DateTimeOffset.UtcNow:O}");
                if (File.Exists(_markerPath)) File.Delete(_markerPath);
            }
            catch { }
        }
    }

    public string GetReport()
    {
        lock (_gate)
        {
            try { return File.Exists(_logPath) ? File.ReadAllText(_logPath) : "No startup diagnostics have been recorded yet."; }
            catch (Exception ex) { return $"Could not read diagnostics: {ex.Message}"; }
        }
    }

    public string GetDocumentsStatus()
    {
        lock (_gate) return _documentsStatus;
    }

    public string GetPrivateReportPath() => _logPath;

    public void Clear()
    {
        lock (_gate)
        {
            try
            {
                if (File.Exists(_logPath)) File.Delete(_logPath);
                AppendUnsafe($"Diagnostics cleared {DateTimeOffset.UtcNow:O}");
                if (File.Exists(_markerPath)) File.Delete(_markerPath);
            }
            catch { }
        }
    }

    private void AppendUnsafe(string line)
    {
        File.AppendAllText(_logPath, line + Environment.NewLine, Encoding.UTF8);
        var info = new FileInfo(_logPath);
        if (info.Length > MaxLogBytes)
        {
            var content = File.ReadAllText(_logPath);
            var keepFrom = Math.Max(0, content.Length - (int)(MaxLogBytes * 0.75));
            var nextBoundary = content.IndexOf('\n', keepFrom);
            if (nextBoundary >= 0) keepFrom = nextBoundary + 1;
            File.WriteAllText(_logPath, content[keepFrom..], Encoding.UTF8);
        }
        ExportToDocumentsUnsafe();
    }

    private void ExportToDocumentsUnsafe()
    {
#if ANDROID
        try
        {
            if (Build.VERSION.SdkInt < BuildVersionCodes.Q)
            {
                _documentsStatus = "Public Documents export requires Android 10 or later. Use Share report to export the private log.";
                return;
            }

            var resolver = Android.App.Application.Context.ContentResolver!;
            var collection = MediaStore.Files.GetContentUri("external");
            const string relativePath = "Documents/FamilyOS/";
            var selection = $"{MediaStore.IMediaColumns.DisplayName}=? AND {MediaStore.IMediaColumns.RelativePath}=?";
            resolver.Delete(collection, selection, new[] { LogFileName, relativePath });

            using var values = new ContentValues();
            values.Put(MediaStore.IMediaColumns.DisplayName, LogFileName);
            values.Put(MediaStore.IMediaColumns.MimeType, "text/plain");
            values.Put(MediaStore.IMediaColumns.RelativePath, relativePath);
            var uri = resolver.Insert(collection, values);
            if (uri is null)
            {
                _documentsStatus = "Android could not create Documents/FamilyOS log file.";
                return;
            }

            using var output = resolver.OpenOutputStream(uri, "w");
            if (output is null)
            {
                _documentsStatus = "Android created the log entry but could not open it for writing.";
                return;
            }

            var bytes = File.ReadAllBytes(_logPath);
            output.Write(bytes, 0, bytes.Length);
            output.Flush();
            _documentsStatus = $"Updated Documents/FamilyOS/{LogFileName}";
        }
        catch (Exception ex)
        {
            _documentsStatus = $"Documents export failed: {ex.GetType().Name}: {ex.Message}. The in-app log is still available.";
        }
#else
        _documentsStatus = "Public Documents export is available on Android 10+. The in-app report remains available.";
#endif
    }
}
