namespace ResolutionTray.UI;

internal sealed class ConfirmationForm : Form
{
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 1000 };
    private readonly Label countdown = new() { AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(0, 16, 0, 16) };
    private int seconds = 15;
    internal ConfirmationForm(string? deviceName, Func<bool> isGameRunning)
    {
        Text = "Conserver cet affichage ?";
        Font = new Font("Segoe UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(500, 205);
        Padding = new Padding(24);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        var area = Screen.AllScreens.FirstOrDefault(s => s.DeviceName == deviceName)?.WorkingArea ?? Screen.PrimaryScreen!.WorkingArea;
        Location = new Point(area.Left + (area.Width - Width) / 2, area.Top + (area.Height - Height) / 2);
        var text = new Label { Text = "L'image doit être centrée avec des bandes noires autour.\nVérifiez sa netteté et la taille de la zone affichée.", AutoSize = true, Dock = DockStyle.Top };
        var keep = new Button { Text = "Conserver", AutoSize = true, DialogResult = DialogResult.OK };
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
