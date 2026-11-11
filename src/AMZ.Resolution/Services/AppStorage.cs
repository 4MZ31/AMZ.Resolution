using System.Text.Json;
using AMZ.Resolution.Core;

namespace AMZ.Resolution.Services;

internal static class AppStorage
{
    // Stable storage identity: renaming the product must preserve settings and recovery journals.
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
                {
                    JsonSerializer.Serialize(stream, value, Json);
                    stream.Flush(true);
                }
            File.Move(temp, target, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    internal static FileStream AcquireExclusive(string name)
    {
        Directory.CreateDirectory(DirectoryPath);
        return new FileStream(FilePath(name), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
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
            if (stream.Length > 64 * 1024) throw new InvalidDataException("Journal de récupération trop volumineux ; fichier conservé.");
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
        return LoadFrom(AppStorage.FilePath("settings.json"));
    }
    internal static string? LoadWarning { get; private set; }
    internal static AppSettings LoadFrom(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            if (stream.Length > 4_000_000) throw new InvalidDataException("Paramètres trop volumineux.");
            var value = JsonSerializer.Deserialize<AppSettings>(stream) ?? throw new InvalidDataException("Paramètres vides.");
            if (value.Width is < 640 or > 2560 || value.Height is < 480 or > 1440
                || value.RefreshRate != 0 && value.RefreshRate is < 24 or > 1000
                || value.Games is { Length: > 2000 } || value.CustomResolutions is { Length: > 256 }
                || value.Games?.Any(g => g is null || string.IsNullOrWhiteSpace(g.Id) || string.IsNullOrWhiteSpace(g.Name)
                    || g.Source is null || g.InstallDirectory is null || g.Executables is null || g.Candidates is null
                    || g.Executables.Length > 256 || g.Candidates.Length > 256
                    || g.Executables.Any(string.IsNullOrWhiteSpace) || g.Candidates.Any(string.IsNullOrWhiteSpace)) == true
                || value.CustomResolutions?.Any(r => r is null || r.Width is < 640 or > 2560 || r.Height is < 480 or > 1440) == true)
                throw new InvalidDataException("Paramètres invalides.");
            return value;
        }
        catch (FileNotFoundException) { return new(); }
        catch (DirectoryNotFoundException) { return new(); }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // Preserve the bytes before any future save; recovery.json is deliberately untouched.
            var backup = path + ".invalid-" + Guid.NewGuid().ToString("N");
            try { File.Move(path, backup); LoadWarning = "Les paramètres illisibles ont été conservés dans " + backup; }
            catch (Exception moveError) when (moveError is IOException or UnauthorizedAccessException)
            {
                LoadWarning = "Paramètres illisibles conservés. Leur accès doit être rétabli avant l’enregistrement.";
                blockedPath = path;
            }
            AppStorage.Log(ex);
            return new();
        }
    }
    private static string? blockedPath;
    internal static void Save(AppSettings settings) {
        if (blockedPath is not null) throw new IOException(LoadWarning);
        AppStorage.WriteAtomic("settings.json", settings);
    }
}
