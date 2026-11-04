using System.Drawing.Drawing2D;

namespace AMZ.Resolution.UI;

/// <summary>Native value model, with an application-drawn face and an application-drawn popup.</summary>
internal sealed class ThemedComboBox : ComboBox
{
    private ToolStripDropDown? popup;
    private ChoicePanel? choices;
    private bool suppressMouseReopen;
    internal ThemedComboBox() { UiTheme.ThemeChanged+=RefreshPopup; }
    private void RefreshPopup() { if(popup is null)return;popup.BackColor=UiTheme.Surface;if(choices is not null)UiTheme.Recolor(choices);popup.Invalidate(true); }
    internal bool IsPopupOpen => popup?.Visible == true;
    internal ChoiceCanvas? ChoiceList => choices?.Canvas;
    internal void OpenChoices()
    {
        if(!Enabled || Items.Count==0 || !IsHandleCreated || IsPopupOpen) return;
        popup?.Dispose();
        var scale=DeviceDpi/96f; var anchor=Parent is FieldFrame?Parent:this;
        var area=Screen.FromControl(this).WorkingArea;
        var row=(int)(36*scale); var padding=(int)(8*scale);
        var measured=Items.Cast<object>().Max(i=>TextRenderer.MeasureText(i.ToString(),Font).Width)+(int)(56*scale);
        var width=Math.Min(area.Width-padding*2,Math.Max(anchor.Width,measured));
        var below=anchor.PointToScreen(new Point(0,anchor.Height+(int)(4*scale)));
        var above=anchor.PointToScreen(Point.Empty);
        var wanted=Math.Min(8,Items.Count)*row+padding;
        var useAbove=area.Bottom-below.Y<wanted && above.Y-area.Top>area.Bottom-below.Y;
        var available=useAbove?above.Y-area.Top:area.Bottom-below.Y;
        var height=Math.Min(wanted,Math.Max(row+padding,available-(int)(4*scale)));
        var x=Math.Clamp(below.X,area.Left,Math.Max(area.Left,area.Right-width));
        var y=Math.Clamp(useAbove?above.Y-height-(int)(4*scale):below.Y,area.Top,Math.Max(area.Top,area.Bottom-height));
        choices=new ChoicePanel(Items.Cast<object>().Select(i=>i.ToString()??"").ToArray(),SelectedIndex,Commit) { Size=new Size(width-padding,height-padding) };
        var host=new ToolStripControlHost(choices) { AutoSize=false, Size=choices.Size, Margin=Padding.Empty, Padding=Padding.Empty };
        popup=new ToolStripDropDown { AutoSize=false, Size=new Size(width,height), Padding=new Padding(padding/2), Margin=Padding.Empty,
            AutoClose=true, DropShadowEnabled=false, BackColor=UiTheme.Surface, Renderer=new ChoiceRenderer() };
        popup.Items.Add(host);
        using(var shape=UiTheme.Round(new RectangleF(0,0,width,height),10*scale)) popup.Region=new Region(shape);
        popup.Closed+=(_,e)=>
        {
            suppressMouseReopen=e.CloseReason==ToolStripDropDownCloseReason.AppClicked && anchor.RectangleToScreen(anchor.ClientRectangle).Contains(Cursor.Position);
            Invalidate(); Parent?.Invalidate(); AccessibilityNotifyClients(AccessibleEvents.StateChange,-1);
            if(e.CloseReason is ToolStripDropDownCloseReason.Keyboard or ToolStripDropDownCloseReason.ItemClicked && CanFocus) Focus();
        };
        popup.Show(anchor,anchor.PointToClient(new Point(x,y))); choices.Canvas.Focus(); choices.EnsureActiveVisible();
        Invalidate(); AccessibilityNotifyClients(AccessibleEvents.StateChange,-1);
    }
    private void Commit(int index)
    {
        if(index>=0 && index<Items.Count) SelectedIndex=index;
        popup?.Close(ToolStripDropDownCloseReason.ItemClicked);
    }
    internal void CloseChoices() => popup?.Close(ToolStripDropDownCloseReason.Keyboard);
    protected override bool ProcessCmdKey(ref Message message, Keys keyData)
    {
        if(keyData==Keys.Escape && IsPopupOpen) { CloseChoices();return true; }
        if(keyData is Keys.F4 or (Keys.Alt|Keys.Down) or Keys.Space or Keys.Enter)
        { if(IsPopupOpen) CloseChoices();else OpenChoices();return true; }
        return base.ProcessCmdKey(ref message,keyData);
    }
    protected override void WndProc(ref Message message)
    {
        if(message.Msg==0x014F) { if(message.WParam!=0)OpenChoices();else CloseChoices();message.Result=0;return; }
        if(message.Msg is 0x0201 or 0x0203) { Focus();return; }
        if(message.Msg==0x0202) { if(suppressMouseReopen){suppressMouseReopen=false;return;}if(IsPopupOpen)CloseChoices();else OpenChoices();return; }
        base.WndProc(ref message);
        if(message.Msg!=0x000F || !IsHandleCreated)return;
        using var graphics=Graphics.FromHwnd(Handle); using var surface=new SolidBrush(UiTheme.Surface); graphics.FillRectangle(surface,ClientRectangle);
        var s=DeviceDpi/96f;
        TextRenderer.DrawText(graphics,SelectedItem?.ToString()??"",Font,new Rectangle((int)(4*s),0,Math.Max(0,Width-(int)(28*s)),Height),Enabled?UiTheme.Ink:UiTheme.Muted,
            TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix);
        graphics.SmoothingMode=SmoothingMode.AntiAlias;
        using var pen=new Pen(UiTheme.Secondary,1.5f*s) { StartCap=LineCap.Round,EndCap=LineCap.Round };
        var x=Width-12*s;var y=Height/2f;var direction=IsPopupOpen?-1:1;
        graphics.DrawLines(pen,[new PointF(x-3*s,y-direction*s),new PointF(x,y+2*direction*s),new PointF(x+3*s,y-direction*s)]);
    }
    protected override void OnEnabledChanged(EventArgs e) { if(!Enabled) CloseChoices();base.OnEnabledChanged(e); }
    protected override void OnVisibleChanged(EventArgs e) { if(!Visible) CloseChoices();base.OnVisibleChanged(e); }
    protected override void Dispose(bool disposing) { if(disposing) {UiTheme.ThemeChanged-=RefreshPopup;popup?.Dispose();popup=null;}base.Dispose(disposing); }
    private sealed class ChoiceRenderer : ToolStripRenderer
    {
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)=>e.Graphics.Clear(UiTheme.Surface);
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using var shape=UiTheme.Round(new RectangleF(.5f,.5f,e.ToolStrip.Width-1,e.ToolStrip.Height-1),10*e.ToolStrip.DeviceDpi/96f);
            using var pen=new Pen(UiTheme.Line);e.Graphics.DrawPath(pen,shape);
        }
    }
}

