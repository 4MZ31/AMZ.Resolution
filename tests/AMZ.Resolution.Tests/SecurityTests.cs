using System.Diagnostics;
using System.Text.Json;
using AMZ.Resolution.Core;
using AMZ.Resolution.Services;

internal static class SecurityTests
{
    private static void Check(bool value) { if (!value) throw new Exception("Régression sécurité/récupération."); }
    private static (string, Func<Task>) Test(string name, Action action) => (name, () => { action(); return Task.CompletedTask; });
    internal static IEnumerable<(string, Func<Task>)> Cases()
    {
        yield return Test("Watchdog: application confirmée active, aucune limite sur une longue partie", () =>
        {
            var d = new WatchdogDeadline();
            for (var i = 1; i <= 7200; i++) Check(!d.ShouldRecover(TimeSpan.FromSeconds(i), true, true));
        });
        yield return Test("Watchdog: absence de pulsation pendant 30 secondes", () =>
        {
            var d = new WatchdogDeadline();
            Check(!d.ShouldRecover(TimeSpan.FromSeconds(5), true, true));
            Check(!d.ShouldRecover(TimeSpan.FromSeconds(34), false, true));
            Check(d.ShouldRecover(TimeSpan.FromSeconds(35), false, true));
        });
        yield return Test("Watchdog: opération non confirmée limitée malgré une UI vivante", () =>
        {
            var d = new WatchdogDeadline();
            Check(!d.ShouldRecover(TimeSpan.FromSeconds(59), true, false));
            Check(d.ShouldRecover(TimeSpan.FromSeconds(60), true, false));
        });
        yield return Test("Watchdog: confirmation annule le délai de transaction", () =>
        {
            var d = new WatchdogDeadline();
            Check(!d.ShouldRecover(TimeSpan.FromSeconds(59), true, false));
            Check(!d.ShouldRecover(TimeSpan.FromSeconds(61), true, true));
        });
        foreach (var json in new[] { "{", "null", "{\"Games\":[null]}", "{\"Width\":0}", "{\"CustomResolutions\":[null]}" })
        {
            var invalid = json;
            yield return Test("Paramètres invalides conservés: " + invalid, () =>
            {
                var dir = NewDirectory();
                try
                {
                    var path = Path.Combine(dir, "settings.json"); File.WriteAllText(path, invalid);
                    var recovery = Path.Combine(dir, "recovery.json"); File.WriteAllText(recovery, "keep recovery");
                    Check(SettingsStore.LoadFrom(path) == new AppSettings());
                    Check(!File.Exists(path));
                    Check(File.ReadAllText(Directory.GetFiles(dir, "settings.json.invalid-*").Single()) == invalid);
                    Check(File.ReadAllText(recovery) == "keep recovery");
                }
                finally { Directory.Delete(dir, true); }
            });
        }
        yield return Test("Paramètres valides: préférences conservées", () =>
        {
            var dir = NewDirectory();
            try
            {
                var path = Path.Combine(dir, "settings.json"); var value = new AppSettings(Width: 1920, Height: 1080, DarkMode: false);
                File.WriteAllText(path, JsonSerializer.Serialize(value));
                Check(SettingsStore.LoadFrom(path) == value && Directory.GetFiles(dir).Length == 1);
            }
            finally { Directory.Delete(dir, true); }
        });
        yield return Test("Paramètres démesurés: conservation et repli", () =>
        {
            var dir = NewDirectory();
            try
            {
                var path = Path.Combine(dir, "settings.json"); File.WriteAllText(path, new string(' ', 4_000_001));
                Check(SettingsStore.LoadFrom(path) == new AppSettings());
                Check(Directory.GetFiles(dir, "*.invalid-*").Length == 1);
            }
            finally { Directory.Delete(dir, true); }
        });
        foreach (var path in new[] { @"\\server\games", @"\\?\C:\games", @"\\.\C:\games", "//server/games", "relative/path" })
        {
            var candidate = path;
            yield return Test("Scan: racine non locale refusée " + path, () => Check(!LocalGamePath.IsLocal(candidate)));
        }
        yield return ("MAJ: arrêt brutal après remplacement, récupération durable et idempotente", async () =>
        {
            var dir = NewDirectory();
            try
            {
                var target = Path.Combine(dir, "target.exe"); var payload = Path.Combine(dir, "payload.exe");
                File.WriteAllText(target, "old verified bytes"); File.WriteAllText(payload, "new unconfirmed bytes");
                var info = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false };
                if (Path.GetFileNameWithoutExtension(info.FileName) == "dotnet") info.ArgumentList.Add(typeof(SecurityTests).Assembly.Location);
                info.ArgumentList.Add("--interrupt-update"); info.ArgumentList.Add(target); info.ArgumentList.Add(payload);
                using var child = Process.Start(info)!;
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                try { await child.WaitForExitAsync(timeout.Token); }
                catch { if (!child.HasExited) child.Kill(); throw; }
                Check(child.ExitCode == 77 && File.ReadAllText(target) == "new unconfirmed bytes");
                Check(File.Exists(target + ".update.json"));
                UpdateTransaction.Recover(target); UpdateTransaction.Recover(target);
                Check(File.ReadAllText(target) == "old verified bytes");
                Check(!File.Exists(target + ".update.json") && Directory.GetFiles(dir, "*.backup").Length == 0);
            }
            finally { Directory.Delete(dir, true); }
        });
        yield return Test("MAJ: journal invalide conservé sans toucher la cible", () =>
        {
            var dir = NewDirectory();
            try
            {
                var target = Path.Combine(dir, "target.exe"); File.WriteAllText(target, "original");
                File.WriteAllText(target + ".update.json", "{}");
                try { UpdateTransaction.Recover(target); throw new Exception("Refus attendu"); }
                catch (InvalidDataException) { }
                Check(File.ReadAllText(target) == "original" && File.Exists(target + ".update.json"));
            }
            finally { Directory.Delete(dir, true); }
        });
    }
    private static string NewDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "AMZ-security-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir); return dir;
    }
}
