using System.Text.Json.Serialization;

namespace AMZ.Resolution.Core;

public sealed record GameEntry(string Id, string Name, string Source, string InstallDirectory,
    string[] Executables, string[] Candidates, bool Enabled = false)
{
    [JsonIgnore]
    public bool Ready => Executables is { Length: > 0 };
}

public static class GameCatalog
{
    public static GameEntry[] Defaults() =>
    [
        new("steam:730", "Counter-Strike 2 / CS:GO", "Steam", "", ["cs2.exe", "csgo.exe"], [], true)
    ];

    public static GameEntry[] Merge(IEnumerable<GameEntry> previous, IEnumerable<GameEntry> scanned)
    {
        var entries = previous.GroupBy(g => g.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        foreach (var found in scanned)
        {
            if (entries.TryGetValue(found.Id, out var saved))
            {
                var sameFolder = string.Equals(saved.InstallDirectory, found.InstallDirectory, StringComparison.OrdinalIgnoreCase);
                entries[found.Id] = found with
                {
                    Enabled = saved.Enabled,
                    Executables = sameFolder && saved.Ready ? saved.Executables : found.Executables
                };
            }
            else entries[found.Id] = found;
        }
        return entries.Values.OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }
}

public sealed class GameProcessMatcher(IEnumerable<GameEntry> games)
{
    private readonly string[] executables = games.Where(g => g.Enabled && g.Ready)
        .SelectMany(g => g.Executables).Where(p => !string.IsNullOrWhiteSpace(p))
        .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    public bool HasSelection => executables.Length > 0;
    public string[] ProcessNames => executables.Select(Path.GetFileNameWithoutExtension)
        .Where(n => !string.IsNullOrEmpty(n)).Cast<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    public bool Matches(string processName, string? fullPath)
    {
        foreach (var executable in executables)
        {
            if (!string.Equals(Path.GetFileNameWithoutExtension(executable), processName, StringComparison.OrdinalIgnoreCase)) continue;
            // Bare filenames keep the original CS2 / CS:GO migration working without a scan.
            if (Path.GetFileName(executable) == executable) return true;
            if (fullPath is not null && string.Equals(Path.GetFullPath(executable), Path.GetFullPath(fullPath), StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}
