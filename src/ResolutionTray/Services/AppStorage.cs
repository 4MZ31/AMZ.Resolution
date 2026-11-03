using System.Text.Json;
using ResolutionTray.Core;

namespace ResolutionTray.Services;

internal static class AppStorage
{
    internal static readonly string DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AmdResolutionTray");
    internal static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    internal static string FilePath(string name) => Path.Combine(DirectoryPath, name);
    internal static void WriteAtomic<T>(string name, T value)
    {
        Directory.CreateDirectory(DirectoryPath);
        var target = FilePath(name);
        var temp = target + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                JsonSerializer.Serialize(stream, value, Json);
            File.Move(temp, target, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    internal static void Log(Exception error)
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            var file = FilePath("app.log");
            if (File.Exists(file) && new FileInfo(file).Length > 1_000_000) File.Move(file, file + ".old", true);
            File.AppendAllText(file, $"{DateTimeOffset.Now:O} {error}\n");
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

internal sealed class RecoveryStore : IRecoveryStore
{
    public RecoverySession? Load()
    {
        var path = AppStorage.FilePath("recovery.json");
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return JsonSerializer.Deserialize<RecoverySession>(stream)
                ?? throw new InvalidOperationException("Le fichier de récupération est vide. Il a été conservé.");
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }
    public void Save(RecoverySession session) => AppStorage.WriteAtomic("recovery.json", session);
    public void Delete() => File.Delete(AppStorage.FilePath("recovery.json"));
}

internal sealed record AppSettings(string? DisplayIdentity = null, bool StartWithWindows = false,
    string? VerifiedProfile = null, GameEntry[]? Games = null, bool AutoUpdate = true,
    int Width = 2324, int Height = 1306, int RefreshRate = 0, ResolutionSize[]? CustomResolutions = null, string? CompatibilityKey = null, bool DarkMode = true)
{
    internal GameEntry[] GameList => Games ?? (DisplayIdentity is null ? [] : GameCatalog.Defaults());
}
internal static class SettingsStore
{
    internal static AppSettings Load()
    {
        var path = AppStorage.FilePath("settings.json");
        if (!File.Exists(path)) return new();
        return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? new();
    }
    internal static void Save(AppSettings settings) => AppStorage.WriteAtomic("settings.json", settings);
}
