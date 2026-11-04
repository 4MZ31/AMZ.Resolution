using System.Drawing.Drawing2D;

namespace AMZ.Resolution.UI;

// Design method adapted from Unslop by Damir Sokolovsky (@heydamir), CC BY 4.0.
// Lucide-derived line glyphs: see docs/THIRD-PARTY-NOTICES.txt.
internal static class UiTheme
{
    private static bool Contrast => SystemInformation.HighContrast;
    internal static bool DarkMode { get; private set; } = true;
    internal static event Action? ThemeChanged;
    internal static void SetDarkMode(bool value)
    {
        if (DarkMode == value) return;
        DarkMode = value; ThemeChanged?.Invoke();
    }
    internal static Color Background => Contrast ? SystemColors.Control : DarkMode ? Color.FromArgb(25, 27, 30) : Color.FromArgb(247, 248, 249);
    internal static Color Sidebar => Contrast ? SystemColors.Control : DarkMode ? Color.FromArgb(19, 21, 24) : Color.FromArgb(239, 242, 244);
    internal static Color Surface => Contrast ? SystemColors.Window : DarkMode ? Color.FromArgb(32, 35, 39) : Color.White;
    internal static Color Raised => Contrast ? SystemColors.Control : DarkMode ? Color.FromArgb(44, 48, 53) : Color.FromArgb(240, 243, 245);
    internal static Color Ink => Contrast ? SystemColors.WindowText : DarkMode ? Color.FromArgb(237, 240, 243) : Color.FromArgb(29, 37, 43);
    internal static Color Secondary => Contrast ? SystemColors.WindowText : DarkMode ? Color.FromArgb(178, 186, 194) : Color.FromArgb(80, 93, 104);
    internal static Color Muted => Contrast ? SystemColors.GrayText : DarkMode ? Color.FromArgb(144, 154, 164) : Color.FromArgb(103, 114, 125);
    internal static Color Line => Contrast ? SystemColors.WindowText : DarkMode ? Color.FromArgb(55, 61, 67) : Color.FromArgb(219, 225, 230);
    internal static Color Green => Contrast ? SystemColors.Highlight : DarkMode ? Color.FromArgb(115, 201, 159) : Color.FromArgb(22, 103, 75);
    internal static Color GreenSoft => Contrast ? SystemColors.Control : DarkMode ? Color.FromArgb(30, 59, 46) : Color.FromArgb(231, 243, 235);
    internal static Color Warning => Contrast ? SystemColors.WindowText : DarkMode ? Color.FromArgb(221, 179, 114) : Color.FromArgb(140, 90, 19);
    internal static Color Error => Contrast ? SystemColors.WindowText : DarkMode ? Color.FromArgb(239, 152, 152) : Color.FromArgb(175, 55, 55);
    internal static Color AccentInk => Contrast ? SystemColors.HighlightText : DarkMode ? Color.FromArgb(16, 38, 26) : Color.White;
    internal static Color PrimaryFill => Contrast ? SystemColors.Highlight : DarkMode ? Color.FromArgb(231, 237, 232) : Green;
    internal static Color PrimaryInk => Contrast ? SystemColors.HighlightText : DarkMode ? Color.FromArgb(25, 35, 29) : Color.White;
    internal static void Recolor(Control control)
    {
        control.BackColor = control is SurfacePanel or FieldFrame or TextBoxBase or ComboBox or UpDownBase or ListView or ListBox ? Surface
            : control.Name == "appSidebar" ? Sidebar : control is Form ? Background : control.Parent?.BackColor ?? Background;
        control.ForeColor = control is Label label ? label.Tag as string == "ink" ? Ink : Secondary : Ink;
        if (control is LinkLabel link) { link.LinkColor = Green; link.ActiveLinkColor = Green; link.VisitedLinkColor = Green; }
        foreach (Control child in control.Controls) Recolor(child);
        if (control is Form form && form.IsHandleCreated) PaintChrome(form);
        control.Invalidate();
    }
    internal static Font Font(float size = 10.5f, bool medium = false) => new(medium ? "Segoe UI Semibold" : "Segoe UI", size, FontStyle.Regular);
    internal static Font Figures(float size = 10) => new("Consolas", size);
    internal static Button Button(string text, int width, bool primary = false) => new ActionButton
    {
        Text = text, Width = width, Height = 40, Primary = primary, Margin = new Padding(0, 0, 8, 0)
    };
    internal static Label Label(string text, int size = 10, bool bold = false) => new()
    {
        Text = text, Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = bold ? Ink : Secondary, Font = Font(size, bold), BackColor = Color.Transparent, Margin = Padding.Empty, Tag = bold ? "ink" : "secondary"
    };
    internal static TableLayoutPanel Stack(params int[] heights)
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = heights.Length, Margin = Padding.Empty };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ControlAdded += (_, e) => { if (e.Control is { } child) panel.SetCellPosition(child, new TableLayoutPanelCellPosition(0, panel.Controls.IndexOf(child))); };
        foreach (var height in heights) panel.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        return panel;
    }
    internal static Control Field(Control control, string? name = null)
    {
        FieldFrame frame = control is TextBox input && name == "searchGames" ? new SearchField(input) : new FieldFrame { Padding = new Padding(12, 8, 12, 4) };
        frame.Dock = DockStyle.Fill; frame.Margin = new Padding(0, 0, 0, 4);
        control.Dock = DockStyle.Fill; control.Margin = Padding.Empty; control.BackColor = Surface; control.ForeColor = Ink; control.Font = Font();
        if (name is not null)
        {
            control.Name = name;
            control.AccessibleName ??= name switch { "searchGames" => "Rechercher un jeu", "gameSource" => "Source des jeux", "targetDisplay" => "Écran cible", _ => name };
        }
        if (control is TextBox text) text.BorderStyle = BorderStyle.None;
        if (control is NumericUpDown number) number.BorderStyle = BorderStyle.None;
        if (control is ComboBox combo)
        {
            frame.Padding = new Padding(8, 4, 8, 4);
            combo.FlatStyle = FlatStyle.Flat;
            combo.DrawMode = DrawMode.OwnerDrawFixed;
            combo.ItemHeight = 24;
            combo.DrawItem += (_, e) =>
            {
                if (e.Index < 0) return;
                var selected = (e.State & DrawItemState.Selected) != 0;
                using var fill = new SolidBrush(selected ? Raised : Surface); e.Graphics.FillRectangle(fill, e.Bounds);
                TextRenderer.DrawText(e.Graphics, combo.Items[e.Index]?.ToString(), combo.Font, e.Bounds, Ink,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                if ((e.State & DrawItemState.Focus) != 0) e.DrawFocusRectangle();
            };
        }
        frame.Controls.Add(control);
        control.Enter += (_, _) => frame.Invalidate(); control.Leave += (_, _) => frame.Invalidate();
        return frame;
    }
    internal static GraphicsPath Round(RectangleF r, float radius)
    {
        // A native child can request its parent background during a zero-size layout transition.
        r.Width = Math.Max(1, r.Width); r.Height = Math.Max(1, r.Height);
        var path = new GraphicsPath(); var d = Math.Max(1, Math.Min(radius * 2, Math.Min(r.Width, r.Height)));
        path.AddArc(r.Left, r.Top, d, d, 180, 90); path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure(); return path;
    }
    internal static void Apply(Form form)
    {
        form.Font = Font(); form.ForeColor = Ink; form.BackColor = Background;
        form.AutoScaleDimensions = new SizeF(96, 96); form.AutoScaleMode = AutoScaleMode.Dpi;
        form.HandleCreated += (_, _) => PaintChrome(form);
    }
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);
    private static void PaintChrome(Form form)
    {
        var dark = DarkMode && !Contrast ? 1 : 0;
        _ = DwmSetWindowAttribute(form.Handle, 20, ref dark, sizeof(int));
        if (Contrast) return;
        var caption = Background.R | Background.G << 8 | Background.B << 16;
        var text = Ink.R | Ink.G << 8 | Ink.B << 16;
        _ = DwmSetWindowAttribute(form.Handle, 35, ref caption, sizeof(int));
        _ = DwmSetWindowAttribute(form.Handle, 36, ref text, sizeof(int));
    }
}

