using System.IO.Compression;
using System.Text.Json;
using AMZ.Resolution.Core;

internal static class UpdateTests
{
    private static (string, Func<Task>) Test(string name, Action action) => (name, () => { action(); return Task.CompletedTask; });
    private static void Check(bool condition) { if (!condition) throw new Exception("Comportement de mise à jour inattendu."); }
    private static void Fails(Action action)
    {
        try { action(); } catch (Exception ex) when (ex is IOException or InvalidDataException) { return; }
        throw new Exception("Échec attendu.");
    }
    private static async Task FailsAsync(Func<Task> action)
    {
        try { await action(); } catch (Exception ex) when (ex is IOException or InvalidDataException) { return; }
        throw new Exception("Échec attendu.");
    }
    private static Dictionary<string, object?> Release(string version, bool draft = false, string host = "github.com")
    {
        var name = $"AMZ.Resolution-{version}-win-x64.zip";
        Dictionary<string, object?> Asset(string filename) => new()
        {
            ["name"] = filename, ["size"] = 1000, ["state"] = "uploaded",
            ["browser_download_url"] = $"https://{host}/{UpdateRelease.Repository}/releases/download/v{version}/{filename}"
        };
        return new() { ["tag_name"] = "v" + version, ["draft"] = draft, ["prerelease"] = true,
            ["assets"] = new[] { Asset(name), Asset(name + ".sha256") } };
    }
    private static UpdateRelease? Select(params Dictionary<string, object?>[] items) =>
        UpdateRelease.Select(JsonSerializer.Serialize(items), new Version(0, 4, 0));

