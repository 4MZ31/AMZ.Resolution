using System.Text.Json;

namespace AMZ.Resolution.Core;

public sealed record GameInstallation(string Id, string Name, string Source, string Directory, string? LaunchExecutable = null);

public static class GameManifests
{
    public static string[] SteamLibraries(string text) =>
        (ValveKeyValues.Parse(text).Child("libraryfolders")?.Children.Values.AsEnumerable() ?? [])
        .Select(v => v.Value ?? v.Text("path"))
        .Where(v => !string.IsNullOrWhiteSpace(v) && Path.IsPathFullyQualified(v))
        .Cast<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    public static GameInstallation? Steam(string text, string library)
    {
        var app = ValveKeyValues.Parse(text).Child("AppState");
        if (app is null || !ulong.TryParse(app.Text("appid"), out var id)
            || string.IsNullOrWhiteSpace(app.Text("name")) || string.IsNullOrWhiteSpace(app.Text("installdir"))) return null;
        var name = app.Text("name")!;
        if (id == 228980 || name.Contains("Redistributable", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("Steam Linux Runtime", StringComparison.OrdinalIgnoreCase)) return null;
        var directory = Inside(Path.Combine(library, "steamapps", "common"), app.Text("installdir")!);
        return directory is null ? null : new($"steam:{id}", name, "Steam", directory);
    }

    public static GameInstallation? Epic(string json)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
        var app = document.RootElement;
        if (app.ValueKind != JsonValueKind.Object) return null;
        static string? Text(JsonElement e, string key) => e.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        var name = Text(app, "DisplayName");
        var id = Text(app, "AppName");
        var directory = Text(app, "InstallLocation");
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(directory)
            || !Path.IsPathFullyQualified(directory) || id.StartsWith("UE_", StringComparison.OrdinalIgnoreCase)) return null;
        if (app.TryGetProperty("bIsIncompleteInstall", out var incomplete) && incomplete.ValueKind == JsonValueKind.True) return null;
        directory = Path.GetFullPath(directory);
        if (directory.TrimEnd(Path.DirectorySeparatorChar) == Path.GetPathRoot(directory)?.TrimEnd(Path.DirectorySeparatorChar)) return null;
        var launch = Text(app, "LaunchExecutable");
        return new($"epic:{id}", name, "Epic Games", directory,
            launch is null ? null : Inside(directory, launch.Replace('/', Path.DirectorySeparatorChar)));
    }

    public static string? Inside(string root, string relative)
    {
        if (Path.IsPathFullyQualified(relative) || Path.IsPathRooted(relative)) return null;
        var prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(prefix, relative));
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? path : null;
    }
}