internal enum UiGlyph { None, Game, Monitor, Sliders, Search, Plus, Scan, Check, Frame, Close }
internal static class Glyphs
{
    internal static void Draw(Graphics graphics, UiGlyph glyph, Rectangle bounds, Color color)
    {
        var state = graphics.Save();
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TranslateTransform(bounds.X, bounds.Y); graphics.ScaleTransform(bounds.Width / 24f, bounds.Height / 24f);
        using var pen = new Pen(color, 2) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        void Line(float x, float y, float x2, float y2) => graphics.DrawLine(pen, x, y, x2, y2);
        switch (glyph)
        {
            case UiGlyph.Monitor:
                using (var p = UiTheme.Round(new RectangleF(2, 3, 20, 14), 2)) graphics.DrawPath(pen, p);
                Line(12, 17, 12, 21); Line(8, 21, 16, 21); break;
            case UiGlyph.Game:
                using (var p = new GraphicsPath())
                {
                    p.AddBezier(6.68f, 5, 4.6f, 5, 3, 6.5f, 2.7f, 8.6f);
                    p.AddBezier(2.7f, 8.6f, 2.4f, 11, 2, 14.4f, 2, 16);
                    p.AddBezier(2, 16, 2, 18, 3.2f, 19, 5, 19);
                    p.AddBezier(5, 19, 6, 19, 6.5f, 18.5f, 7, 18);
                    p.AddLine(7, 18, 8.414f, 16.586f); p.AddBezier(8.414f, 16.586f, 8.8f, 16.2f, 9.2f, 16, 9.828f, 16);
                    p.AddLine(9.828f, 16, 14.172f, 16); p.AddBezier(14.172f, 16, 14.8f, 16, 15.2f, 16.2f, 15.586f, 16.586f);
                    p.AddLine(15.586f, 16.586f, 17, 18); p.AddBezier(17, 18, 17.5f, 18.5f, 18, 19, 19, 19);
                    p.AddBezier(19, 19, 20.8f, 19, 22, 18, 22, 16); p.AddBezier(22, 16, 22, 14.4f, 21.6f, 11, 21.3f, 8.6f);
                    p.AddBezier(21.3f, 8.6f, 21, 6.5f, 19.4f, 5, 17.32f, 5); p.CloseFigure(); graphics.DrawPath(pen, p);
                }
                Line(6, 11, 10, 11); Line(8, 9, 8, 13); Line(15, 12, 15.01f, 12); Line(18, 10, 18.01f, 10); break;
            case UiGlyph.Sliders:
                Line(3, 5, 10, 5); Line(14, 5, 21, 5); Line(14, 3, 14, 7);
                Line(3, 12, 8, 12); Line(12, 12, 21, 12); Line(8, 10, 8, 14);
                Line(3, 19, 12, 19); Line(16, 19, 21, 19); Line(16, 17, 16, 21); break;
            case UiGlyph.Search: graphics.DrawEllipse(pen, 3, 3, 13, 13); Line(16, 16, 21, 21); break;
            case UiGlyph.Close: Line(6,6,18,18); Line(18,6,6,18); break;
            case UiGlyph.Plus: Line(12, 5, 12, 19); Line(5, 12, 19, 12); break;
            case UiGlyph.Scan:
                Line(3, 8, 3, 3); Line(3, 3, 8, 3); Line(16, 3, 21, 3); Line(21, 3, 21, 8);
                Line(21, 16, 21, 21); Line(21, 21, 16, 21); Line(8, 21, 3, 21); Line(3, 21, 3, 16); Line(7, 12, 17, 12); break;
            case UiGlyph.Check: Line(5, 12, 9, 16); Line(9, 16, 19, 6); break;
            case UiGlyph.Frame:
                Line(2, 8, 2, 2); Line(2, 2, 8, 2); Line(16, 2, 22, 2); Line(22, 2, 22, 8);
                Line(22, 16, 22, 22); Line(22, 22, 16, 22); Line(8, 22, 2, 22); Line(2, 22, 2, 16); break;
        }
        graphics.Restore(state);
    }
}

