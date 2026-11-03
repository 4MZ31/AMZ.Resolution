using System.Security.Principal;
using System.Text.Json;
using ResolutionTray.Core;
using ResolutionTray.Services;
using ResolutionTray.UI;

namespace ResolutionTray;

internal static class Program
{
    internal static string MutexName => @"Local\AmdResolutionTray-" + WindowsIdentity.GetCurrent().User!.Value;

    [STAThread]
    private static int Main(string[] args)
    {
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
            if (args is ["--scan-games", var catalogOutput]) return ScanGames(catalogOutput);
            if (args is ["--preview-settings"]) return PreviewSettings();
            if (args.Length != 0 && args is not ["--settings"] && args is not ["--quit"])
                throw new ArgumentException("Arguments : --settings, --quit, --diagnose <fichier.json> ou --scan-games <fichier.json>.");
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
                        MessageBox.Show("Resolution Tray démarre déjà. Réessayez dans quelques secondes.", "Resolution Tray");
                    }
                    return 0;
                }
            }
            catch (AbandonedMutexException) { }
            try
            {
                if (args is ["--quit"]) return 0;
                using var context = new TrayContext(args.Length > 0);
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
                Application = "AMD Resolution Tray", Version = "0.3.0", ReadOnly = true,
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
        MessageBox.Show(text, "Resolution Tray", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
