namespace AMZ.Resolution.Core;

public static class GameExecutables
{
    private static readonly string[] Helpers =
    [
        "launcher", "bootstrap", "crash", "report", "unins", "setup", "install", "redist",
        "anticheat", "battleye", "service", "cef", "webhelper", "vconsole", "resourcecompiler",
        "shadercompile", "makevpk", "vtex", "dedicated", "prereq"
    ];
    public static bool IsCandidate(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        return path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            && !Helpers.Any(helper => name.Contains(helper, StringComparison.OrdinalIgnoreCase));
    }

    public static GameEntry FromInstallation(GameInstallation installation, IEnumerable<string> paths)
    {
        var candidates = paths.Where(IsCandidate).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();
        string[] selected;
        if (installation.Id == "steam:730")
        {
            selected = candidates.Where(p => Path.GetFileName(p).Equals("cs2.exe", StringComparison.OrdinalIgnoreCase)
                || Path.GetFileName(p).Equals("csgo.exe", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (selected.Length == 0) selected = ["cs2.exe", "csgo.exe"];
        }
        else
        {
            var shipping = candidates.Where(p => p.EndsWith("-Win64-Shipping.exe", StringComparison.OrdinalIgnoreCase)).ToArray();
            selected = shipping.Length > 0 ? shipping
                : installation.LaunchExecutable is { } launch && candidates.Contains(launch, StringComparer.OrdinalIgnoreCase) ? [launch]
                : candidates.Length == 1 ? candidates : [];
        }
        return new(installation.Id, installation.Name, installation.Source, installation.Directory, selected, candidates);
    }
}
