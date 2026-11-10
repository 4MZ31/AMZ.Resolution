namespace AMZ.Resolution.Core;

public static class LocalGamePath
{
    public static bool IsLocal(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.StartsWith('\\') || path.StartsWith("//", StringComparison.Ordinal)
            || !Path.IsPathFullyQualified(path)) return false;
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path));
            if (root is null) return false;
            // On Windows this also excludes mapped network drives, before touching a directory.
            return !OperatingSystem.IsWindows() || new DriveInfo(root).DriveType == DriveType.Fixed;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException) { return false; }
    }
}
