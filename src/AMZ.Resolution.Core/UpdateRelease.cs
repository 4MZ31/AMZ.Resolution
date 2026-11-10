using System.Text.Json;

namespace AMZ.Resolution.Core;

public sealed record UpdateRelease(Version Version, string Tag, string ArchiveName, Uri ArchiveUrl,
    Uri ChecksumUrl, long ArchiveSize, string? ArchiveDigest)
{
    public const string Repository = "4MZ31/AMZ.Resolution";
    public const long MaxArchiveBytes = 200 * 1024 * 1024;
    public static bool TryVersion(string? tag, out Version version)
    {
        version = new(0, 0, 0);
        if (string.IsNullOrEmpty(tag)) return false;
        var value = tag.StartsWith('v') ? tag[1..] : tag;
        var pieces = value.Split('.');
        if (pieces.Length != 3 || pieces.Any(p => p.Length == 0 || !p.All(char.IsAsciiDigit))) return false;
        if (!Version.TryParse(value, out var parsed) || parsed.Build < 0) return false;
        version = parsed; return true;
    }
    public static UpdateRelease? Select(string json, Version current)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
        if (document.RootElement.ValueKind != JsonValueKind.Array) throw new FormatException("Réponse GitHub invalide.");
        var releases = new List<UpdateRelease>();
        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("draft", out var draft)
                || draft.ValueKind != JsonValueKind.False
                || !item.TryGetProperty("prerelease", out var preview) || preview.ValueKind != JsonValueKind.False) continue;
            var tag = Text(item, "tag_name");
            if (!TryVersion(tag, out var version) || version <= current) continue;
            if (!item.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array) continue;
            var name = $"AMZ.Resolution-{version.ToString(3)}-win-x64.zip";
            var archives = assets.EnumerateArray().Where(a => Text(a, "name") == name).ToArray();
            var checksums = assets.EnumerateArray().Where(a => Text(a, "name") == name + ".sha256").ToArray();
            if (archives.Length != 1 || checksums.Length != 1) continue;
            var archive = archives[0]; var checksum = checksums[0];
            if (Text(archive, "state") != "uploaded" || Text(checksum, "state") != "uploaded") continue;
            if (!archive.TryGetProperty("size", out var sizeNode) || sizeNode.ValueKind != JsonValueKind.Number || !sizeNode.TryGetInt64(out var size)
                || size < 100 || size > MaxArchiveBytes) continue;
            if (!TryAssetUrl(Text(archive, "browser_download_url"), tag!, name, out var archiveUrl)
                || !TryAssetUrl(Text(checksum, "browser_download_url"), tag!, name + ".sha256", out var checksumUrl)) continue;
            releases.Add(new(version, tag!, name, archiveUrl!, checksumUrl!, size, Text(archive, "digest")));
        }
        return releases.OrderByDescending(r => r.Version).FirstOrDefault();
    }
    private static string? Text(JsonElement node, string key) => node.ValueKind == JsonValueKind.Object
        && node.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static bool TryAssetUrl(string? value, string tag, string filename, out Uri? uri)
    {
        uri = null;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var parsed) || parsed.Scheme != "https"
            || parsed.Host != "github.com" || !parsed.IsDefaultPort || parsed.UserInfo.Length != 0
            || parsed.Query.Length != 0 || parsed.Fragment.Length != 0) return false;
        if (parsed.AbsolutePath != $"/{Repository}/releases/download/{Uri.EscapeDataString(tag)}/{filename}") return false;
        uri = parsed; return true;
    }
}
