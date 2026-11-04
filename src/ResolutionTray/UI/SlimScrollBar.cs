using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace ResolutionTray.UI;

/// <summary>Shared themed scrollbar, using content/viewport units rather than native non-client chrome.</summary>
internal sealed class SlimScrollBar : Control
{
    private int extent, viewport = 1, value, dragStart, dragValue, wheelRemainder;
    private bool hovered, dragging;
    internal bool Horizontal;
    internal int SmallChange = 1;
    internal event Action? ValueChanged;
    internal int Maximum => Math.Max(0, extent - viewport);
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal int Value
    {
        get => value;
        set
        {
            var next = Math.Clamp(value, 0, Maximum);
            if (this.value == next) return;
            this.value = next; Invalidate(); ValueChanged?.Invoke();
            if (IsHandleCreated) AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
        }
    }
    internal SlimScrollBar()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        Width = Height = 16; TabStop = true; AccessibleRole = AccessibleRole.ScrollBar; AccessibleName = "Défilement";
    }
    internal void SetRange(int content, int visible)
    {
        content = Math.Max(0, content); visible = Math.Max(1, visible);
        if (extent == content && viewport == visible) return;
        extent = content; viewport = visible; Value = value; Invalidate();
    }
    private int TrackLength => Math.Max(0, (Horizontal ? Width : Height) - 8 * DeviceDpi / 96);
    private int ThumbLength => Maximum == 0 ? TrackLength : Math.Min(TrackLength, Math.Max(24 * DeviceDpi / 96, (int)((long)TrackLength * viewport / Math.Max(1, extent))));
    private Rectangle Thumb
    {
        get
        {
            var start = 4 * DeviceDpi / 96 + (Maximum == 0 ? 0 : (int)((long)(TrackLength - ThumbLength) * Value / Maximum));
            var thickness = (hovered || dragging ? 8 : 6) * DeviceDpi / 96;
            return Horizontal ? new(start, (Height - thickness) / 2, ThumbLength, thickness) : new((Width - thickness) / 2, start, thickness, ThumbLength);
        }
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? UiTheme.Surface); if (Maximum == 0 || Thumb.Width <= 0 || Thumb.Height <= 0) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = UiTheme.Round(Thumb, Math.Min(Thumb.Width, Thumb.Height) / 2f);
        using var fill = new SolidBrush(dragging || hovered || Focused ? UiTheme.Secondary : UiTheme.Muted); e.Graphics.FillPath(fill, path);
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, ClientRectangle, UiTheme.Ink, BackColor);
    }
    protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovered = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e); if (e.Button != MouseButtons.Left || Maximum == 0) return;
        Focus(); var coordinate = Horizontal ? e.X : e.Y; var thumb = Thumb;
        if (thumb.Contains(e.Location)) { dragging = true; Capture = true; dragStart = coordinate; dragValue = Value; }
        else Value += coordinate < (Horizontal ? thumb.Left : thumb.Top) ? -viewport : viewport;
        Invalidate();
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e); if (!dragging || TrackLength <= ThumbLength) return;
        var delta = (Horizontal ? e.X : e.Y) - dragStart;
        Value = (int)Math.Clamp(dragValue + (long)delta * Maximum / (TrackLength - ThumbLength), 0, Maximum);
    }
    protected override void OnMouseUp(MouseEventArgs e) { dragging = false; Capture = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnMouseCaptureChanged(EventArgs e) { if (!Capture) dragging = false; base.OnMouseCaptureChanged(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown || base.IsInputKey(keyData);
    protected override void OnKeyDown(KeyEventArgs e)
    {
        var next = e.KeyCode switch { Keys.Up or Keys.Left => Value - SmallChange, Keys.Down or Keys.Right => Value + SmallChange,
            Keys.PageUp => Value - viewport, Keys.PageDown => Value + viewport, Keys.Home => 0, Keys.End => Maximum, _ => Value };
        if (e.KeyCode is Keys.Up or Keys.Left or Keys.Down or Keys.Right or Keys.PageUp or Keys.PageDown or Keys.Home or Keys.End) { Value = next; e.Handled = e.SuppressKeyPress = true; }
        base.OnKeyDown(e);
    }
    internal void Wheel(int delta)
    {
        var lines = SystemInformation.MouseWheelScrollLines;
        wheelRemainder += delta; var steps=wheelRemainder/120;wheelRemainder%=120;
        Value -= steps * (lines < 0 ? viewport : Math.Max(1, lines) * Math.Max(1, SmallChange));
    }
    protected override void OnMouseWheel(MouseEventArgs e) { Wheel(e.Delta); if (e is HandledMouseEventArgs handled) handled.Handled = true; base.OnMouseWheel(e); }
    protected override AccessibleObject CreateAccessibilityInstance() => new ScrollAccessibleObject(this);
    private sealed class ScrollAccessibleObject(SlimScrollBar owner) : ControlAccessibleObject(owner)
    {
        public override AccessibleRole Role => AccessibleRole.ScrollBar;
        public override string? Value { get => owner.Value.ToString(System.Globalization.CultureInfo.InvariantCulture); set { if (int.TryParse(value, out var parsed)) owner.Value = parsed; } }
        public override AccessibleStates State => base.State | AccessibleStates.Focusable;
    }
}

/// <summary>Scrollable page with a clipped content plane and a scrollbar outside the viewport.</summary>
internal sealed class ScrollViewport : UserControl
{
    private readonly Panel viewport = new() { Dock = DockStyle.Fill, Margin = Padding.Empty };
    private readonly SlimScrollBar scroll = new() { Dock = DockStyle.Right, Name = "pageScroll", SmallChange = 40 };
    private Control? content;
    private bool updating;
    internal ScrollViewport()
    {
        Margin = Padding.Empty; AutoScroll = false; TabStop = false;
        Controls.Add(viewport); Controls.Add(scroll); scroll.Visible = false;
        scroll.ValueChanged += () => { if (content is not null) content.Top = -scroll.Value; };
        viewport.SizeChanged += (_, _) => UpdateScroll();
    }
    internal void SetContent(Control child)
    {
        if (content is not null) throw new InvalidOperationException("Le contenu du défilement est déjà défini.");
        // This viewport owns both bounds. AutoSize would shrink a Dock=None panel
        // with top/fill-docked children back to its horizontal padding (48 px).
        // Measure its preferred height explicitly, while keeping the viewport width.
        content = child; child.AutoSize = false; child.Dock = DockStyle.None; child.Margin = Padding.Empty;
        viewport.Controls.Add(child);
        child.SizeChanged += (_, _) => UpdateScroll();
        child.Layout += (_, _) => UpdateScroll();
        HookWheel(child); UpdateScroll();
    }
    private void HookWheel(Control child)
    {
        child.MouseWheel += (_, e) => { if (e is HandledMouseEventArgs { Handled:true } || scroll.Maximum == 0) return; scroll.Wheel(e.Delta); if (e is HandledMouseEventArgs handled) handled.Handled = true; };
        child.ControlAdded += (_, e) => { if (e.Control is not null) HookWheel(e.Control); };
        foreach (Control nested in child.Controls) HookWheel(nested);
    }
    protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); UpdateScroll(); }
    protected override void OnDpiChangedAfterParent(EventArgs e) { base.OnDpiChangedAfterParent(e); UpdateScroll(); }
    internal void UpdateScroll()
    {
        if (updating || content is null || IsDisposed || Disposing || viewport.ClientSize.Width<=0 || viewport.ClientSize.Height<=0) return;
        updating = true;
        try
        {
            scroll.Width = 16 * DeviceDpi / 96; scroll.SmallChange = 40 * DeviceDpi / 96;
            for (var pass=0; pass<2; pass++)
            {
                content.Width = Math.Max(1, viewport.ClientSize.Width);
                content.Height = content.GetPreferredSize(new Size(content.Width, 0)).Height;
                scroll.Visible = content.Height > viewport.ClientSize.Height;
                PerformLayout();
            }
            scroll.SetRange(content.Height, viewport.ClientSize.Height); content.Location = new Point(0, -scroll.Value);
        }
        finally { updating = false; }
    }
    protected override void OnMouseWheel(MouseEventArgs e) { if (scroll.Maximum>0) { scroll.Wheel(e.Delta); if(e is HandledMouseEventArgs handled) handled.Handled=true; } else base.OnMouseWheel(e); }
}