internal class ActionButton : Button
{
    internal bool Primary;
    internal bool Quiet;
    internal bool Selected;
    internal UiGlyph Glyph;
    internal bool Navigation;
    private bool hovered, pressed;
    internal ActionButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; Font = UiTheme.Font(10, true);
        Cursor = Cursors.Hand; UseVisualStyleBackColor = false; BackColor = UiTheme.Surface;
    }
    protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovered = pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? UiTheme.Background);
        var scale = DeviceDpi / 96f;
        var fill = !Enabled ? UiTheme.Raised : Primary ? UiTheme.PrimaryFill : pressed || hovered || Selected ? UiTheme.Raised : Quiet ? Parent?.BackColor ?? UiTheme.Background : UiTheme.Surface;
        if (Primary && Enabled && (hovered || pressed)) fill = UiTheme.DarkMode ? Color.FromArgb(206, 218, 211) : Color.FromArgb(16, 82, 59);
        var ink = !Enabled ? UiTheme.Muted : Primary ? UiTheme.PrimaryInk : UiTheme.Ink;
        using var path = UiTheme.Round(new RectangleF(1, 1, Width - 3, Height - 3), 8 * scale);
        using var brush = new SolidBrush(fill); g.FillPath(brush, path);
        if (!Quiet && !Primary || Selected) { using var border = new Pen(UiTheme.Line); g.DrawPath(border, path); }
        if (Focused && ShowFocusCues) { using var focus = new Pen(UiTheme.Green, 2 * scale); g.DrawPath(focus, path); }
        var rect = new Rectangle((int)(12 * scale), 0, Width - (int)(24 * scale), Height);
        if (Glyph != UiGlyph.None)
        {
            var icon = (int)(18 * scale); var gap = (int)(8 * scale);
            var x = Navigation ? rect.Left : (Width - TextRenderer.MeasureText(Text, Font).Width - icon - gap) / 2;
            Glyphs.Draw(g, Glyph, new Rectangle(x, (Height - icon) / 2, icon, icon), ink);
            rect.X = x + icon + gap; rect.Width = Width - rect.X - (int)(12 * scale);
        }
        TextRenderer.DrawText(g, Text, Font, rect, ink, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix
            | (Navigation || Glyph != UiGlyph.None ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter));
    }
}

