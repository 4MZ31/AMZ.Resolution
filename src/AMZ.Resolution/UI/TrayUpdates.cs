using AMZ.Resolution.Services;

namespace AMZ.Resolution.UI;

internal sealed partial class TrayContext
{
    private readonly ToolStripMenuItem updateItem = new("Mettre à jour maintenant…");
    private readonly GitHubUpdateClient updater = new();
    private readonly CancellationTokenSource updateLifetime = new();
    private CancellationTokenSource? updateCancellation;
    private PreparedUpdate? pendingUpdate;
    private DateTimeOffset nextUpdateCheck;
    private Guid? updateStartupId;
    private bool resumeAfterUpdate;
    private bool updateBusy;
    private bool updateBlocked;
    private bool updateManual;

    private void InitializeUpdates(Guid? updated, bool resume)
    {
        updateStartupId = updated;
        resumeAfterUpdate = resume;
        nextUpdateCheck = updated.HasValue ? DateTimeOffset.UtcNow.AddHours(6) : DateTimeOffset.MinValue;
        updateItem.Click += async (_, _) => await CheckUpdatesAsync(manual: true);
    }
    private void AcknowledgeUpdateStartup()
    {
        // Acknowledge before opening settings or resuming any resolution change.
        if (updateStartupId is { } id) { UpdateInstaller.SignalHealthy(id); updateStartupId = null; }
        if (resumeAfterUpdate && settings.DisplayIdentity is not null && game.HasSelection) gameMode.Enable();
        resumeAfterUpdate = false;
    }
    private async Task UpdateTickAsync()
    {
        if (UpdateInstaller.TakeOutcome() is { } outcome)
            tray.ShowBalloonTip(6000, "AMZ.Resolution", outcome.Success
                ? $"Application mise à jour en v{outcome.Version}. Vos réglages sont conservés."
                : $"Mise à jour annulée : {outcome.Error}", outcome.Success ? ToolTipIcon.Info : ToolTipIcon.Warning);
        if (pendingUpdate is not null && !updateBusy && !updateBlocked && (settings.AutoUpdate || updateManual) && !game.IsRunning())
        {
            await InstallPendingAsync();
            return;
        }
        if (UpdateInstaller.CanInstall && settings.AutoUpdate && !updateBusy && pendingUpdate is null
            && DateTimeOffset.UtcNow >= nextUpdateCheck && !game.IsRunning())
            _ = CheckUpdatesAsync(manual: false);
    }
    private async Task CheckUpdatesAsync(bool manual)
    {
        if (updateBusy || exiting) return;
        if (!UpdateInstaller.CanInstall)
        {
            if (manual) MessageBox.Show("Les mises à jour sont disponibles dans la version portable téléchargée sur GitHub.", "AMZ.Resolution");
            return;
        }
        updateManual = manual;
        if (pendingUpdate is not null && !updateBlocked) return;
        pendingUpdate = null; updateBlocked = false;
        updateBusy = true; updateItem.Enabled = false;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(updateLifetime.Token);
        cancellation.CancelAfter(TimeSpan.FromMinutes(6));
        updateCancellation = cancellation;
        try
        {
            updateItem.Text = "Recherche sur GitHub…";
            await Task.Run(() => GitHubUpdateClient.Cleanup(), cancellation.Token);
            var release = await updater.CheckAsync(cancellation.Token);
            nextUpdateCheck = DateTimeOffset.UtcNow.AddHours(6);
            if (release is null)
            {
                if (manual) tray.ShowBalloonTip(4000, "AMZ.Resolution", $"Aucune mise à jour compatible disponible (v{GitHubUpdateClient.CurrentVersion.ToString(3)}).", ToolTipIcon.Info);
                return;
            }
            var progress = new Progress<int>(p =>
            {
                if (!exiting) updateItem.Text = $"Téléchargement v{release.Version.ToString(3)} · {p} %";
            });
            pendingUpdate = await updater.DownloadAsync(release, progress, cancellation.Token);
            if (!exiting && game.IsRunning())
                tray.ShowBalloonTip(5000, "Mise à jour téléchargée", "Elle sera installée après la fermeture des jeux sélectionnés.", ToolTipIcon.Info);
        }
        catch (OperationCanceledException) when (updateLifetime.IsCancellationRequested || !settings.AutoUpdate && !manual) { }
        catch (Exception ex)
        {
            AppStorage.Log(ex); nextUpdateCheck = DateTimeOffset.UtcNow.AddHours(1);
            if (manual && !exiting) MessageBox.Show("La mise à jour n’a pas pu être téléchargée ou vérifiée. Réessayez plus tard.\n\n" + ex.Message, "AMZ.Resolution", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        finally
        {
            updateCancellation = null; updateBusy = false;
            if (!exiting)
            {
                updateItem.Enabled = true;
                updateItem.Text = pendingUpdate is null ? "Mettre à jour maintenant…" : $"Installer v{pendingUpdate.Version.ToString(3)} dès que possible";
            }
        }
    }
    private async Task InstallPendingAsync()
    {
        if (working || exiting || pendingUpdate is not { } prepared || game.IsRunning()
            || controller.Session is not null && !gameMode.Enabled) return;
        SetWorking(true); updateItem.Enabled = false;
        var wasEnabled = gameMode.Enabled;
        try
        {
            await gameMode.DisableAsync();
            await SafetyMonitor.WaitForWatchdogsAsync();
            // A game may have launched while desktop restoration was in progress.
            if (game.IsRunning()) { if (wasEnabled) gameMode.Enable(); return; }
            await UpdateInstaller.BeginAsync(prepared, wasEnabled, Environment.ProcessPath!);
            if (game.IsRunning())
            {
                File.WriteAllText(Path.Combine(prepared.Directory, "cancelled"), "cancelled");
                pendingUpdate = null; nextUpdateCheck = DateTimeOffset.UtcNow;
                if (wasEnabled) gameMode.Enable();
                return;
            }
            exiting = true; poll.Stop(); tray.Visible = false;
            updateLifetime.Cancel(); ExitThread();
        }
        catch (Exception ex)
        {
            AppStorage.Log(ex); updateBlocked = true;
            if (wasEnabled) gameMode.Enable();
            tray.ShowBalloonTip(6000, "Mise à jour différée", "Installation impossible. Vérifiez que le dossier de l’application est accessible en écriture, puis réessayez depuis le menu.", ToolTipIcon.Warning);
        }
        finally { if (!exiting) { SetWorking(false); updateItem.Enabled = true; } }
    }
    private void UpdateSettingsChanged()
    {
        if (!settings.AutoUpdate && !updateManual) updateCancellation?.Cancel();
        if (settings.AutoUpdate) nextUpdateCheck = DateTimeOffset.MinValue;
    }
    private void DisposeUpdates()
    {
        updateLifetime.Cancel();
        updater.Dispose();
        updateLifetime.Dispose();
    }
}