internal sealed class ChoicePanel : UserControl
{
    internal readonly ChoiceCanvas Canvas;
    internal readonly SlimScrollBar Bar=new() { Dock=DockStyle.Right,Name="choiceScroll",AccessibleName="Défilement des options" };
    internal ChoicePanel(string[] items,int selected,Action<int> commit)
    {
        Margin=Padding.Empty;Padding=Padding.Empty;BackColor=UiTheme.Surface;
        Canvas=new ChoiceCanvas(items,selected,commit) {Dock=DockStyle.Fill};
        Controls.Add(Canvas);Controls.Add(Bar);
        Canvas.RequestClose+=()=>FindDropDown()?.Close(ToolStripDropDownCloseReason.Keyboard);
        Canvas.ActiveChanged+=EnsureActiveVisible;
        Bar.ValueChanged+=()=>{Canvas.FirstVisible=Bar.Value;Canvas.Invalidate();};
        SizeChanged+=(_,_)=>UpdateRange();
    }
    private ToolStripDropDown? FindDropDown()=>Parent is ToolStripDropDown drop?drop:Parent?.Parent as ToolStripDropDown;
    private void UpdateRange()
    {
        Bar.Width=16*DeviceDpi/96;var page=Math.Max(1,Canvas.Height/Canvas.RowHeight);
        Bar.Visible=Canvas.Count>page;Bar.SetRange(Canvas.Count,page);
    }
    internal void EnsureActiveVisible()
    {
        UpdateRange();var page=Math.Max(1,Canvas.Height/Canvas.RowHeight);
        if(Canvas.ActiveIndex<Bar.Value)Bar.Value=Canvas.ActiveIndex;
        else if(Canvas.ActiveIndex>=Bar.Value+page)Bar.Value=Canvas.ActiveIndex-page+1;
    }
    protected override void OnPaintBackground(PaintEventArgs e)=>e.Graphics.Clear(UiTheme.Surface);
}

