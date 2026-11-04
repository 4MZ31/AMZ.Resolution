using System.Text.Json;
using Microsoft.Win32;
using AMZ.Resolution.Core;

namespace AMZ.Resolution.Services;

internal sealed record GameScanProgress(string Message, int Count);
internal sealed record GameScanResult(GameEntry[] Games, int Warnings);

internal sealed class InstalledGameScanner
{
    internal Task<GameScanResult> ScanAsync(IProgress<GameScanProgress>? progress, CancellationToken cancellation) =>
        Task.Run(() => Scan(progress, cancellation), cancellation);

    private static GameScanResult Scan(IProgress<GameScanProgress>? progress, CancellationToken cancellation)
    {
        var games = new List<GameEntry>();
        var warnings = 0;
        var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in SteamRoots())
        {
            cancellation.ThrowIfCancellationRequested();
            libraries.Add(root);
            var file = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(file)) continue;
            try { foreach (var library in GameManifests.SteamLibraries(ReadManifest(file))) libraries.Add(library); }
            catch (Exception ex) when (IsManifestError(ex)) { warnings++; }
        }
        foreach (var library in libraries)
        {
            var folder = Path.Combine(library, "steamapps");
            foreach (var file in Files(folder, "appmanifest_*.acf", ref warnings))
            {
                cancellation.ThrowIfCancellationRequested();
                try
                {
                    var installation = GameManifests.Steam(ReadManifest(file), library);
                    if (installation is not null) AddInstallation(installation);
                }
                catch (Exception ex) when (IsManifestError(ex)) { warnings++; }
            }
        }
        var epic = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Epic", "EpicGamesLauncher", "Data", "Manifests");
        foreach (var file in Files(epic, "*.item", ref warnings))
        {
            cancellation.ThrowIfCancellationRequested();
            try
            {
                var installation = GameManifests.Epic(ReadManifest(file));
                if (installation is not null) AddInstallation(installation);
            }
            catch (Exception ex) when (IsManifestError(ex)) { warnings++; }
        }
        return new(games.DistinctBy(g => g.Id, StringComparer.OrdinalIgnoreCase).ToArray(), warnings);

        void AddInstallation(GameInstallation installation)
        {
            if (!Directory.Exists(installation.Directory) || games.Count >= 2000) return;
            progress?.Report(new($"{installation.Source} · {installation.Name}", games.Count));
            var paths = FindExecutables(installation.Directory, cancellation, out var incomplete).ToList();
            if (installation.LaunchExecutable is { } launch && File.Exists(launch)) paths.Add(launch);
            if (incomplete) warnings++;
            games.Add(GameExecutables.FromInstallation(installation, paths));
        }
    }

    private static string ReadManifest(string file)
    {
        if (new FileInfo(file).Length > 4_000_000) throw new FormatException("Manifeste trop volumineux.");
        return File.ReadAllText(file);
    }
    private static bool IsManifestError(Exception ex) => ex is IOException or UnauthorizedAccessException
        or FormatException or JsonException or ArgumentException or NotSupportedException;

    private static string[] Files(string directory, string pattern, ref int warnings)
    {
        if (!Directory.Exists(directory)) return [];
        try { return Directory.GetFiles(directory, pattern, SearchOption.TopDirectoryOnly); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { warnings++; return []; }
    }

    private static IEnumerable<string> SteamRoots()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Read(Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath");
        Read(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath");
        Read(Registry.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath");
        roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
        return roots.Where(Directory.Exists);
        void Read(RegistryKey registry, string path, string value)
        {
            try
            {
                using var key = registry.OpenSubKey(path);
                if (key?.GetValue(value) is string root && Path.IsPathFullyQualified(root)) roots.Add(Path.GetFullPath(root));
            }
            catch (System.Security.SecurityException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static readonly HashSet<string> IgnoredFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        ".egstore", ".git", "_CommonRedist", "Redist", "Redistributables", "__Installer", "Engine",
        "Content", "Paks", "Assets", "Movies", "Localization", "StreamingAssets", "Cache", "node_modules", "Support", "Tools", "SDK"
    };

    private static string[] FindExecutables(string root, CancellationToken cancellation, out bool incomplete)
    {
        incomplete = false;
        var found = new List<string>();
        var pending = new Queue<(string Path, int Depth)>();
        pending.Enqueue((root, 0));
        var visited = 0;
        while (pending.Count > 0)
        {
            cancellation.ThrowIfCancellationRequested();
            if (++visited > 256 || found.Count >= 256) { incomplete = true; break; }
            var (directory, depth) = pending.Dequeue();
            try
            {
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) continue;
                found.AddRange(Directory.EnumerateFiles(directory, "*.exe").Where(GameExecutables.IsCandidate).Take(256 - found.Count));
                if (depth >= 6) continue;
                foreach (var child in Directory.EnumerateDirectories(directory))
                {
                    if (IgnoredFolders.Contains(Path.GetFileName(child)) || (File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0) continue;
                    if (pending.Count < 512) pending.Enqueue((child, depth + 1));
                    else incomplete = true;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { incomplete = true; }
        }
        return found.ToArray();
    }
}
