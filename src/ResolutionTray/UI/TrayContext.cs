using ResolutionTray.Core;
using ResolutionTray.Native;
using ResolutionTray.Services;

namespace ResolutionTray.UI;

internal sealed class TrayContext : ApplicationContext
{
    private readonly WindowsDisplayBackend backend = new();
    private readonly ProfileController controller;
    private readonly SelectedGamePresence game = new();
    private readonly GameModeController gameMode;
    private readonly NotifyIcon tray;
    private readonly ContextMenuStrip menu = new();
    private readonly ToolStripMenuItem status = new("Désactivé") { Enabled = false };
    private readonly ToolStripMenuItem toggle = new("Activer");
    private readonly ToolStripMenuItem restore = new("Restaurer l'affichage précédent");
    private readonly ToolStripMenuItem settingsItem = new("Paramètres…");
    private readonly ToolStripMenuItem quit = new("Quitter");
    private readonly Icon gray = CreateIcon(Color.FromArgb(130, 138, 148));
    private readonly Icon green = CreateIcon(Color.FromArgb(24, 185, 100));
    private readonly System.Windows.Forms.Timer poll = new() { Interval = 3000 };
    private AppSettings settings;
    private readonly EventWaitHandle settingsSignal = new(false, EventResetMode.AutoReset, Program.MutexName + "-Settings");
    private readonly EventWaitHandle quitSignal = new(false, EventResetMode.AutoReset, Program.MutexName + "-Quit");
    private bool working;
    private bool initialized;
    private bool openSettings;
    private bool exiting;

    internal TrayContext(bool showSettings)
    {
        settings = SettingsStore.Load();
        game.Update(settings.GameList);
        openSettings = showSettings || settings.DisplayIdentity is null;
        controller = new ProfileController(backend, new RecoveryStore(), new SafetyMonitor());
        gameMode = new GameModeController(controller, game);
        menu.Items.AddRange([status, new ToolStripSeparator(), toggle, restore, settingsItem, new ToolStripSeparator(), quit]);
        tray = new NotifyIcon { Icon = gray, Text = "AMD Resolution Tray — Désactivé", ContextMenuStrip = menu, Visible = true };
        tray.MouseDoubleClick += async (_, e) => { if (e.Button == MouseButtons.Left) await ToggleAsync(); };
        toggle.Click += async (_, _) => await ToggleAsync();
        restore.Click += async (_, _) => await RestoreAsync();
        settingsItem.Click += async (_, _) => await OpenSettingsAsync();
        quit.Click += async (_, _) => await QuitAsync();
        poll.Tick += async (_, _) => await TickAsync();
        poll.Interval = 100;
        poll.Start();
    }

