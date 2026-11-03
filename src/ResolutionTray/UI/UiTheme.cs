namespace ResolutionTray.UI;

internal static class UiTheme
{
    internal static readonly Color Background = Color.FromArgb(245, 247, 249);
    internal static readonly Color Ink = Color.FromArgb(27, 38, 51);
    internal static readonly Color Muted = Color.FromArgb(105, 117, 130);
    internal static readonly Color Green = Color.FromArgb(22, 139, 86);
    internal static readonly Color Line = Color.FromArgb(222, 228, 234);
    internal static Button Button(string text, int width, bool primary = false) => new()
    {
        Text = text, Width = width, Height = 38, FlatStyle = FlatStyle.Flat,
        BackColor = primary ? Green : Color.White, ForeColor = primary ? Color.White : Ink,
        UseVisualStyleBackColor = false, Cursor = Cursors.Hand, Margin = new Padding(0, 0, 8, 0),
        FlatAppearance = { BorderSize = primary ? 0 : 1, BorderColor = Line }
    };
    internal static Label Label(string text, int size = 10, bool bold = false) => new()
    {
        Text = text, Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = bold ? Ink : Muted, Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
        Margin = Padding.Empty
    };
}

internal sealed class GameListView : ListView
{
    internal GameListView() { DoubleBuffered = true; }
}

internal sealed class DisplayPreview : Control
{
    private int resolutionWidth = 2324;
    private int resolutionHeight = 1306;
    internal void SetResolution(int width, int height)
    {
        resolutionWidth = width; resolutionHeight = height;
        AccessibleName = $"Zone {width} × {height} centrée sur une dalle 2560 × 1440";
        Invalidate();
    }
    internal DisplayPreview()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        Dock = DockStyle.Fill;
        BackColor = Color.White;
        AccessibleName = "Zone 2324 × 1306 centrée sur une dalle 2560 × 1440";
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var width = Math.Min(ClientSize.Width - 16, (ClientSize.Height - 24) * 16 / 9);
        if (width < 40) return;
        var screen = new Rectangle((ClientSize.Width - width) / 2, 8, width, width * 9 / 16);
        using var dark = new SolidBrush(Color.FromArgb(35, 45, 57));
        using var fill = new SolidBrush(Color.FromArgb(225, 246, 234));
        using var ink = new SolidBrush(UiTheme.Green);
        using var pen = new Pen(UiTheme.Line, 2);
        g.FillRectangle(dark, screen);
        var activeWidth = screen.Width * resolutionWidth / 2560;
        var activeHeight = screen.Height * resolutionHeight / 1440;
        var active = new Rectangle(screen.Left + (screen.Width - activeWidth) / 2, screen.Top + (screen.Height - activeHeight) / 2,
            activeWidth, activeHeight);
        g.FillRectangle(fill, active);
        g.DrawRectangle(pen, screen);
        g.DrawLine(pen, screen.Left + screen.Width / 2, screen.Bottom + 2, screen.Left + screen.Width / 2, screen.Bottom + 10);
        g.DrawLine(pen, screen.Left + screen.Width / 2 - 25, screen.Bottom + 11, screen.Left + screen.Width / 2 + 25, screen.Bottom + 11);
        using var font = new Font("Segoe UI", 10, FontStyle.Bold);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString($"{resolutionWidth} × {resolutionHeight}", font, ink, active, format);
    }
}
