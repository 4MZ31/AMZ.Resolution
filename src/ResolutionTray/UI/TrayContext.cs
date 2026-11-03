using System.Diagnostics;
using ResolutionTray.Core;
using ResolutionTray.Native;
using ResolutionTray.Services;

namespace ResolutionTray.UI;

internal sealed class TrayContext : ApplicationContext
{
    private readonly WindowsDisplayBackend backend = new();
    private readonly ProfileController controller;
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
        openSettings = showSettings || settings.DisplayIdentity is null;
        controller = new ProfileController(backend, new RecoveryStore(), new SafetyMonitor());
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
                initialized = true; poll.Interval = 3000;
                if (controller.Session is not null)
                {
                    var result = MessageBox.Show("Une session précédente n'a pas été restaurée. Restaurer les réglages sauvegardés ?",
                        "Resolution Tray — Récupération", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (result == DialogResult.Yes) await RestoreAsync();
                }
                if (openSettings && controller.Session is null) { openSettings = false; await OpenSettingsAsync(); }
            }
            if (!working) await RefreshAsync();
        }
        catch (Exception ex) { poll.Stop(); SetInactive("Erreur — voir le journal"); Program.ShowError(ex); }
    }

    private void SetWorking(bool value)
    {
        working = value;
        toggle.Enabled = restore.Enabled = settingsItem.Enabled = quit.Enabled = !value;
        if (value) { tray.Icon = gray; status.Text = "Changement en cours…"; }
    }
    private async Task RefreshAsync()
    {
        if (working || exiting) return;
        working = true;
        try
        {
            var active = await controller.IsActiveAsync();
            var session = controller.Session;
            tray.Icon = active ? green : gray;
            status.Text = active ? $"Actif — 2324 × 1306 · {session!.RequestedMode.RefreshRate} Hz"
                : session is not null ? "Profil interrompu — restauration disponible" : "Désactivé";
            tray.Text = ("AMD Resolution Tray — " + status.Text)[..Math.Min(63, ("AMD Resolution Tray — " + status.Text).Length)];
            toggle.Text = session is not null ? "Désactiver et restaurer" : "Activer";
            restore.Visible = session is not null;
        }
        finally { SetWorking(false); settingsItem.Enabled = controller.Session is null; }
    }
    private void SetInactive(string text) { tray.Icon = gray; status.Text = text; tray.Text = "AMD Resolution Tray — Désactivé"; }

    private async Task ToggleAsync()
    {
        if (working) return;
        if (controller.Session is not null) { await RestoreAsync(); return; }
        if (settings.DisplayIdentity is null) { await OpenSettingsAsync(); return; }
        SetWorking(true);
        try
        {
            var display = (await Task.Run(backend.GetDisplays)).SingleOrDefault(d => d.Identity == settings.DisplayIdentity)
                ?? throw new InvalidOperationException("L'écran sélectionné est déconnecté.");
            var fingerprint = $"{display.Identity}|{display.CurrentMode.RefreshRate}|{display.DriverVersion}";
            await controller.ActivateAsync(new Profile(settings.DisplayIdentity));
            if (settings.VerifiedProfile == fingerprint) await controller.ConfirmAsync();
            else
            {
                using var confirmation = new ConfirmationForm(display.DeviceName);
                if (confirmation.ShowDialog() == DialogResult.OK)
                {
                    await controller.ConfirmAsync();
                    settings = settings with { VerifiedProfile = fingerprint };
                    SettingsStore.Save(settings);
                }
                else await controller.RestoreAsync();
            }
        }
        catch (Exception ex)
        {
            // This also covers failure opening the confirmation dialog or confirming a changed mode.
            try { if (controller.Session is not null) await controller.RestoreAsync(); }
            catch (Exception recovery) { AppStorage.Log(recovery); }
            Program.ShowError(ex);
        }
        finally { SetWorking(false); await RefreshAsync(); }
    }

    private async Task RestoreAsync()
    {
        if (working) return;
        SetWorking(true);
        try { await controller.RestoreAsync(); }
        catch (Exception ex) { Program.ShowError(ex); }
        finally { SetWorking(false); await RefreshAsync(); }
    }

    private async Task OpenSettingsAsync()
    {
        if (working || controller.Session is not null) return;
        SetWorking(true);
        bool activateAfter = false;
        try
        {
            var displays = await Task.Run(backend.GetDisplays);
            using var dialog = new SettingsForm(settings, displays);
            if (dialog.ShowDialog() == DialogResult.OK) { settings = dialog.Result; activateAfter = dialog.ActivateRequested; }
        }
        catch (Exception ex) { Program.ShowError(ex); }
        finally { SetWorking(false); }
        if (activateAfter) await ToggleAsync();
        else await RefreshAsync();
    }

    private async Task QuitAsync()
    {
        if (working) return;
        SetWorking(true);
        try
        {
            await controller.RestoreAsync();
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