    private async Task TickAsync()
    {
        if (working || exiting) return;
        try
        {
            if (quitSignal.WaitOne(0)) { await QuitAsync(); return; }
            if (settingsSignal.WaitOne(0)) { await OpenSettingsAsync(); return; }
            if (!initialized)
            {
                initialized = true; poll.Interval = 1000;
                if (controller.Session is not null)
                {
                    var result = MessageBox.Show("Une session précédente n'a pas été restaurée. Restaurer les réglages sauvegardés ?",
                        "Resolution Tray — Récupération", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (result == DialogResult.Yes) await RestoreAsync();
                }
                if (openSettings && controller.Session is null) { openSettings = false; await OpenSettingsAsync(); }
            }
            if (!working && gameMode.Enabled) await SynchronizeGameAsync();
            if (!working) await RefreshAsync();
        }
        catch (Exception ex) { poll.Stop(); SetInactive("Erreur — voir le journal"); Program.ShowError(ex); }
    }

    private void SetWorking(bool value, bool showTransition = true)
    {
        working = value;
        toggle.Enabled = restore.Enabled = settingsItem.Enabled = quit.Enabled = !value;
        if (value && showTransition) { tray.Icon = gray; status.Text = "Changement en cours…"; }
    }
    private async Task RefreshAsync()
    {
        if (working || exiting) return;
        working = true;
        try
        {
            var active = gameMode.Enabled && game.IsRunning() && await controller.IsActiveAsync();
            active = active && game.IsRunning();
            var session = controller.Session;
            tray.Icon = active ? green : gray;
            status.Text = active ? $"Actif — 2324 × 1306 · {session!.RequestedMode.RefreshRate} Hz"
                : session is not null ? "Profil interrompu — restauration disponible"
                : !gameMode.Enabled ? "Désactivé"
                : game.IsRunning() ? "Profil non appliqué — réactivez la surveillance"
                : "En attente d’un jeu sélectionné";
            tray.Text = ("AMD Resolution Tray — " + status.Text)[..Math.Min(63, ("AMD Resolution Tray — " + status.Text).Length)];
            toggle.Text = gameMode.Enabled ? "Désactiver la surveillance"
                : session is not null ? "Restaurer l’affichage précédent" : "Activer la surveillance";
            restore.Visible = session is not null;
        }
        finally { SetWorking(false); settingsItem.Enabled = controller.Session is null; }
    }
    private void SetInactive(string text) { tray.Icon = gray; status.Text = text; tray.Text = "AMD Resolution Tray — Désactivé"; }

    private async Task ToggleAsync()
    {
        if (working) return;
        if (gameMode.Enabled || controller.Session is not null) { await RestoreAsync(); return; }
        if (settings.DisplayIdentity is null || !game.HasSelection) { await OpenSettingsAsync(); return; }
        gameMode.Enable();
        await SynchronizeGameAsync();
        await RefreshAsync();
    }

    private async Task SynchronizeGameAsync()
    {
        if (working || !gameMode.Enabled || settings.DisplayIdentity is null) return;
        SetWorking(true, showTransition: false);
        try
        {
            string? verified = null;
            await gameMode.SynchronizeAsync(new Profile(settings.DisplayIdentity), async session =>
            {
                if (!game.IsRunning()) return false;
                var display = (await Task.Run(backend.GetDisplays)).SingleOrDefault(d => d.Identity == session.DisplayIdentity)
                    ?? throw new InvalidOperationException("L'écran sélectionné est déconnecté.");
                var fingerprint = $"{display.Identity}|{session.RequestedMode.RefreshRate}|{display.DriverVersion}";
                if (settings.VerifiedProfile == fingerprint) return true;
                using var confirmation = new ConfirmationForm(display.DeviceName, game.IsRunning);
                if (confirmation.ShowDialog() != DialogResult.OK || !game.IsRunning()) return false;
                verified = fingerprint;
                return true;
            });
            if (verified is not null && controller.Session is { Confirmed: true })
            {
                settings = settings with { VerifiedProfile = verified };
                SettingsStore.Save(settings);
            }
        }
        catch (Exception ex)
        {
            // Fail once, restore if possible, and require an explicit re-enable after an error.
            try { await gameMode.DisableAsync(); }
            catch (Exception recovery) { AppStorage.Log(recovery); }
            Program.ShowError(ex);
        }
        finally { SetWorking(false); }
    }

    private async Task RestoreAsync()
    {
        if (working) return;
        SetWorking(true);
        try { await gameMode.DisableAsync(); }
        catch (Exception ex) { Program.ShowError(ex); }
        finally { SetWorking(false); await RefreshAsync(); }
    }

    private async Task OpenSettingsAsync()
    {
        if (working || controller.Session is not null) return;
        SetWorking(true);
        bool activateAfter = false;
        var wasEnabled = gameMode.Enabled;
        try
        {
            var displays = await Task.Run(backend.GetDisplays);
            using var dialog = new SettingsForm(settings, displays);
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                settings = dialog.Result;
                game.Update(settings.GameList);
                activateAfter = dialog.ActivateRequested || wasEnabled;
            }
        }
        catch (Exception ex) { Program.ShowError(ex); }
        finally { SetWorking(false); }
        if (!game.HasSelection) await gameMode.DisableAsync();
        else if (activateAfter)
        {
            gameMode.Enable();
            await SynchronizeGameAsync();
        }
        await RefreshAsync();
    }

    private async Task QuitAsync()
    {
        if (working) return;
        SetWorking(true);
        try
        {
            await gameMode.DisableAsync();
            exiting = true; poll.Stop(); tray.Visible = false; ExitThread();
        }
        catch (Exception ex) { Program.ShowError(ex); SetWorking(false); await RefreshAsync(); }
    }

    private static Icon CreateIcon(Color color)
    {
        using var bitmap = new Bitmap(32, 32);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(color);
            using var pen = new Pen(Color.White, 2);
            graphics.FillEllipse(brush, 1, 1, 30, 30);
            graphics.DrawRectangle(pen, 8, 9, 16, 11);
            graphics.DrawLine(pen, 16, 20, 16, 24);
            graphics.DrawLine(pen, 11, 24, 21, 24);
        }
        var handle = bitmap.GetHicon();
        try { return (Icon)Icon.FromHandle(handle).Clone(); }
        finally { Win32.DestroyIcon(handle); }
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            poll.Dispose(); tray.Dispose(); menu.Dispose(); gray.Dispose(); green.Dispose();
            settingsSignal.Dispose(); quitSignal.Dispose();
        }
        base.Dispose(disposing);
    }
}
