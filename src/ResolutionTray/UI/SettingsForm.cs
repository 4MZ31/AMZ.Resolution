using Microsoft.Win32;
using ResolutionTray.Core;
using ResolutionTray.Services;

namespace ResolutionTray.UI;

internal sealed class SettingsForm : Form
{
    private readonly ComboBox displays = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Top };
    private readonly CheckBox startup = new() { Text = "Lancer avec Windows (sans activer le profil)", AutoSize = true };
    private readonly Label details = new() { AutoSize = false, Height = 78, Dock = DockStyle.Fill };
    private readonly Button save = new() { Text = "Enregistrer", AutoSize = true, Enabled = false };
    private readonly Button activate = new() { Text = "Enregistrer et surveiller", AutoSize = true, Enabled = false };
    private readonly IReadOnlyList<DisplayTarget> targets;
    public AppSettings Result { get; private set; }
    public bool ActivateRequested { get; private set; }

    internal SettingsForm(AppSettings settings, IReadOnlyList<DisplayTarget> allDisplays)
    {
        Result = settings;
        targets = allDisplays.Where(d => d.NativeWidth == 2560 && d.NativeHeight == 1440).ToArray();
        Text = "AMD Resolution Tray — Paramètres";
        Font = new Font("Segoe UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(630, 370);
        MinimumSize = Size;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(248, 249, 251);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 7 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.Controls.Add(new Label { Text = "Une zone de jeu centrée sur votre écran", AutoSize = true, Font = new Font(Font, FontStyle.Bold) });
        layout.Controls.Add(new Label { Text = "Écran natif 2560 × 1440 à utiliser", AutoSize = true });
        layout.Controls.Add(displays);
        layout.Controls.Add(details);
        startup.Checked = StartupRegistration.IsEnabled;
        layout.Controls.Add(startup);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        buttons.Controls.Add(activate);
        buttons.Controls.Add(save);
        var close = new Button { Text = "Annuler", AutoSize = true, DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(close);
        layout.Controls.Add(buttons);
        layout.Controls.Add(new Label { Text = "Vert : résolution active • Gris : en attente ou désactivé", AutoSize = true, ForeColor = Color.DimGray });
        Controls.Add(layout);
        CancelButton = close;
        foreach (var d in targets) displays.Items.Add($"{d.Name} — {d.DeviceName} — {d.CurrentMode.RefreshRate} Hz");
        displays.SelectedIndexChanged += (_, _) => UpdateSelection();
        var selected = targets.ToList().FindIndex(d => d.Identity == settings.DisplayIdentity);
        if (selected >= 0) displays.SelectedIndex = selected;
        else if (targets.Count == 1) displays.SelectedIndex = 0;
        UpdateSelection();
        save.Click += (_, _) => Save(false);
        activate.Click += (_, _) => Save(true);
    }

    private void UpdateSelection()
    {
        save.Enabled = activate.Enabled = displays.SelectedIndex >= 0;
        details.Text = displays.SelectedIndex >= 0
            ? $"Automatique avec CS2 / CS:GO : 2324 × 1306 centré.\nFréquence conservée : {targets[displays.SelectedIndex].CurrentMode.RefreshRate} Hz.\nSur une dalle 27 pouces, la zone représente environ 24,5 pouces."
            : targets.Count == 0 ? "Aucun écran AMD natif 2560 × 1440 identifié.\nVérifiez le pilote Adrenalin et utilisez le mode Étendre de Windows."
            : "Choisissez l'écran sur lequel appliquer le profil.";
    }
    private void Save(bool shouldActivate)
    {
        try
        {
            var result = Result with { DisplayIdentity = targets[displays.SelectedIndex].Identity, StartWithWindows = startup.Checked };
            StartupRegistration.Set(startup.Checked);
            SettingsStore.Save(result);
            Result = result;
            ActivateRequested = shouldActivate;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex) { Program.ShowError(ex); }
    }
}

internal static class StartupRegistration
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "AmdResolutionTray";
    internal static bool IsEnabled
    {
        get { using var key = Registry.CurrentUser.OpenSubKey(KeyPath); return key?.GetValue(ValueName) is string; }
    }
    internal static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
        if (!enabled) { key.DeleteValue(ValueName, false); return; }
        var command = SafetyMonitor.Command();
        var quoted = '"' + command.FileName + '"';
        foreach (var arg in command.ArgumentList) quoted += " \"" + arg + "\"";
        key.SetValue(ValueName, quoted);
    }
}
