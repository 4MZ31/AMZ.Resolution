using ResolutionTray.Core;

namespace ResolutionTray.UI;

internal sealed class GameExecutableForm : Form
{
    private readonly List<string> paths = [];
    private readonly CheckedListBox choices = new() { Dock = DockStyle.Fill, CheckOnClick = true, BorderStyle = BorderStyle.FixedSingle, IntegralHeight = false };
    private readonly Button apply = UiTheme.Button("Utiliser ces fichiers", 184, true);
    private readonly string directory;
    internal string[] Executables => choices.CheckedIndices.Cast<int>().Select(i => paths[i]).ToArray();
    internal string[] Candidates => paths.ToArray();

    internal GameExecutableForm(GameEntry game)
    {
        directory = game.InstallDirectory;
        Text = $"ResolutionTray — {game.Name}";
        UiTheme.Apply(this);
        ClientSize = new Size(800, 512); MinimumSize = new Size(680, 440);
        StartPosition = FormStartPosition.CenterParent; MinimizeBox = false; MaximizeBox = false; ShowInTaskbar = false;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 3, Margin = Padding.Empty };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 88)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        var header = UiTheme.Stack(40, 32); var title = UiTheme.Label(game.Name, 20); title.ForeColor = UiTheme.Ink;
        header.Controls.Add(title); header.Controls.Add(UiTheme.Label("Cochez les fichiers utilisés pendant une partie")); layout.Controls.Add(header);
        var surface = new SurfacePanel { Dock = DockStyle.Fill, Padding = new Padding(16) };
        choices.Name = "executableChoices"; choices.AccessibleName = "Exécutables du jeu à surveiller";
        choices.Font = UiTheme.Font(); choices.BackColor = UiTheme.Surface; choices.ForeColor = UiTheme.Ink; choices.BorderStyle = BorderStyle.None;
        choices.HorizontalScrollbar = true; surface.Controls.Add(choices); layout.Controls.Add(surface);
        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(0, 20, 0, 0), Margin = Padding.Empty };
        footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var browse = UiTheme.Button("Parcourir…", 136); browse.Name = "browseExecutable"; footer.Controls.Add(browse);
        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Margin = Padding.Empty };
        var cancel = new ActionButton { Text = "Annuler", Width = 96, Height = 40, Quiet = true, DialogResult = DialogResult.Cancel };
        apply.Name = "applyExecutables"; buttons.Controls.Add(apply); buttons.Controls.Add(cancel); footer.Controls.Add(buttons); layout.Controls.Add(footer); Controls.Add(layout);
        foreach (var executable in game.Executables.Concat(game.Candidates).Distinct(StringComparer.OrdinalIgnoreCase))
            AddPath(executable, game.Executables.Contains(executable, StringComparer.OrdinalIgnoreCase));
        apply.Enabled = choices.CheckedItems.Count > 0;
        choices.ItemCheck += (_, e) => apply.Enabled = choices.CheckedItems.Count
            + (e.NewValue == CheckState.Checked ? 1 : 0) - (e.CurrentValue == CheckState.Checked ? 1 : 0) > 0;
        browse.Click += (_, _) =>
        {
            using var picker = new OpenFileDialog { Filter = "Jeux Windows (*.exe)|*.exe", Multiselect = true, CheckFileExists = true, Title = "Choisir l’exécutable du jeu" };
            if (Directory.Exists(directory)) picker.InitialDirectory = directory;
            if (picker.ShowDialog(this) != DialogResult.OK) return;
            foreach (var file in picker.FileNames) AddPath(file, true);
        };
        apply.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };
        CancelButton = cancel; AcceptButton = apply;
    }
    private void AddPath(string executable, bool selected)
    {
        var existing = paths.FindIndex(p => p.Equals(executable, StringComparison.OrdinalIgnoreCase));
        if (existing >= 0) { if (selected) choices.SetItemChecked(existing, true); return; }
        paths.Add(executable);
        var label = executable;
        if (Path.IsPathFullyQualified(executable) && Directory.Exists(directory))
        {
            var relative = Path.GetRelativePath(directory, executable);
            if (!relative.StartsWith("..", StringComparison.Ordinal)) label = relative;
        }
        choices.Items.Add(label, selected);
    }
}
