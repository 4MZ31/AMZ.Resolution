using Microsoft.Win32;
using ResolutionTray.Core;
using ResolutionTray.Services;

namespace ResolutionTray.UI;

internal sealed class SettingsForm : Form
{
    private readonly IReadOnlyList<DisplayTarget> targets;
    private readonly List<GameEntry> games;
    private readonly ComboBox displays = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Top };
    private readonly CheckBox startup = new() { Text = "Lancer avec Windows", AutoSize = true };
    private readonly GameListView library = new() { Dock = DockStyle.Fill, View = View.Details, CheckBoxes = true,
        FullRowSelect = true, MultiSelect = false, HideSelection = false, BorderStyle = BorderStyle.None, ShowItemToolTips = true };
    private readonly TextBox search = new() { Dock = DockStyle.Fill, PlaceholderText = "Rechercher un jeu…", BorderStyle = BorderStyle.FixedSingle };
    private readonly ComboBox source = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly Label summary = UiTheme.Label("");
    private readonly Label detail = UiTheme.Label("Sélectionnez un jeu pour consulter son exécutable.");
    private readonly Label displayInfo = UiTheme.Label("");
    private readonly Label footerInfo = UiTheme.Label("Vos choix sont enregistrés sur ce PC.", 9);
    private readonly Label empty = UiTheme.Label("Aucun jeu trouvé. Lancez un scan ou ajoutez un jeu.");
    private readonly Button scan = UiTheme.Button("Scanner mes jeux", 176, true);
    private readonly Button manual = UiTheme.Button("Ajouter un jeu…", 144);
    private readonly Button configure = UiTheme.Button("Exécutables…", 136);
    private readonly Button save = UiTheme.Button("Enregistrer", 116);
    private readonly Button activate = UiTheme.Button("Enregistrer et surveiller", 212, true);
    private readonly ProgressBar progress = new() { Dock = DockStyle.Bottom, Height = 3, Style = ProgressBarStyle.Marquee, Visible = false };
    private CancellationTokenSource? scanCancellation;
    private bool rendering;
    private readonly bool saveChanges;
    private string scanNote = "Steam · Epic Games · Ajout manuel";
    public AppSettings Result { get; private set; }
    public bool ActivateRequested { get; private set; }

    internal SettingsForm(AppSettings settings, IReadOnlyList<DisplayTarget> allDisplays, bool saveChanges = true)
    {
        Result = settings;
        this.saveChanges = saveChanges;
        games = settings.GameList.ToList();
        targets = allDisplays.Where(d => d.NativeWidth == 2560 && d.NativeHeight == 1440).ToArray();
        Text = "AMD Resolution Tray — Vos jeux";
        Font = new Font("Segoe UI", 10);
        ForeColor = UiTheme.Ink;
        BackColor = UiTheme.Background;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(1140, 720);
        MinimumSize = new Size(1020, 650);
        StartPosition = FormStartPosition.CenterScreen;
        var outer = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 3 };
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 94));
        outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
        header.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        header.Controls.Add(UiTheme.Label("Vos jeux, votre affichage", 22, true));
        header.Controls.Add(UiTheme.Label("Cochez les jeux qui utiliseront votre résolution personnalisée."));
        outer.Controls.Add(header);
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 66));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        body.Controls.Add(BuildLibrary(), 0, 0);
        body.Controls.Add(BuildDisplay(), 1, 0);
        outer.Controls.Add(body);
        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(0, 18, 0, 0), Margin = Padding.Empty };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.Controls.Add(footerInfo);
        var actions = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        var cancel = UiTheme.Button("Annuler", 104);
        cancel.DialogResult = DialogResult.Cancel;
        actions.Controls.Add(activate); actions.Controls.Add(save); actions.Controls.Add(cancel);
        footer.Controls.Add(actions);
        outer.Controls.Add(footer);
        Controls.Add(outer);
        CancelButton = cancel;
        startup.Checked = StartupRegistration.IsEnabled;
        foreach (var display in targets) displays.Items.Add($"{display.Name} · {display.CurrentMode.RefreshRate} Hz");
        var selected = targets.ToList().FindIndex(d => d.Identity == settings.DisplayIdentity);
        displays.SelectedIndex = selected >= 0 ? selected : targets.Count == 1 ? 0 : -1;
        displays.SelectedIndexChanged += (_, _) => UpdateButtons();
        search.TextChanged += (_, _) => RenderLibrary();
        source.SelectedIndexChanged += (_, _) => RenderLibrary();
        library.ItemCheck += CheckGame;
        library.SelectedIndexChanged += (_, _) => UpdateDetail();
        library.DoubleClick += (_, _) => ConfigureSelected();
        library.Resize += (_, _) => ResizeColumns();
        configure.Click += (_, _) => ConfigureSelected();
        manual.Click += (_, _) => AddManual();
        scan.Click += async (_, _) => { if (scanCancellation is not null) scanCancellation.Cancel(); else await ScanAsync(); };
        save.Click += (_, _) => Save(false);
        activate.Click += (_, _) => Save(true);
        FormClosing += (_, _) => scanCancellation?.Cancel();
        RenderLibrary();
        UpdateButtons();
        if (settings.Games is null) Shown += async (_, _) => await ScanAsync();
    }

    private Control BuildLibrary()
    {
        var card = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(20),
            ColumnCount = 1, RowCount = 6, Margin = new Padding(0, 0, 16, 0) };
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        card.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
        card.Controls.Add(UiTheme.Label("Bibliothèque de jeux", 14, true));
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
        toolbar.Controls.Add(scan); toolbar.Controls.Add(manual);
        card.Controls.Add(toolbar);
        var filters = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 148));
        search.Margin = new Padding(0, 0, 12, 0);
        filters.Controls.Add(search);
        source.Items.AddRange(["Toutes les sources", "Steam", "Epic Games", "Manuel"]);
        source.SelectedIndex = 0;
        filters.Controls.Add(source);
        card.Controls.Add(filters);
        var scanStatus = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
        scanStatus.Controls.Add(summary); scanStatus.Controls.Add(progress);
        card.Controls.Add(scanStatus);
        library.Columns.Add("Jeu", 290);
        library.Columns.Add("Source", 94);
        library.Columns.Add("État", 162);
        library.AccessibleName = "Jeux installés à surveiller";
        var listArea = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty, BackColor = Color.White };
        empty.TextAlign = ContentAlignment.MiddleCenter;
        listArea.Controls.Add(library); listArea.Controls.Add(empty);
        card.Controls.Add(listArea);
        var info = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, Padding = new Padding(0, 12, 0, 0) };
        info.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        info.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 144));
        detail.Font = new Font("Segoe UI", 9);
        info.Controls.Add(detail);
        info.Controls.Add(configure);
        card.Controls.Add(info);
        return card;
    }

    private Control BuildDisplay()
    {
        var card = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(20),
            ColumnCount = 1, RowCount = 8, Margin = Padding.Empty };
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 154));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        card.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        card.Controls.Add(UiTheme.Label("Votre zone de jeu", 14, true));
        card.Controls.Add(displays);
        card.Controls.Add(displayInfo);
        card.Controls.Add(new DisplayPreview());
        card.Controls.Add(UiTheme.Label("Centré, sans agrandissement.\nSur un écran 27″ : environ 24,5″.", 9));
        card.Controls.Add(UiTheme.Label("Activé avec vos jeux cochés.\nBureau restauré à leur fermeture.", 10));
        card.Controls.Add(startup);
        card.Controls.Add(UiTheme.Label("Vert : en jeu · Gris : attente ou arrêt", 9));
        return card;
    }

    private void RenderLibrary()
    {
        var selectedId = SelectedGame()?.Id;
        rendering = true;
        library.BeginUpdate();
        try
        {
            library.Items.Clear();
            var query = search.Text.Trim();
            foreach (var game in games.OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                if (!game.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)) continue;
                if (source.SelectedIndex > 0 && game.Source != source.SelectedItem?.ToString()) continue;
                var item = new ListViewItem(game.Name) { Tag = game.Id, Checked = game.Enabled, UseItemStyleForSubItems = false,
                    ToolTipText = game.InstallDirectory };
                item.SubItems.Add(game.Source);
                item.SubItems.Add(game.Ready ? "Prêt" : "Exécutable à choisir").ForeColor = game.Ready ? UiTheme.Green : Color.FromArgb(161, 104, 27);
                library.Items.Add(item);
                if (game.Id == selectedId) item.Selected = true;
            }
            empty.Visible = library.Items.Count == 0;
            empty.Text = games.Count == 0 ? "Lancez un scan ou ajoutez votre premier jeu." : "Aucun jeu ne correspond à votre recherche.";
            if (empty.Visible) empty.BringToFront();
        }
        finally { library.EndUpdate(); rendering = false; }
        ResizeColumns(); UpdateDetail(); UpdateButtons();
    }
    private void ResizeColumns()
    {
        if (library.Columns.Count < 3) return;
        library.Columns[0].Width = Math.Max(160, library.ClientSize.Width - 94 - 162 - 24);
        library.Columns[1].Width = 94;
        library.Columns[2].Width = 162;
    }
    private GameEntry? SelectedGame() => library.SelectedItems.Count == 1
        ? games.FirstOrDefault(g => g.Id == library.SelectedItems[0].Tag?.ToString()) : null;
    private void UpdateDetail()
    {
        var game = SelectedGame();
        configure.Enabled = game is not null && scanCancellation is null;
        detail.Text = game is null ? "Sélectionnez un jeu pour consulter son exécutable."
            : game.Ready ? $"{game.Name}\n{game.Executables.Length} fichier(s) surveillé(s)" : $"{game.Name}\nChoisissez son exécutable pour l’activer.";
    }
    private void CheckGame(object? sender, ItemCheckEventArgs e)
    {
        if (rendering) return;
        var id = library.Items[e.Index].Tag?.ToString();
        var index = games.FindIndex(g => g.Id == id);
        if (index >= 0) games[index] = games[index] with { Enabled = e.NewValue == CheckState.Checked };
        UpdateButtons();
    }
    private void UpdateButtons()
    {
        var selected = games.Count(g => g.Enabled);
        var pending = games.Any(g => g.Enabled && !g.Ready);
        var scanning = scanCancellation is not null;
        save.Enabled = !scanning && !pending && displays.SelectedIndex >= 0;
        activate.Enabled = save.Enabled && selected > 0;
        manual.Enabled = !scanning;
        if (!scanning) summary.Text = $"{selected} sélectionné(s) · {games.Count} jeux · {scanNote}";
        displayInfo.Text = displays.SelectedIndex >= 0
            ? $"2324 × 1306 · {targets[displays.SelectedIndex].CurrentMode.RefreshRate} Hz\nÉcran natif 2560 × 1440"
            : "Sélectionnez un écran AMD 2560 × 1440.\nUtilisez le mode Étendre de Windows.";
        footerInfo.Text = pending ? "Choisissez l’exécutable des jeux cochés à configurer."
            : selected == 0 ? "Cochez au moins un jeu pour lancer la surveillance." : "Vos choix sont enregistrés sur ce PC.";
    }
    private async Task ScanAsync()
    {
        using var cancellation = new CancellationTokenSource();
        scanCancellation = cancellation;
        scan.Text = "Annuler le scan";
        progress.Visible = true;
        UpdateButtons(); UpdateDetail();
        try
        {
            var updates = new Progress<GameScanProgress>(p =>
            {
                if (!IsDisposed && !Disposing && scanCancellation is not null) summary.Text = $"Analyse… {p.Count} jeux · {p.Message}";
            });
            var result = await new InstalledGameScanner().ScanAsync(updates, cancellation.Token);
            if (IsDisposed || Disposing) return;
            var merged = GameCatalog.Merge(games, result.Games);
            games.Clear(); games.AddRange(merged);
            scanNote = result.Warnings == 0 ? "scan terminé" : "scan partiel, ajout manuel disponible";
        }
        catch (OperationCanceledException) { scanNote = "scan annulé"; }
        catch (Exception ex) { AppStorage.Log(ex); scanNote = "scan indisponible, réessayez"; }
        finally
        {
            scanCancellation = null;
            if (!IsDisposed && !Disposing)
            {
                scan.Text = "Scanner mes jeux"; progress.Visible = false; RenderLibrary();
            }
        }
    }
    private void AddManual()
    {
        using var picker = new OpenFileDialog { Filter = "Jeux Windows (*.exe)|*.exe", CheckFileExists = true, Title = "Ajouter un jeu" };
        if (picker.ShowDialog(this) != DialogResult.OK) return;
        var executable = Path.GetFullPath(picker.FileName);
        var existing = games.FindIndex(g => g.Executables.Concat(g.Candidates).Contains(executable, StringComparer.OrdinalIgnoreCase));
        if (existing >= 0) games[existing] = games[existing] with { Enabled = true, Executables = [executable] };
        else games.Add(new("manual:" + executable.ToUpperInvariant(), Path.GetFileNameWithoutExtension(executable), "Manuel",
            Path.GetDirectoryName(executable)!, [executable], [executable], true));
        RenderLibrary();
    }
    private void ConfigureSelected()
    {
        if (SelectedGame() is not { } game || scanCancellation is not null) return;
        using var dialog = new GameExecutableForm(game);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var index = games.FindIndex(g => g.Id == game.Id);
        games[index] = game with { Executables = dialog.Executables, Candidates = dialog.Candidates };
        RenderLibrary();
    }
    private void Save(bool shouldActivate)
    {
        try
        {
            var result = Result with { DisplayIdentity = targets[displays.SelectedIndex].Identity,
                StartWithWindows = startup.Checked, Games = games.ToArray() };
            if (saveChanges)
            {
                StartupRegistration.Set(startup.Checked);
                SettingsStore.Save(result);
            }
            Result = result; ActivateRequested = shouldActivate;
            DialogResult = DialogResult.OK; Close();
        }
        catch (Exception ex) { Program.ShowError(ex); }
    }
}

internal static class StartupRegistration
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "AmdResolutionTray";
    internal static bool IsEnabled
    {
        get { using var key = Registry.CurrentUser.OpenSubKey(KeyPath); return key?.GetValue(ValueName) is string; }
    }
    internal static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
        if (!enabled) { key.DeleteValue(ValueName, false); return; }
        var command = SafetyMonitor.Command();
        var quoted = '"' + command.FileName + '"';
        foreach (var arg in command.ArgumentList) quoted += " \"" + arg + "\"";
        key.SetValue(ValueName, quoted);
    }
}
