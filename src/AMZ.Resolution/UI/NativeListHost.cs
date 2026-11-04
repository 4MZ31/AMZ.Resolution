using System.Runtime.InteropServices;

namespace AMZ.Resolution.UI;

internal static class NativeScrolling
{
    [DllImport("user32.dll")] private static extern int GetSystemMetricsForDpi(int index,uint dpi);
    internal static int ScrollbarWidth(int dpi)=>GetSystemMetricsForDpi(2,(uint)dpi);
    [DllImport("user32.dll", CharSet=CharSet.Auto)] internal static extern nint SendMessage(nint window, int message, nint wParam, nint lParam);
}

internal sealed class NativeListHost : UserControl
{
    private readonly GameListView list;
    private readonly SlimScrollBar scroll = new() { Dock=DockStyle.Right, Name="gameScroll", AccessibleName="Défilement des jeux" };
    private readonly GameHeader header;
    private readonly Panel viewport=new() {Dock=DockStyle.Fill,Margin=Padding.Empty,Name="gameViewport"};
    private bool updating;
    internal NativeListHost(GameListView list)
    {
        this.list=list; Dock=DockStyle.Fill; Margin=Padding.Empty; TabStop=false;
        list.HeaderStyle=ColumnHeaderStyle.None; list.Dock=DockStyle.None;
        header=new GameHeader(list) { Dock=DockStyle.Top, Height=32, Name="gameHeader" };
        var body=new Panel { Dock=DockStyle.Fill, Margin=Padding.Empty }; viewport.Controls.Add(list); body.Controls.Add(viewport); body.Controls.Add(scroll);
        viewport.SizeChanged+=(_,_)=>{LayoutNativeList();UpdateScroll();};
        Controls.Add(body); Controls.Add(header);
        scroll.ValueChanged += () =>
        {
            if (updating || !list.IsHandleCreated || list.Items.Count==0) return;
            var top=(int)NativeScrolling.SendMessage(list.Handle,0x1027,0,0);
            var row=Math.Max(1,list.Items[0].Bounds.Height);
            NativeScrolling.SendMessage(list.Handle,0x1014,0,(scroll.Value-top)*row);
            list.QueueViewportUpdate();
        };
        list.ViewportChanged += UpdateScroll; list.SizeChanged += (_,_)=>UpdateScroll();
    }
    internal void UpdateScroll()
    {
        if(updating || IsDisposed || Disposing || !list.IsHandleCreated) return;
        updating=true;
        try
        {
            scroll.Width=16*DeviceDpi/96; header.Height=32*DeviceDpi/96;
            LayoutNativeList();
            var page=Math.Max(1,(int)NativeScrolling.SendMessage(list.Handle,0x1028,0,0));
            scroll.Visible=list.Items.Count>page;
            scroll.SetRange(list.Items.Count,page); scroll.Value=Math.Max(0,(int)NativeScrolling.SendMessage(list.Handle,0x1027,0,0));
            header.Invalidate();
        }
        finally { updating=false; }
    }
    private void LayoutNativeList()
    {
        // The native control keeps its scrolling engine; its non-client rail lies outside the clipped viewport.
        var bounds=new Rectangle(0,0,Math.Max(0,viewport.ClientSize.Width)+NativeScrolling.ScrollbarWidth(DeviceDpi),Math.Max(0,viewport.ClientSize.Height));
        if(list.Bounds!=bounds)list.Bounds=bounds;
    }
    protected override void Dispose(bool disposing) { if(disposing) list.ViewportChanged-=UpdateScroll; base.Dispose(disposing); }
}

/// <summary>A real themed header, including the trailing area formerly painted by Windows.</summary>
internal sealed class GameHeader(GameListView list) : Control
{
    private int resizing=-1, startX, startWidth, startNextWidth;
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(UiTheme.Surface); using var font=UiTheme.Font(9); var x=0; var scale=DeviceDpi/96f;
        for(var i=0;i<list.Columns.Count;i++)
        {
            var column=list.Columns[i]; var inset=(int)((i==0?36:8)*scale);
            var rect=new Rectangle(x+inset,0,Math.Max(0,column.Width-inset-8),Height);
            TextRenderer.DrawText(e.Graphics,column.Text,font,rect,UiTheme.Muted,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
            x+=column.Width;
        }
        using var line=new Pen(UiTheme.Line); e.Graphics.DrawLine(line,0,Height-1,Width,Height-1);
    }
    private int Divider(int position)
    {
        var x=0; for(var i=0;i<list.Columns.Count-1;i++) { x+=list.Columns[i].Width; if(Math.Abs(x-position)<=5*DeviceDpi/96) return i; } return -1;
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if(resizing>=0)
        {
            var minimum=64*DeviceDpi/96; var width=Math.Clamp(startWidth+e.X-startX,minimum,startWidth+startNextWidth-minimum);
            list.Columns[resizing].Width=width;list.Columns[resizing+1].Width=startWidth+startNextWidth-width;
            Invalidate(); list.Invalidate(); list.QueueViewportUpdate();
        }
        else Cursor=Divider(e.X)>=0?Cursors.VSplit:Cursors.Default;
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e); if(e.Button!=MouseButtons.Left) return;
        resizing=Divider(e.X); if(resizing<0)return; startX=e.X; startWidth=list.Columns[resizing].Width;startNextWidth=list.Columns[resizing+1].Width; Capture=true;
    }
    protected override void OnMouseUp(MouseEventArgs e) { resizing=-1; Capture=false; base.OnMouseUp(e); }
    protected override void OnMouseCaptureChanged(EventArgs e) { if(!Capture)resizing=-1; base.OnMouseCaptureChanged(e); }
    protected override AccessibleObject CreateAccessibilityInstance() => new HeaderAccessible(this,list);
    private sealed class HeaderAccessible(GameHeader owner, GameListView list) : ControlAccessibleObject(owner)
    {
        public override AccessibleRole Role=>AccessibleRole.Row;
        public override int GetChildCount()=>list.Columns.Count;
        public override AccessibleObject? GetChild(int index)=>index>=0&&index<list.Columns.Count?new ColumnAccessible(owner,list,index):null;
    }
    private sealed class ColumnAccessible(GameHeader owner, GameListView list,int index) : AccessibleObject
    {
        public override AccessibleRole Role=>AccessibleRole.ColumnHeader;
        public override string? Name { get=>list.Columns[index].Text;set{} }
        public override Rectangle Bounds=>owner.RectangleToScreen(new Rectangle(list.Columns.Cast<ColumnHeader>().Take(index).Sum(c=>c.Width),0,list.Columns[index].Width,owner.Height));
    }
}