internal sealed class ChoiceCanvas : Control
{
    private readonly string[] items;
    private readonly int selected;
    private readonly Action<int> commit;
    private int active;
    private string prefix="";
    private long lastTyped;
    internal int FirstVisible;
    internal int Count=>items.Length;
    internal int RowHeight=>36*DeviceDpi/96;
    internal int ActiveIndex=>active;
    internal event Action? ActiveChanged;
    internal event Action? RequestClose;
    internal ChoiceCanvas(string[] items,int selected,Action<int> commit)
    {
        this.items=items;this.selected=selected;this.commit=commit;active=Math.Clamp(selected,0,Math.Max(0,items.Length-1));
        SetStyle(ControlStyles.Selectable|ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint,true);
        TabStop=true;AccessibleRole=AccessibleRole.List;AccessibleName="Options";Font=UiTheme.Font();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(UiTheme.Surface);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;var s=DeviceDpi/96f;
        var end=Math.Min(items.Length,FirstVisible+Height/Math.Max(1,RowHeight)+1);
        for(var i=FirstVisible;i<end;i++)
        {
            var row=new Rectangle(0,(i-FirstVisible)*RowHeight,Width,RowHeight);
            if(i==active) { using var shape=UiTheme.Round(Rectangle.Inflate(row,-2,-2),6*s);using var fill=new SolidBrush(UiTheme.Raised);e.Graphics.FillPath(fill,shape); }
            var text=new Rectangle((int)(12*s),row.Top,Math.Max(0,Width-(int)(48*s)),row.Height);
            TextRenderer.DrawText(e.Graphics,items[i],Font,text,UiTheme.Ink,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
            if(i==selected)Glyphs.Draw(e.Graphics,UiGlyph.Check,new Rectangle(Width-(int)(30*s),row.Top+(row.Height-(int)(18*s))/2,(int)(18*s),(int)(18*s)),UiTheme.Green);
        }
    }
    private void SetActive(int index)
    {
        index=Math.Clamp(index,0,Math.Max(0,items.Length-1));if(active==index)return;active=index;Invalidate();ActiveChanged?.Invoke();
        AccessibilityNotifyClients(AccessibleEvents.Focus,index+1);
    }
    protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e);var index=FirstVisible+e.Y/Math.Max(1,RowHeight);if(index<items.Length)SetActive(index); }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e);var index=FirstVisible+e.Y/Math.Max(1,RowHeight);if(e.Button==MouseButtons.Left&&ClientRectangle.Contains(e.Location)&&index>=0&&index<items.Length)commit(index); }
    protected override void OnMouseWheel(MouseEventArgs e) { if(Parent is ChoicePanel panel)panel.Bar.Wheel(e.Delta);if(e is HandledMouseEventArgs handled)handled.Handled=true; }
    protected override bool IsInputKey(Keys keyData)=>(keyData&Keys.KeyCode) is Keys.Up or Keys.Down or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown or Keys.Enter or Keys.Escape or Keys.Space || base.IsInputKey(keyData);
    internal bool HandleKey(Keys key)
    {
        var page=Math.Max(1,Height/Math.Max(1,RowHeight));
        switch(key)
        {
            case Keys.Up:SetActive(active-1);break;case Keys.Down:SetActive(active+1);break;case Keys.Home:SetActive(0);break;case Keys.End:SetActive(items.Length-1);break;
            case Keys.PageUp:SetActive(active-page);break;case Keys.PageDown:SetActive(active+page);break;
            case Keys.Enter:case Keys.Space:commit(active);break;case Keys.Escape:RequestClose?.Invoke();break;default:return false;
        }
        return true;
    }
    protected override bool ProcessCmdKey(ref Message message,Keys keyData)
    {
        if(keyData is Keys.F4 or (Keys.Alt|Keys.Up)){RequestClose?.Invoke();return true;}
        return base.ProcessCmdKey(ref message,keyData);
    }
    protected override void OnKeyDown(KeyEventArgs e) { if(HandleKey(e.KeyCode))e.Handled=e.SuppressKeyPress=true;base.OnKeyDown(e); }
    protected override bool ProcessDialogKey(Keys keyData)
    {
        if((keyData&Keys.KeyCode)==Keys.Tab) { RequestClose?.Invoke();return false; }
        return HandleKey(keyData&Keys.KeyCode)||base.ProcessDialogKey(keyData);
    }
    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        if(!char.IsControl(e.KeyChar))
        {
            if(Environment.TickCount64-lastTyped>700)prefix="";lastTyped=Environment.TickCount64;prefix+=e.KeyChar;
            var match=Array.FindIndex(items,i=>i.StartsWith(prefix,StringComparison.CurrentCultureIgnoreCase));
            if(match>=0)SetActive(match);e.Handled=true;
        }
        base.OnKeyPress(e);
    }
    protected override AccessibleObject CreateAccessibilityInstance()=>new OptionsAccessible(this);
    private sealed class OptionsAccessible(ChoiceCanvas owner):ControlAccessibleObject(owner)
    {
        public override AccessibleRole Role=>AccessibleRole.List;
        public override int GetChildCount()=>owner.items.Length;
        public override AccessibleObject? GetChild(int index)=>index>=0&&index<owner.items.Length?new OptionAccessible(owner,index):null;
    }
    private sealed class OptionAccessible(ChoiceCanvas owner,int index):AccessibleObject
    {
        public override string? Name {get=>owner.items[index];set{}}
        public override AccessibleRole Role=>AccessibleRole.ListItem;
        public override AccessibleObject? Parent=>owner.AccessibilityObject;
        public override AccessibleStates State=>AccessibleStates.Selectable|(index==owner.active?AccessibleStates.Focused:0)|(index==owner.selected?AccessibleStates.Selected:0)
            |(index<owner.FirstVisible||(index-owner.FirstVisible)*owner.RowHeight>=owner.Height?AccessibleStates.Offscreen:0);
        public override Rectangle Bounds=>owner.RectangleToScreen(new Rectangle(0,(index-owner.FirstVisible)*owner.RowHeight,owner.Width,owner.RowHeight));
        public override string? DefaultAction=>"Choisir";
        public override void DoDefaultAction()=>owner.commit(index);
    }
}
