using ResolutionTray.Core;

namespace ResolutionTray.UI;

internal sealed class GameExecutableForm : Form
{
    private readonly List<string> paths = [];
    private readonly CheckedListBox choices = new() { Dock = DockStyle.Fill, CheckOnClick = true, BorderStyle = BorderStyle.FixedSingle, IntegralHeight = false };
    private readonly Button apply = UiTheme.Button("Valider", 112, true);
    private readonly string directory;
    internal string[] Executables => choices.CheckedIndices.Cast<int>().Select(i => paths[i]).ToArray();
    internal string[] Candidates => paths.ToArray();

    internal GameExecutableForm(GameEntry game)
    {
        directory = game.InstallDirectory;
        Text = $"Exécutables — {game.Name}";
        Font = new Font("Segoe UI", 10);
        ForeColor = UiTheme.Ink;
        BackColor = UiTheme.Background;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(740, 400);
        MinimumSize = new Size(620, 350);
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false; MaximizeBox = false;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 3 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        layout.Controls.Add(UiTheme.Label("Choisissez le fichier du jeu utilisé pendant une partie.\nVous pouvez cocher plusieurs variantes du même jeu."));
        layout.Controls.Add(choices);
        var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 14, 0, 0), WrapContents = false };
        var browse = UiTheme.Button("Parcourir…", 130);
        var cancel = UiTheme.Button("Annuler", 104);
        cancel.DialogResult = DialogResult.Cancel;
        footer.Controls.Add(apply); footer.Controls.Add(cancel); footer.Controls.Add(browse);
        layout.Controls.Add(footer);
        Controls.Add(layout);
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
