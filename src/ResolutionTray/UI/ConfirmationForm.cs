using ResolutionTray.Core;

namespace ResolutionTray.UI;

internal sealed class ConfirmationForm : Form
{
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 1000 };
    private readonly Label countdown = UiTheme.Label("", 10, true);
    private readonly ProgressBar remaining = new() { Dock = DockStyle.Fill, Maximum = 15, Value = 15, Style = ProgressBarStyle.Continuous, Margin = new Padding(0, 8, 0, 8) };
    private int seconds = 15;
    internal ConfirmationForm(string? deviceName, Func<bool> isGameRunning, DisplayMode mode, bool testOnly = false)
    {
        UiTheme.Apply(this);
        Text = testOnly ? "ResolutionTray — Test d’affichage" : "ResolutionTray — Confirmer le profil";
        ClientSize = new Size(608, 352); Padding = new Padding(28);
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
        TopMost = true; StartPosition = FormStartPosition.Manual;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7, Margin = Padding.Empty };
        foreach (var height in new[] { 40, 40, 68, 28, 24, 16, 56 }) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        var title = UiTheme.Label("L’image est-elle correcte ?", 20); title.ForeColor = UiTheme.Ink; layout.Controls.Add(title);
        var resolution = UiTheme.Label($"{mode.Width} × {mode.Height} · {mode.RefreshRate} Hz", 17, true); resolution.Font = UiTheme.Figures(17); layout.Controls.Add(resolution);
        layout.Controls.Add(UiTheme.Label("Vérifiez la netteté, le centrage et la taille de la zone de jeu.\n" +
            (testOnly ? "Après validation du test, le bureau sera restauré." : "Ce profil sera utilisé jusqu’à la fermeture des jeux sélectionnés.")));
        layout.Controls.Add(countdown); layout.Controls.Add(remaining); layout.Controls.Add(new Panel());
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Margin = Padding.Empty };
        var keep = UiTheme.Button(testOnly ? "Valider le test" : "Conserver le profil", 180, true); keep.DialogResult = DialogResult.OK; keep.Name = "confirmProfile";
        var revert = UiTheme.Button("Restaurer maintenant", 192); revert.DialogResult = DialogResult.Cancel; revert.Name = "restoreProfile";
        buttons.Controls.Add(keep); buttons.Controls.Add(revert); layout.Controls.Add(buttons); Controls.Add(layout);
        AcceptButton = keep; CancelButton = revert; countdown.Name = "confirmationCountdown";
        UpdateCountdown();
        timer.Tick += (_, _) =>
        {
            if (!isGameRunning()) { DialogResult = DialogResult.Cancel; Close(); return; }
            seconds--; UpdateCountdown();
            if (seconds <= 0) { DialogResult = DialogResult.Cancel; Close(); }
        };
        Shown += (_, _) =>
        {
            var area = Screen.AllScreens.FirstOrDefault(s => s.DeviceName == deviceName)?.WorkingArea ?? Screen.PrimaryScreen!.WorkingArea;
            Location = new Point(area.Left + (area.Width - Width) / 2, area.Top + (area.Height - Height) / 2);
            if (isGameRunning()) timer.Start(); else { DialogResult = DialogResult.Cancel; Close(); }
        };
        FormClosed += (_, _) => timer.Stop();
    }
    private void UpdateCountdown()
    {
        countdown.Text = $"Retour automatique au bureau dans {seconds} s";
        remaining.Value = Math.Max(0, seconds);
    }
    protected override void Dispose(bool disposing) { if (disposing) timer.Dispose(); base.Dispose(disposing); }
}
