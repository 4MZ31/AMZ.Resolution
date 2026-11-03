using ResolutionTray.Core;

namespace ResolutionTray.UI;

internal sealed class ConfirmationForm : Form
{
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 1000 };
    private readonly Label countdown = new() { AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(0, 16, 0, 16) };
    private int seconds = 15;
    internal ConfirmationForm(string? deviceName, Func<bool> isGameRunning, DisplayMode mode, bool testOnly = false)
    {
        Text = testOnly ? "Test de compatibilité — retour automatique" : "Conserver cet affichage ?";
        Font = new Font("Segoe UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(560, 235);
        Padding = new Padding(24);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        var area = Screen.AllScreens.FirstOrDefault(s => s.DeviceName == deviceName)?.WorkingArea ?? Screen.PrimaryScreen!.WorkingArea;
        Location = new Point(area.Left + (area.Width - Width) / 2, area.Top + (area.Height - Height) / 2);
        var text = new Label { Text = $"{mode.Width} × {mode.Height} · {mode.RefreshRate} Hz\nVérifiez la netteté, le centrage et la taille de l’image.\n" +
            (testOnly ? "Le bureau sera restauré même si vous validez ce test." : "Les marges dépendent de la résolution choisie."), AutoSize = true, Dock = DockStyle.Top };
        var keep = new Button { Text = testOnly ? "L’image est correcte" : "Conserver", AutoSize = true, DialogResult = DialogResult.OK };
        var revert = new Button { Text = "Restaurer", AutoSize = true, DialogResult = DialogResult.Cancel };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 38, FlowDirection = FlowDirection.RightToLeft };
        buttons.Controls.Add(keep); buttons.Controls.Add(revert);
        Controls.Add(buttons); Controls.Add(countdown); Controls.Add(text);
        AcceptButton = keep; CancelButton = revert;
        UpdateCountdown();
        timer.Tick += (_, _) =>
        {
            if (!isGameRunning()) { DialogResult = DialogResult.Cancel; Close(); return; }
            seconds--; UpdateCountdown();
            if (seconds <= 0) { DialogResult = DialogResult.Cancel; Close(); }
        };
        Shown += (_, _) =>
        {
            if (isGameRunning()) timer.Start();
            else { DialogResult = DialogResult.Cancel; Close(); }
        };
        FormClosed += (_, _) => timer.Stop();
    }
    private void UpdateCountdown() => countdown.Text = $"Retour automatique à l'affichage précédent dans {seconds} secondes.";
    protected override void Dispose(bool disposing) { if (disposing) timer.Dispose(); base.Dispose(disposing); }
}
