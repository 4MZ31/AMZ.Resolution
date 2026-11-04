namespace AMZ.Resolution.UI;

internal sealed class SearchTextBox : TextBox
{
    protected override bool ProcessCmdKey(ref Message message, Keys keyData)
    {
        if (keyData == Keys.Escape && TextLength > 0) { Clear(); return true; }
        return base.ProcessCmdKey(ref message, keyData);
    }
}

internal sealed class SearchField : FieldFrame
{
    private readonly ActionButton clear = new() { Glyph = UiGlyph.Close, Quiet = true, Text = "", TabStop = true, Name = "clearSearch", AccessibleName = "Effacer la recherche" };
    private readonly TextBox input;
    internal SearchField(TextBox input)
    {
        this.input = input; Name = "searchField"; Padding = new Padding(40, 10, 40, 8);
        Controls.Add(clear); clear.Click += (_, _) => { input.Clear(); input.Focus(); };
        input.TextChanged += (_, _) => { clear.Visible = input.TextLength > 0; Invalidate(); };
        clear.Visible = input.TextLength > 0;
    }
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e); var s=DeviceDpi/96f;
        clear.SetBounds(Width-(int)(36*s), (Height-(int)(32*s))/2, (int)(32*s), (int)(32*s));
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); var size=18*DeviceDpi/96;
        Glyphs.Draw(e.Graphics, UiGlyph.Search, new Rectangle(12*DeviceDpi/96,(Height-size)/2,size,size), UiTheme.Muted);
    }
}