    internal static IEnumerable<(string, Func<Task>)> Cases()
    {
        yield return Test("MAJ: version publiée la plus récente, ordre numérique", () =>
            Check(Select(Release("0.5.0"), Release("0.10.0"), Release("0.9.0"))!.Version == new Version(0, 10, 0)));
        yield return Test("MAJ: brouillons ignorés", () => Check(Select(Release("0.9.0", true), Release("0.5.0"))!.Version == new Version(0, 5, 0)));
        yield return Test("MAJ: versions expérimentales du projet acceptées", () => Check(Select(Release("0.5.0")) is not null));
        yield return Test("MAJ: aucune réinstallation ou rétrogradation", () => Check(Select(Release("0.4.0"), Release("0.3.0")) is null));
        yield return Test("MAJ: tags non numériques ignorés", () => Check(!UpdateRelease.TryVersion("v0.5.0-beta", out _) && !UpdateRelease.TryVersion("v0.5.0.1", out _)));
        yield return Test("MAJ: URL d’un autre domaine refusée", () => Check(Select(Release("0.5.0", host: "example.com")) is null));
        yield return Test("MAJ: URL d’un autre dépôt refusée", () =>
        {
            var release = Release("0.5.0"); var assets = (Dictionary<string, object?>[])release["assets"]!;
            assets[0]["browser_download_url"] = "https://github.com/other/repo/releases/download/v0.5.0/file.zip";
            Check(Select(release) is null);
        });
        yield return Test("MAJ: archive sans checksum ignorée", () =>
        {
            var release = Release("0.5.0"); release["assets"] = ((Dictionary<string, object?>[])release["assets"]!)[..1];
            Check(Select(release) is null);
        });
        yield return Test("MAJ: archive pas encore publiée ignorée", () =>
        {
            var release = Release("0.5.0"); ((Dictionary<string, object?>[])release["assets"]!)[0]["state"] = "new";
            Check(Select(release) is null);
        });
        yield return Test("MAJ: taille invalide ignorée", () =>
        {
            var release = Release("0.5.0"); ((Dictionary<string, object?>[])release["assets"]!)[0]["size"] = "not a number";
            Check(Select(release) is null);
        });
        yield return Test("MAJ: checksum conforme et casse acceptée", () => Check(UpdateArchive.ParseChecksum(new string('A', 64) + "  app.zip\n", "app.zip") == new string('a', 64)));
        yield return Test("MAJ: checksum attribué à un autre fichier refusé", () => Fails(() => UpdateArchive.ParseChecksum(new string('a', 64) + "  other.zip", "app.zip")));
        yield return Test("MAJ: empreinte invalide refusée", () => Fails(() => UpdateArchive.ParseChecksum("not-a-hash  app.zip", "app.zip")));
        yield return ("MAJ: extraction x64 vérifiée et entrées hors dossier ignorées", async () =>
        {
            using var files = new Files();
            var zip = files.Archive(("AMZ.Resolution.exe", Image()), ("../outside.txt", [1, 2, 3]));
            var payload = await UpdateArchive.ExtractAsync(zip, UpdateArchive.HashFile(zip), files.Root, CancellationToken.None);
            Check(File.Exists(payload) && Directory.GetFiles(files.Root).Length == 2);
        });
        yield return ("MAJ: archive corrompue refusée avant extraction", async () =>
        {
            using var files = new Files(); var zip = files.Archive(("AMZ.Resolution.exe", Image()));
            await FailsAsync(() => UpdateArchive.ExtractAsync(zip, new string('0', 64), files.Root, CancellationToken.None));
            Check(!File.Exists(Path.Combine(files.Root, "payload.exe")));
        });
        yield return ("MAJ: exécutable manquant refusé", async () =>
        {
            using var files = new Files(); var zip = files.Archive(("other.exe", Image()));
            await FailsAsync(() => UpdateArchive.ExtractAsync(zip, UpdateArchive.HashFile(zip), files.Root, CancellationToken.None));
        });
        yield return ("MAJ: doublon d’exécutable refusé", async () =>
        {
            using var files = new Files(); var zip = files.Archive(("AMZ.Resolution.exe", Image()), ("AMZ.Resolution.exe", Image()));
            await FailsAsync(() => UpdateArchive.ExtractAsync(zip, UpdateArchive.HashFile(zip), files.Root, CancellationToken.None));
        });
        yield return ("MAJ: fichier préexistant conservé si extraction refusée", async () =>
        {
            using var files = new Files(); var zip = files.Archive(("AMZ.Resolution.exe", Image()));
            var existing = files.Write("payload.exe", "existing data");
            await FailsAsync(() => UpdateArchive.ExtractAsync(zip, UpdateArchive.HashFile(zip), files.Root, CancellationToken.None));
            Check(File.ReadAllText(existing) == "existing data");
        });
        yield return ("MAJ: mauvais format binaire supprimé", async () =>
        {
            using var files = new Files(); var zip = files.Archive(("AMZ.Resolution.exe", new byte[256]));
            await FailsAsync(() => UpdateArchive.ExtractAsync(zip, UpdateArchive.HashFile(zip), files.Root, CancellationToken.None));
            Check(!File.Exists(Path.Combine(files.Root, "payload.exe")));
        });
        yield return ("MAJ: extraction annulée sans fichier partiel", async () =>
        {
            using var files = new Files(); var zip = files.Archive(("AMZ.Resolution.exe", Image()));
            using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
            try { await UpdateArchive.ExtractAsync(zip, UpdateArchive.HashFile(zip), files.Root, cancellation.Token); throw new Exception("Annulation attendue"); }
            catch (OperationCanceledException) { }
            Check(!File.Exists(Path.Combine(files.Root, "payload.exe")));
        });
        yield return Test("MAJ: remplacement conservé après confirmation du démarrage", () =>
        {
            using var files = new Files(); var target = files.Write("target.exe", "old"); var payload = files.Write("new.exe", "new");
            var settings = files.Write("settings.json", "preferences");
            UpdateTransaction.Install(target, payload, UpdateArchive.HashFile(target), UpdateArchive.HashFile(payload), () => File.ReadAllText(target) == "new");
            Check(File.ReadAllText(target) == "new" && File.ReadAllText(settings) == "preferences");
            Check(!Directory.GetFiles(files.Root).Any(p => p.EndsWith(".backup", StringComparison.Ordinal)));
        });
        yield return Test("MAJ: démarrage non confirmé restaure l’ancienne version", () =>
        {
            using var files = new Files(); var target = files.Write("target.exe", "old"); var payload = files.Write("new.exe", "new");
            Fails(() => UpdateTransaction.Install(target, payload, UpdateArchive.HashFile(target), UpdateArchive.HashFile(payload), () => false));
            Check(File.ReadAllText(target) == "old");
        });
        yield return Test("MAJ: erreur de lancement restaure l’ancienne version", () =>
        {
            using var files = new Files(); var target = files.Write("target.exe", "old"); var payload = files.Write("new.exe", "new");
            Fails(() => UpdateTransaction.Install(target, payload, UpdateArchive.HashFile(target), UpdateArchive.HashFile(payload), () => throw new IOException("Cannot start")));
            Check(File.ReadAllText(target) == "old");
        });
        yield return Test("MAJ: cible modifiée ne doit pas être écrasée", () =>
        {
            using var files = new Files(); var target = files.Write("target.exe", "old"); var hash = UpdateArchive.HashFile(target);
            var payload = files.Write("new.exe", "new"); File.WriteAllText(target, "user change");
            Fails(() => UpdateTransaction.Install(target, payload, hash, UpdateArchive.HashFile(payload), () => true));
            Check(File.ReadAllText(target) == "user change");
        });
        yield return Test("MAJ: fichier téléchargé modifié ne remplace pas la cible", () =>
        {
            using var files = new Files(); var target = files.Write("target.exe", "old"); var payload = files.Write("new.exe", "new");
            var hash = UpdateArchive.HashFile(payload); File.WriteAllText(payload, "changed");
            Fails(() => UpdateTransaction.Install(target, payload, UpdateArchive.HashFile(target), hash, () => true));
            Check(File.ReadAllText(target) == "old");
        });
    }
    private static byte[] Image()
    {
        var image = new byte[256]; image[0] = 0x4d; image[1] = 0x5a;
        BitConverter.TryWriteBytes(image.AsSpan(0x3C), 128);
        BitConverter.TryWriteBytes(image.AsSpan(128), 0x4550);
        BitConverter.TryWriteBytes(image.AsSpan(132), (ushort)0x8664);
        return image;
    }
    private sealed class Files : IDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "AMZ.Resolution-updates-" + Guid.NewGuid().ToString("N"));
        internal Files() => Directory.CreateDirectory(Root);
        internal string Write(string name, string content) { var file = Path.Combine(Root, name); File.WriteAllText(file, content); return file; }
        internal string Archive(params (string Name, byte[] Content)[] entries)
        {
            var file = Path.Combine(Root, "update.zip");
            using var archive = ZipFile.Open(file, ZipArchiveMode.Create);
            foreach (var entry in entries) { using var output = archive.CreateEntry(entry.Name).Open(); output.Write(entry.Content); }
            return file;
        }
        public void Dispose()
        {
            foreach (var file in Directory.GetFiles(Root)) File.Delete(file);
            Directory.Delete(Root, false);
        }
    }
}
