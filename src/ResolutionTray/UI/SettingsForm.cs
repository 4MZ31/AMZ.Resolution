using Microsoft.Win32;
using ResolutionTray.Core;
using ResolutionTray.Services;

namespace ResolutionTray.UI;

internal sealed partial class SettingsForm : Form
{
    private readonly IReadOnlyList<DisplayTarget> targets;
    private readonly List<GameEntry> games;
    private readonly ComboBox displays = new ThemedComboBox() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Top };
    private readonly CheckBox startup = new ToggleSwitch { Name = "startup", AccessibleName = "Démarrage avec Windows" };
    private readonly CheckBox automaticUpdates = new ToggleSwitch { Name = "automaticUpdates", AccessibleName = "Mises à jour automatiques" };
    private readonly GameListView library = new() { Dock = DockStyle.Fill, View = View.Details, CheckBoxes = true,
        FullRowSelect = true, MultiSelect = false, HideSelection = false, BorderStyle = BorderStyle.None, ShowItemToolTips = true };
    private readonly TextBox search = new SearchTextBox() { Dock = DockStyle.Fill, PlaceholderText = "Rechercher un jeu…", BorderStyle = BorderStyle.FixedSingle };
    private readonly ComboBox source = new ThemedComboBox() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly Label summary = UiTheme.Label("");
    private readonly Label detail = UiTheme.Label("Sélectionnez un jeu pour consulter son exécutable.");
    private readonly Label displayInfo = UiTheme.Label("");
    private readonly Label footerInfo = UiTheme.Label("Vos choix sont enregistrés sur ce PC.", 9);
    private readonly Label empty = UiTheme.Label("Aucun jeu trouvé. Lancez un scan ou ajoutez un jeu.");
    private readonly Button scan = UiTheme.Button("Scanner les jeux", 164);
    private readonly Button manual = UiTheme.Button("Ajouter un jeu…", 144);
    private readonly Button configure = UiTheme.Button("Exécutables…", 136);
    private readonly Button save = UiTheme.Button("Enregistrer", 116);
    private readonly Button activate = UiTheme.Button("Enregistrer et activer", 204, true);
    private readonly ProgressBar progress = new() { Dock = DockStyle.Bottom, Height = 3, Style = ProgressBarStyle.Marquee, Visible = false };
    private CancellationTokenSource? scanCancellation;
    private bool rendering;
    private readonly bool saveChanges;
    private string scanNote = "Steam · Epic Games · Ajout manuel";
    public AppSettings Result { get; private set; }
    public bool ActivateRequested { get; private set; }

    internal SettingsForm(AppSettings settings, IReadOnlyList<DisplayTarget> allDisplays, bool saveChanges = true,
        Func<Profile, Func<bool>, Task<bool>>? testProfile = null, bool monitoringEnabled = false)
    {
        Result = settings;
        UiTheme.SetDarkMode(settings.DarkMode);
        this.saveChanges = saveChanges;
        this.testProfile = testProfile;
        games = settings.GameList.ToList();
        targets = allDisplays.Where(d => d.NativeWidth == 2560 && d.NativeHeight == 1440).ToArray();
        Text = "ResolutionTray — Paramètres";
        UiTheme.Apply(this);
        ClientSize = new Size(1200, 800);
        MinimumSize = new Size(1040, 680);
        StartPosition = FormStartPosition.CenterScreen;
        BuildShell(monitoringEnabled);
        startup.Checked = StartupRegistration.IsEnabled;
        automaticUpdates.Checked = settings.AutoUpdate;
        darkMode.Checked = settings.DarkMode;
        InitializeDisplay(settings);
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
        FormClosing += (_, e) => { if (testingDisplay) e.Cancel = true; else scanCancellation?.Cancel(); };
        UiTheme.Recolor(this);
        RenderLibrary();
        baselineDraft = DraftSignature();
        startup.CheckedChanged += (_, _) => UpdateButtons();
        automaticUpdates.CheckedChanged += (_, _) => UpdateButtons();
        darkMode.CheckedChanged += (_, _) => { UiTheme.SetDarkMode(darkMode.Checked); UpdateButtons(); };
        UiTheme.ThemeChanged += RefreshTheme;
        FormClosed += (_, _) => { UiTheme.ThemeChanged -= RefreshTheme; if (DialogResult != DialogResult.OK || !saveChanges) UiTheme.SetDarkMode(settings.DarkMode); };
        UpdateButtons();
        if (settings.Games is null) Shown += async (_, _) => await ScanAsync();
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
                if (!game.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) || selectedOnly && !game.Enabled) continue;
                if (source.SelectedIndex > 0 && game.Source != source.SelectedItem?.ToString()) continue;
                var item = new ListViewItem(game.Name) { Tag = game.Id, Checked = game.Enabled, UseItemStyleForSubItems = false,
                    ToolTipText = game.InstallDirectory };
                item.SubItems.Add(game.Source);
                item.SubItems.Add(game.Ready ? "Prêt" : "À configurer").ForeColor = game.Ready ? UiTheme.Secondary : UiTheme.Warning;
                library.Items.Add(item);
                if (game.Id == selectedId) item.Selected = true;
            }
            empty.Visible = library.Items.Count == 0;
            empty.Text = games.Count == 0 ? "Votre bibliothèque commence ici\nScannez Steam et Epic Games, ou ajoutez un exécutable"
                : selectedOnly && games.All(g => !g.Enabled) ? "Aucun jeu sélectionné\nRevenez à Tous les jeux pour faire votre choix" : "Aucun résultat\nModifiez la recherche ou le filtre de source";
            if (empty.Visible) empty.BringToFront();
        }
        finally { library.EndUpdate(); rendering = false; }
        ResizeColumns(); UpdateDetail(); UpdateButtons();
    }
    private void ResizeColumns()
    {
        if (library.Columns.Count < 3) return;
        var scale = DeviceDpi / 96f;
        library.Columns[1].Width = (int)(112 * scale);
        library.Columns[2].Width = (int)(144 * scale);
        library.Columns[0].Width = Math.Max((int)(180 * scale), (library.Parent?.ClientSize.Width ?? library.ClientSize.Width) - library.Columns[1].Width - library.Columns[2].Width);
    }
    private GameEntry? SelectedGame() => library.SelectedItems.Count == 1
        ? games.FirstOrDefault(g => g.Id == library.SelectedItems[0].Tag?.ToString()) : null;
    private void UpdateDetail()
    {
        var game = SelectedGame();
        configure.Enabled = game is not null && scanCancellation is null;
        configure.Text = game?.Ready == true ? "Configurer…" : "Choisir le fichier…";
        detail.Text = game is null ? "Sélectionnez un jeu pour vérifier son exécutable"
            : game.Ready ? $"{game.Name}\n{game.Executables.Length} {(game.Executables.Length == 1 ? "exécutable surveillé" : "exécutables surveillés")}" : $"{game.Name}\nChoisissez le fichier utilisé pendant une partie";
    }
    private void CheckGame(object? sender, ItemCheckEventArgs e)
    {
        if (rendering) return;
        var id = library.Items[e.Index].Tag?.ToString();
        var index = games.FindIndex(g => g.Id == id);
        if (index >= 0) games[index] = games[index] with { Enabled = e.NewValue == CheckState.Checked };
        UpdateButtons();
        if (selectedOnly && IsHandleCreated) BeginInvoke((Action)(() => { if (!IsDisposed && !Disposing) RenderLibrary(); }));
    }
    private void UpdateButtons()
    {
        if (updatingDisplay) return;
        var selected = games.Count(g => g.Enabled);
        var pending = games.Any(g => g.Enabled && !g.Ready);
        var scanning = scanCancellation is not null;
        save.Enabled = !scanning && !pending && !testingDisplay && CanSaveDisplay();
        activate.Enabled = save.Enabled && selected > 0;
        manual.Enabled = !scanning;
        if (!scanning) summary.Text = $"{selected} {(selected == 1 ? "jeu sélectionné" : "jeux sélectionnés")} sur {games.Count} · {scanNote}";
        selectionFilter.Text = $"Sélectionnés ({selected})";
        UpdateDisplayInfo();
        UpdateFooter(pending, selected);
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
                scan.Text = "Scanner les jeux"; progress.Visible = false; RenderLibrary();
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
    protected override void Dispose(bool disposing)
    {
        if (disposing) UiTheme.ThemeChanged -= RefreshTheme;
        base.Dispose(disposing);
    }
    private void Save(bool shouldActivate)
    {
        if (!CanSaveDisplay()) return;
        try
        {
            var result = CreateDraft();
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