internal sealed class SurfacePanel : Panel
{
    internal SurfacePanel() { DoubleBuffered = true; BackColor = UiTheme.Surface; Padding = new Padding(24); Margin = Padding.Empty; }
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? UiTheme.Background); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = UiTheme.Round(new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), 12 * DeviceDpi / 96f);
        using var fill = new SolidBrush(BackColor); e.Graphics.FillPath(fill, path);
        using var pen = new Pen(UiTheme.Line); e.Graphics.DrawPath(pen, path);
    }
}
internal class FieldFrame : Panel
{
    internal FieldFrame() { DoubleBuffered = true; BackColor = UiTheme.Surface; }
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? UiTheme.Background); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = UiTheme.Round(new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), 8 * DeviceDpi / 96f);
        using var fill = new SolidBrush(UiTheme.Surface); e.Graphics.FillPath(fill, path);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = UiTheme.Round(new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), 8 * DeviceDpi / 96f);
        using var pen = new Pen(ContainsFocus ? UiTheme.Green : UiTheme.Line, ContainsFocus ? 2 : 1); e.Graphics.DrawPath(pen, path);
    }
}

internal sealed class ToggleSwitch : CheckBox
{
    internal ToggleSwitch()
    {
        AutoSize = false; Size = new Size(40, 24); Cursor = Cursors.Hand;
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
    }
    protected override void OnCheckedChanged(EventArgs e) { Invalidate(); base.OnCheckedChanged(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var s = DeviceDpi / 96f; var g = e.Graphics; g.Clear(Parent?.BackColor ?? UiTheme.Surface); g.SmoothingMode = SmoothingMode.AntiAlias;
        var track = new RectangleF(1, 3 * s, Width - 3, Height - 6 * s);
        using var path = UiTheme.Round(track, track.Height / 2); using var fill = new SolidBrush(Checked ? UiTheme.Green : UiTheme.Muted); g.FillPath(fill, path);
        var knob = track.Height - 6 * s; using var white = new SolidBrush(Checked ? UiTheme.AccentInk : Color.White);
        g.FillEllipse(white, Checked ? track.Right - knob - 3 * s : track.Left + 3 * s, track.Top + 3 * s, knob, knob);
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(g, ClientRectangle, UiTheme.Ink, UiTheme.Surface);
    }
}

internal sealed class GameListView : ListView
{
    internal event Action? ViewportChanged;
    private bool viewportQueued;
    internal void QueueViewportUpdate()
    {
        if(viewportQueued || !IsHandleCreated || IsDisposed || Disposing) return;
        viewportQueued=true;
        BeginInvoke((Action)(()=>
        {
            try
            {
                if(IsDisposed || Disposing || !IsHandleCreated)return;
                ViewportChanged?.Invoke();
            }
            finally { viewportQueued=false; }
        }));
    }
    protected override void WndProc(ref Message message)
    {
        base.WndProc(ref message);
        if(message.Msg is 0x0005 or 0x0100 or 0x0115 or 0x0114 or 0x020A or 0x1007 or 0x104D or 0x1008 or 0x1009 or 0x1014 or 0x1013) QueueViewportUpdate();
    }
    private readonly ImageList rowSizing = new() { ImageSize = new Size(1, 44), ColorDepth = ColorDepth.Depth32Bit };
    internal GameListView()
    {
        DoubleBuffered = true; OwnerDraw = true; SmallImageList = rowSizing;
        Font = UiTheme.Font(); BackColor = UiTheme.Surface; ForeColor = UiTheme.Ink;
    }
    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); SizeRows(); }
    protected override void OnDpiChangedAfterParent(EventArgs e) { base.OnDpiChangedAfterParent(e); SizeRows(); }
    private void SizeRows() => rowSizing.ImageSize = new Size(1, Math.Clamp(44 * DeviceDpi / 96, 1, 256));
    protected override void OnDrawColumnHeader(DrawListViewColumnHeaderEventArgs e)
    {
        using var fill = new SolidBrush(UiTheme.Surface);
        var background = e.Bounds; if (e.ColumnIndex == Columns.Count - 1) background.Width = ClientSize.Width - background.Left;
        e.Graphics.FillRectangle(fill, background);
        using var font = UiTheme.Font(9);
        var r = e.Bounds; r.X += (int)((e.ColumnIndex == 0 ? 36 : 8) * DeviceDpi / 96f); r.Width -= (int)(16 * DeviceDpi / 96f);
        TextRenderer.DrawText(e.Graphics, e.Header?.Text, font, r, UiTheme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        using var pen = new Pen(UiTheme.Line); e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
    }
    protected override void OnDrawItem(DrawListViewItemEventArgs e) { if (View != View.Details) e.DrawDefault = true; }
    protected override void OnDrawSubItem(DrawListViewSubItemEventArgs e)
    {
        var selected = e.Item?.Selected == true; var r = e.Bounds;
        using var fill = new SolidBrush(selected ? UiTheme.Raised : UiTheme.Surface); e.Graphics.FillRectangle(fill, r);
        var scale = DeviceDpi / 96f;
        if (e.ColumnIndex == 0 && e.Item is { } item)
        {
            var box = new Rectangle(r.Left + (int)(4 * scale), r.Top + (r.Height - (int)(16 * scale)) / 2, (int)(16 * scale), (int)(16 * scale));
            using var p = UiTheme.Round(box, 4 * scale);
            using var b = new SolidBrush(item.Checked ? UiTheme.Green : UiTheme.Surface); e.Graphics.FillPath(b, p);
            using var line = new Pen(item.Checked ? UiTheme.Green : UiTheme.Muted); e.Graphics.DrawPath(line, p);
            if (item.Checked) Glyphs.Draw(e.Graphics, UiGlyph.Check, box, UiTheme.AccentInk);
            r.X += (int)(36 * scale); r.Width -= (int)(40 * scale);
        }
        else { r.X += (int)(8 * scale); r.Width -= (int)(16 * scale); }
        TextRenderer.DrawText(e.Graphics, e.SubItem?.Text, Font, r, e.ColumnIndex == 0 ? UiTheme.Ink : e.SubItem?.ForeColor ?? UiTheme.Secondary,
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        if (selected && Focused && e.ColumnIndex == 0) ControlPaint.DrawFocusRectangle(e.Graphics, e.Bounds, UiTheme.Ink, UiTheme.Raised);
    }
    protected override void Dispose(bool disposing) { if (disposing) rowSizing.Dispose(); base.Dispose(disposing); }
}

internal sealed class DisplayPreview : Control
{
    private int resolutionWidth = 2324, resolutionHeight = 1306, refreshRate;
    internal void SetResolution(int width, int height, int hz = 0)
    {
        resolutionWidth = width; resolutionHeight = height; refreshRate = hz;
        AccessibleName = $"Zone {width} × {height} centrée sur une dalle 2560 × 1440"; Invalidate();
    }
    internal DisplayPreview() { DoubleBuffered = true; ResizeRedraw = true; Dock = DockStyle.Fill; BackColor = UiTheme.Surface; TabStop = false; }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        var s = DeviceDpi / 96f;
        var width = Math.Min(ClientSize.Width - (int)(32 * s), (ClientSize.Height - (int)(88 * s)) * 16 / 9);
        if (width < 80) return;
        var screen = new Rectangle((ClientSize.Width - width) / 2, (int)(32 * s), width, width * 9 / 16);
        using var device = UiTheme.Round(Rectangle.Inflate(screen, (int)(6 * s), (int)(6 * s)), 8 * s);
        using var dark = new SolidBrush(Color.FromArgb(42, 49, 55)); g.FillPath(dark, device);
        using var black = new SolidBrush(Color.FromArgb(17, 22, 26)); g.FillRectangle(black, screen);
        var w = screen.Width * resolutionWidth / 2560; var h = screen.Height * resolutionHeight / 1440;
        var active = new Rectangle(screen.Left + (screen.Width - w) / 2, screen.Top + (screen.Height - h) / 2, w, h);
        using var fill = new SolidBrush(UiTheme.GreenSoft); g.FillRectangle(fill, active);
        using var rule = new Pen(UiTheme.Green, 1.5f * s);
        var corner = Math.Min((int)(12 * s), Math.Min(w, h) / 4);
        foreach (var x in new[] { active.Left + 1, active.Right - 1 }) foreach (var y in new[] { active.Top + 1, active.Bottom - 1 })
        { g.DrawLine(rule, x, y, x + (x < active.Right - 1 ? corner : -corner), y); g.DrawLine(rule, x, y, x, y + (y < active.Bottom - 1 ? corner : -corner)); }
        using var title = UiTheme.Font(w < 180 * s ? 11 : 17, true);
        TextRenderer.DrawText(g, $"{resolutionWidth} × {resolutionHeight}", title, active, UiTheme.Green, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        using var meta = UiTheme.Figures(9);
        TextRenderer.DrawText(g, "2560 × 1440 · dalle native", meta, new Rectangle(screen.Left, 0, screen.Width, (int)(24 * s)), UiTheme.Muted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        using var stand = new Pen(Color.FromArgb(99, 110, 120), 3 * s);
        g.DrawLine(stand, screen.Left + width / 2, screen.Bottom + 8 * s, screen.Left + width / 2, screen.Bottom + 22 * s);
        g.DrawLine(stand, screen.Left + width / 2 - 24 * s, screen.Bottom + 22 * s, screen.Left + width / 2 + 24 * s, screen.Bottom + 22 * s);
        TextRenderer.DrawText(g, refreshRate > 0 ? $"Centrage 1:1 · {refreshRate} Hz" : "Centrage 1:1", meta,
            new Rectangle(0, screen.Bottom + (int)(30 * s), Width, (int)(20 * s)), UiTheme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}
