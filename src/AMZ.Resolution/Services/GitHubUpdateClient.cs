using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using AMZ.Resolution.Core;

namespace AMZ.Resolution.Services;

internal sealed record PreparedUpdate(Version Version, string Directory, string Payload, string Hash);

internal sealed class GitHubUpdateClient : IDisposable
{
    internal static Version CurrentVersion
    {
        get
        {
            var version = typeof(Program).Assembly.GetName().Version!;
            return new(version.Major, version.Minor, Math.Max(0, version.Build));
        }
    }
    internal static string UpdatesRoot => AppStorage.FilePath("Updates");
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(30) };
    internal GitHubUpdateClient()
    {
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"AMZ.Resolution/{CurrentVersion.ToString(3)}");
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    }
    internal async Task<UpdateRelease?> CheckAsync(CancellationToken cancellation, Version? current = null)
    {
        var json = await TextAsync(new Uri($"https://api.github.com/repos/{UpdateRelease.Repository}/releases?per_page=30"), 4_000_000, false, cancellation);
        return UpdateRelease.Select(json, current ?? CurrentVersion);
    }
    internal async Task<PreparedUpdate> DownloadAsync(UpdateRelease release, IProgress<int>? progress, CancellationToken cancellation)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromMinutes(5));
        var token = deadline.Token;
        var directory = Path.Combine(UpdatesRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var checksum = await TextAsync(release.ChecksumUrl, 4096, true, token);
        var expected = UpdateArchive.ParseChecksum(checksum, release.ArchiveName);
        if (release.ArchiveDigest is { Length: > 0 } digest
            && !digest.Equals("sha256:" + expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Les empreintes SHA-256 publiées sur GitHub ne correspondent pas.");
        var archive = Path.Combine(directory, "archive.zip");
        using (var response = await http.GetAsync(release.ArchiveUrl, HttpCompletionOption.ResponseHeadersRead, token))
        {
            response.EnsureSuccessStatusCode();
            VerifyDownloadHost(response);
            if (response.Content.Headers.ContentLength is { } length && length != release.ArchiveSize)
                throw new InvalidDataException("La taille de l’archive ne correspond pas à la version publiée.");
            await using var source = await response.Content.ReadAsStreamAsync(token);
            await using var target = new FileStream(archive, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            var buffer = new byte[81920]; long total = 0; int read;
            while ((read = await source.ReadAsync(buffer, token)) > 0)
            {
                total += read;
                if (total > UpdateRelease.MaxArchiveBytes || total > release.ArchiveSize) throw new InvalidDataException("Archive trop volumineuse.");
                await target.WriteAsync(buffer.AsMemory(0, read), token);
                progress?.Report((int)(total * 100 / release.ArchiveSize));
            }
            if (total != release.ArchiveSize) throw new InvalidDataException("Le téléchargement est incomplet.");
        }
        var payload = await UpdateArchive.ExtractAsync(archive, expected, directory, token);
        VerifyExecutable(payload, release.Version);
        return new(release.Version, directory, payload, await Task.Run(() => UpdateArchive.HashFile(payload), token));
    }
    internal static void VerifyExecutable(string file, Version expected)
    {
        var info = FileVersionInfo.GetVersionInfo(file);
        if (info.FileMajorPart != expected.Major || info.FileMinorPart != expected.Minor || info.FileBuildPart != expected.Build
            || info.ProductName != "AMZ.Resolution")
            throw new InvalidDataException("La version de l’exécutable reçu ne correspond pas à la mise à jour.");
    }
    private async Task<string> TextAsync(Uri uri, int maxBytes, bool download, CancellationToken cancellation)
    {
        using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellation);
        response.EnsureSuccessStatusCode();
        if (download) VerifyDownloadHost(response);
        else if (response.RequestMessage?.RequestUri?.Host != "api.github.com") throw new InvalidDataException("Réponse GitHub inattendue.");
        await using var source = await response.Content.ReadAsStreamAsync(cancellation);
        using var target = new MemoryStream();
        var buffer = new byte[8192]; int read;
        while ((read = await source.ReadAsync(buffer, cancellation)) > 0)
        {
            if (target.Length + read > maxBytes) throw new InvalidDataException("Réponse GitHub trop volumineuse.");
            target.Write(buffer, 0, read);
        }
        return Encoding.UTF8.GetString(target.ToArray());
    }
    private static void VerifyDownloadHost(HttpResponseMessage response)
    {
        var uri = response.RequestMessage?.RequestUri;
        if (uri is null || uri.Scheme != "https" || !uri.IsDefaultPort || uri.UserInfo.Length != 0 || uri.Host is not ("github.com" or "release-assets.githubusercontent.com" or "objects.githubusercontent.com"))
            throw new InvalidDataException("Le téléchargement a été redirigé vers une adresse non reconnue.");
    }
    internal static void Cleanup(string? keepDirectory = null)
    {
        if (!Directory.Exists(UpdatesRoot) || UpdateInstaller.IsInstalling()) return;
        foreach (var directory in Directory.EnumerateDirectories(UpdatesRoot))
        {
            if (directory == keepDirectory || !Guid.TryParseExact(Path.GetFileName(directory), "N", out _)) continue;
            try
            {
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0
                    || System.IO.Directory.GetCreationTimeUtc(directory) > DateTime.UtcNow.AddHours(-1)) continue;
                foreach (var name in new[] { "archive.zip", "payload.exe", "AMZ.Resolution.Updater.exe", "install.json", "cancelled" })
                    File.Delete(Path.Combine(directory, name));
                System.IO.Directory.Delete(directory, false);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
    public void Dispose() => http.Dispose();
}
