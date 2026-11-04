using System.Security.Principal;
using System.Text.Json;
using AMZ.Resolution.Core;
using AMZ.Resolution.Services;
using AMZ.Resolution.UI;

namespace AMZ.Resolution;

internal static class Program
{
    // Keep the per-user protocol shared with pre-rename clients and their recovery helpers.
    internal static string MutexName => @"Local\AmdResolutionTray-" + WindowsIdentity.GetCurrent().User!.Value;

    [STAThread]
    private static int Main(string[] args)
    {
        if (args is ["--apply-update", var job]) return UpdateInstaller.Run(job);
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => ShowError(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => AppStorage.Log((Exception)e.ExceptionObject);
        try
        {
            WindowsDisplayBackend.VerifyLayouts();
            if (args is ["--watchdog", var parent, var session, var ticks])
                return SafetyMonitor.RunWatchdog(int.Parse(parent), Guid.Parse(session), long.Parse(ticks));
            if (args is ["--diagnose", var output]) return Diagnose(output);
            if (args is ["--probe-update-download", var updateOutput]) return ProbeUpdateDownload(updateOutput);
            if (args is ["--scan-games", var catalogOutput]) return ScanGames(catalogOutput);
            if (args is ["--preview-settings"]) return PreviewSettings();
            Guid? updated = null;
            if (args is ["--updated", _] or ["--updated", _, "--resume-monitoring"])
                updated = Guid.Parse(args[1]);
            var resume = args is ["--resume-monitoring"] or ["--updated", _, "--resume-monitoring"];
            if (args.Length != 0 && args is not ["--settings"] && args is not ["--quit"] && updated is null && !resume)
                throw new ArgumentException("Arguments : --settings, --quit, --diagnose <fichier.json> ou --scan-games <fichier.json>.");
            if (updated is null && UpdateInstaller.IsInstalling())
            {
                MessageBox.Show("Une mise à jour est en cours. L’application va redémarrer automatiquement.", "AMZ.Resolution");
                return 0;
            }
            using var appLock = new Mutex(false, MutexName);
            try
            {
                if (!appLock.WaitOne(0))
                {
                    try
                    {
                        using var signal = EventWaitHandle.OpenExisting(MutexName + (args is ["--quit"] ? "-Quit" : "-Settings"));
                        signal.Set();
                    }
                    catch (WaitHandleCannotBeOpenedException)
                    {
                        MessageBox.Show("AMZ.Resolution démarre déjà. Réessayez dans quelques secondes.", "AMZ.Resolution");
                    }
                    return 0;
                }
            }
            catch (AbandonedMutexException) { }
            try
            {
                if (args is ["--quit"]) return 0;
                using var context = new TrayContext(args is ["--settings"], updated, resume);
                Application.Run(context);
            }
            finally { appLock.ReleaseMutex(); }
            return 0;
        }
        catch (Exception ex) { ShowError(ex); return 1; }
    }

    private static int Diagnose(string output)
    {
        object result;
        var exitCode = 0;
        try
        {
            var backend = new WindowsDisplayBackend();
            var games = new SelectedGamePresence();
            games.Update(SettingsStore.Load().GameList);
            result = new
            {
                Application = "AMZ.Resolution", Version = GitHubUpdateClient.CurrentVersion.ToString(3), ReadOnly = true,
                SelectedGameRunning = games.IsRunning(),
                Displays = backend.GetDisplays().Select(display =>
                {
                    ScalingState? scaling = null;
                    string? error = null;
                    try { scaling = backend.GetScaling(display.Identity); }
                    catch (Exception ex) { error = ex.Message; }
                    return new { Display = display, Scaling = scaling, Error = error,
                        Eligible = display.NativeWidth == 2560 && display.NativeHeight == 1440 };
                }).ToArray()
            };
        }
        catch (Exception ex) { result = new { ReadOnly = true, Error = ex.ToString() }; exitCode = 1; }
        File.WriteAllText(Path.GetFullPath(output), JsonSerializer.Serialize(result, AppStorage.Json));
        return exitCode;
    }

    private static int ProbeUpdateDownload(string output)
    {
        try
        {
            using var client = new GitHubUpdateClient();
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            var release = client.CheckAsync(cancellation.Token, new Version(0, 0, 0)).GetAwaiter().GetResult()
                ?? throw new InvalidDataException("Aucune version publiée à vérifier.");
            var update = client.DownloadAsync(release, null, cancellation.Token).GetAwaiter().GetResult();
            File.WriteAllText(output, JsonSerializer.Serialize(new { Version = update.Version.ToString(3), Verified = true, InstallAttempted = false }, AppStorage.Json));
            return 0;
        }
        catch (Exception ex)
        {
            File.WriteAllText(output, JsonSerializer.Serialize(new { Verified = false, Error = ex.ToString() }, AppStorage.Json));
            return 1;
        }
    }

    private static int PreviewSettings()
    {
        using var form = new SettingsForm(SettingsStore.Load(), new WindowsDisplayBackend().GetDisplays(), saveChanges: false);
        form.ShowDialog();
        return 0;
    }

    private static int ScanGames(string output)
    {
        var catalog = new InstalledGameScanner().ScanAsync(null, CancellationToken.None).GetAwaiter().GetResult();
        File.WriteAllText(Path.GetFullPath(output), JsonSerializer.Serialize(catalog, AppStorage.Json));
        return 0;
    }

    internal static void ShowError(Exception exception)
    {
        AppStorage.Log(exception);
        var text = exception is AggregateException aggregate
            ? string.Join("\n", aggregate.Flatten().InnerExceptions.Select(e => e.Message)) : exception.Message;
        MessageBox.Show(text, "AMZ.Resolution", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
