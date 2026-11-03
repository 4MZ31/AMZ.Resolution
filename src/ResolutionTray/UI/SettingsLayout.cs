using System.Text.Json;
using ResolutionTray.Services;

namespace ResolutionTray.UI;

internal sealed partial class SettingsForm
{
    private readonly CheckBox darkMode = new ToggleSwitch { Name = "darkMode", AccessibleName = "Mode sombre (désactivé : mode clair)" };
    private readonly Panel pageHost = new() { Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = new Padding(28, 24, 28, 16) };
    private readonly List<Control> pages = [];
    private readonly List<ActionButton> navigation = [];
    private readonly ActionButton allFilter = new() { Text = "Tous les jeux", Width = 120, Height = 36, Quiet = true, Selected = true, Name = "allGames" };
    private readonly ActionButton selectionFilter = new() { Text = "Sélectionnés", Width = 160, Height = 36, Quiet = true, Name = "selectedGames" };
    private readonly Label profileSummary = UiTheme.Label("", 11, true);
    private readonly LinkLabel fixSelection = new() { Text = "Voir", AutoSize = true, LinkColor = UiTheme.Green, ActiveLinkColor = UiTheme.Green,
        VisitedLinkColor = UiTheme.Green, LinkBehavior = LinkBehavior.HoverUnderline, Margin = new Padding(8, 0, 0, 0), TabStop = true };
    private bool selectedOnly;
    private string? baselineDraft;

    private void BuildShell(bool monitoringEnabled)
    {
        var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240)); shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var side = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = UiTheme.Sidebar, Name = "appSidebar", Padding = new Padding(20, 24, 20, 20), ColumnCount = 1, RowCount = 6, Margin = Padding.Empty };
        side.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var height in new[] { 80, 40, 40, 40 }) side.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        side.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); side.RowStyles.Add(new RowStyle(SizeType.Absolute, 140));
        var brand = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Margin = Padding.Empty };
        brand.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 32)); brand.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        brand.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); brand.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        var mark = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 8, 0), AccessibleName = "Repères de cadrage ResolutionTray" };
        mark.Paint += (_, e) => Glyphs.Draw(e.Graphics, UiGlyph.Frame, new Rectangle(1, 4, 22, 22), UiTheme.Ink);
        brand.Controls.Add(mark, 0, 0); brand.Controls.Add(UiTheme.Label("ResolutionTray", 13, true), 1, 0);
        brand.Controls.Add(UiTheme.Label("AMD Radeon", 9), 1, 1); side.Controls.Add(brand, 0, 0);
        var labels = new[] { "Jeux", "Affichage", "Application" }; var glyphs = new[] { UiGlyph.Game, UiGlyph.Monitor, UiGlyph.Sliders };
        for (var index = 0; index < labels.Length; index++)
        {
            var page = index;
            var button = new ActionButton { Text = labels[index], Glyph = glyphs[index], Navigation = true, Quiet = true,
                Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 4), Name = "nav" + index, AccessibleName = labels[index] + $" (Ctrl+{index + 1})" };
            button.Click += (_, _) => ShowPage(page); navigation.Add(button); side.Controls.Add(button, 0, index + 1);
        }
        var state = UiTheme.Stack(24, 44, 32, 24);
        state.Controls.Add(UiTheme.Label(monitoringEnabled ? "En attente d’un jeu" : "Surveillance arrêtée", 10, true));
        state.Controls.Add(UiTheme.Label("Double-clic sur l’icône de notification pour activer ou arrêter", 9));
        var keys = UiTheme.Label("Ctrl 1–3   Naviguer", 9); keys.Font = UiTheme.Figures(9); state.Controls.Add(keys);
        state.Controls.Add(UiTheme.Label($"Version {GitHubUpdateClient.CurrentVersion.ToString(3)}", 9));
        side.Controls.Add(state, 0, 5); shell.Controls.Add(side, 0, 0);
        var main = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); main.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
        pages.Add(BuildLibrary()); pages.Add(BuildDisplay()); pages.Add(BuildApplication());
        foreach (var page in pages) { page.Dock = DockStyle.Fill; page.Visible = false; pageHost.Controls.Add(page); }
        main.Controls.Add(pageHost, 0, 0); main.Controls.Add(BuildFooter(), 0, 1); shell.Controls.Add(main, 1, 0);
        Controls.Add(shell); ShowPage(0);
        scan.Name = "scanGames"; manual.Name = "addGame"; configure.Name = "configureGame"; save.Name = "saveSettings"; activate.Name = "activateSettings";
        search.Name = "searchGames"; source.Name = "gameSource"; library.Name = "gameLibrary";
        if (scan is ActionButton scanButton) scanButton.Glyph = UiGlyph.Scan;
        if (manual is ActionButton addButton) addButton.Glyph = UiGlyph.Plus;
        allFilter.Click += (_, _) => { selectedOnly = false; UpdateFilters(); RenderLibrary(); };
        selectionFilter.Click += (_, _) => { selectedOnly = true; UpdateFilters(); RenderLibrary(); };
        KeyPreview = true;
        KeyDown += (_, e) =>
        {
            if (!e.Control) return;
            if (e.KeyCode >= Keys.D1 && e.KeyCode <= Keys.D3) { ShowPage((int)e.KeyCode - (int)Keys.D1); e.SuppressKeyPress = true; }
            if (e.KeyCode == Keys.F) { ShowPage(0); search.Focus(); e.SuppressKeyPress = true; }
            if (e.KeyCode == Keys.S && save.Enabled) { Save(false); e.SuppressKeyPress = true; }
        };
        library.KeyDown += (_, e) => { if (e.KeyCode == Keys.F2) { ConfigureSelected(); e.Handled = true; } };
    }

    private static Control PageHeader(string title, string description)
    {
        var header = UiTheme.Stack(40, 28);
        var heading = UiTheme.Label(title, 24); heading.ForeColor = UiTheme.Ink; heading.Tag = "ink";
        header.Controls.Add(heading); header.Controls.Add(UiTheme.Label(description));
        return header;
    }
    private void ShowPage(int index)
    {
        if (testingDisplay) return;
        for (var i = 0; i < pages.Count; i++) { pages[i].Visible = i == index; navigation[i].Selected = i == index; navigation[i].Invalidate(); }
        if (pages.Count > index) pages[index].BringToFront();
        if (IsHandleCreated) navigation[index].Focus();
    }
    private void UpdateFilters()
    {
        allFilter.Selected = !selectedOnly; selectionFilter.Selected = selectedOnly;
        allFilter.Invalidate(); selectionFilter.Invalidate();
    }

    private Control BuildLibrary()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 88)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 352));
        header.Controls.Add(PageHeader("Jeux", "Votre profil s’active avec les jeux cochés"));
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 8, 0, 0), Margin = Padding.Empty };
        manual.Width = 160; scan.Width = 164; actions.Controls.Add(manual); actions.Controls.Add(scan); header.Controls.Add(actions); layout.Controls.Add(header);
        var filters = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        filters.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 176));
        var searchFrame = UiTheme.Field(search, "searchGames"); searchFrame.Margin = new Padding(0, 0, 12, 12); search.PlaceholderText = "Rechercher un jeu";
        filters.Controls.Add(searchFrame); source.Items.AddRange(["Toutes les sources", "Steam", "Epic Games", "Manuel"]); source.SelectedIndex = 0;
        var sourceFrame = UiTheme.Field(source, "gameSource"); sourceFrame.Margin = new Padding(0, 0, 0, 12); filters.Controls.Add(sourceFrame); layout.Controls.Add(filters);
        var surface = new SurfacePanel { Dock = DockStyle.Fill, Padding = new Padding(16) };
        var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Margin = Padding.Empty };
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); content.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); content.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        var tabs = new FlowLayoutPanel { Dock = DockStyle.Fill, Margin = Padding.Empty, WrapContents = false }; tabs.Controls.Add(allFilter); tabs.Controls.Add(selectionFilter); content.Controls.Add(tabs);
        var statusArea = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty }; summary.Font = UiTheme.Font(9); statusArea.Controls.Add(summary); statusArea.Controls.Add(progress); content.Controls.Add(statusArea);
        library.Columns.Add("Jeu", 320); library.Columns.Add("Source", 112); library.Columns.Add("État", 144); library.AccessibleName = "Jeux installés à surveiller";
        var listArea = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty, BackColor = UiTheme.Surface };
        empty.TextAlign = ContentAlignment.MiddleCenter; empty.Font = UiTheme.Font(11); empty.BackColor = UiTheme.Surface;
        listArea.Controls.Add(library); listArea.Controls.Add(empty); content.Controls.Add(listArea);
        var selected = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(0, 16, 0, 0), Margin = Padding.Empty };
        selected.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        selected.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); selected.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 168));
        detail.Font = UiTheme.Font(9); configure.Width = 160; selected.Controls.Add(detail); selected.Controls.Add(configure); content.Controls.Add(selected);
        surface.Controls.Add(content); layout.Controls.Add(surface); return layout;
    }

    private Control BuildFooter()
    {
        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = UiTheme.Surface, Padding = new Padding(28, 16, 20, 12), ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.Paint += (_, e) => { using var pen = new Pen(UiTheme.Line); e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0); };
        var summaryArea = UiTheme.Stack(24, 28); summaryArea.Dock = DockStyle.Fill;
        profileSummary.Name = "profileSummary"; summaryArea.Controls.Add(profileSummary);
        var hint = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        hint.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        hint.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); hint.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footerInfo.Font = UiTheme.Font(9); hint.Controls.Add(footerInfo); hint.Controls.Add(fixSelection); summaryArea.Controls.Add(hint); footer.Controls.Add(summaryArea);
        fixSelection.LinkClicked += (_, _) => { ShowPage(games.Any(g => g.Enabled && !g.Ready) ? 0 : 1); };
        var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Margin = Padding.Empty, Padding = new Padding(0, 4, 0, 0) };
        var cancel = new ActionButton { Text = "Annuler", Width = 88, Height = 40, Quiet = true, DialogResult = DialogResult.Cancel, Name = "cancelSettings", Margin = new Padding(0, 0, 8, 0) };
        cancel.Click += (_, _) => Close();
        save.Width = 116; actions.Controls.Add(activate); actions.Controls.Add(save); actions.Controls.Add(cancel); footer.Controls.Add(actions); CancelButton = cancel;
        return footer;
    }

    private Control BuildApplication()
    {
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Margin = Padding.Empty };
        var layout = UiTheme.Stack(88, 256, 20, 144, 20, 100);
        layout.Controls.Add(PageHeader("Application", "Un utilitaire discret, prêt quand vous jouez"));
        var preferences = new SurfacePanel { Dock = DockStyle.Fill };
        var rows = UiTheme.Stack(64, 8, 64, 8, 64); rows.Dock = DockStyle.Fill;
        rows.Controls.Add(Preference("Mode sombre", "Désactivez pour utiliser le mode clair", darkMode)); rows.Controls.Add(new Panel());
        rows.Controls.Add(Preference("Démarrage avec Windows", "La surveillance reste désactivée au démarrage", startup)); rows.Controls.Add(new Panel());
        rows.Controls.Add(Preference("Mises à jour automatiques", "Recherche au démarrage, puis toutes les 6 heures", automaticUpdates)); preferences.Controls.Add(rows); layout.Controls.Add(preferences);
        layout.Controls.Add(new Panel());
        var tray = new SurfacePanel { Dock = DockStyle.Fill };
        var help = UiTheme.Stack(28, 28, 40);
        help.Controls.Add(UiTheme.Label("L’icône vous informe", 13, true));
        help.Controls.Add(UiTheme.Label("Vert : profil appliqué   ·   Gris : attente ou arrêt"));
        help.Controls.Add(UiTheme.Label("Double-cliquez pour activer ou arrêter la surveillance. Le clic droit donne accès à la restauration et aux mises à jour.", 10));
        tray.Controls.Add(help); layout.Controls.Add(tray); layout.Controls.Add(new Panel());
        var about = new SurfacePanel { Dock = DockStyle.Fill };
        var info = UiTheme.Stack(24, 28); info.Controls.Add(UiTheme.Label($"ResolutionTray {GitHubUpdateClient.CurrentVersion.ToString(3)} · Windows x64", 11, true));
        var releases = new LinkLabel { Text = "Voir les versions sur GitHub", Dock = DockStyle.Fill, LinkColor = UiTheme.Green, LinkBehavior = LinkBehavior.HoverUnderline, TextAlign = ContentAlignment.MiddleLeft };
        releases.LinkClicked += (_, _) => { try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/4MZ31/amd-resolution-tray/releases") { UseShellExecute = true }); } catch (Exception ex) { Program.ShowError(ex); } };
        info.Controls.Add(releases); about.Controls.Add(info); layout.Controls.Add(about); scroll.Controls.Add(layout); return scroll;
    }
    private static Control Preference(string title, string description, CheckBox option)
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48));
        var text = UiTheme.Stack(28, 28); text.Controls.Add(UiTheme.Label(title, 11, true)); text.Controls.Add(UiTheme.Label(description, 9));
        option.Anchor = AnchorStyles.Right; row.Controls.Add(text); row.Controls.Add(option); return row;
    }

    private AppSettings CreateDraft()
    {
        var profile = SelectedProfile();
        return Result with { DisplayIdentity = profile?.DisplayIdentity ?? Result.DisplayIdentity, Width = profile?.Width ?? Result.Width,
            Height = profile?.Height ?? Result.Height, RefreshRate = profile?.RefreshRate ?? Result.RefreshRate,
            Games = games.ToArray(), StartWithWindows = startup.Checked, AutoUpdate = automaticUpdates.Checked,
            CustomResolutions = customResolutions.ToArray(), CompatibilityKey = compatibilityKey, DarkMode = darkMode.Checked };
    }
    private void RefreshTheme()
    {
        if (IsDisposed || Disposing) return;
        UiTheme.Recolor(this); RenderLibrary(); UpdateDisplayInfo();
    }
    private string DraftSignature() => JsonSerializer.Serialize(CreateDraft());
    private void UpdateFooter(bool pending, int selected)
    {
        var profile = SelectedProfile();
        profileSummary.Text = profile is null ? "Configurez votre profil" : $"{profile.Width} × {profile.Height} · {(profile.RefreshRate == 0 ? targets[displays.SelectedIndex].CurrentMode.RefreshRate : profile.RefreshRate)} Hz";
        var invalidDisplay = !CanSaveDisplay();
        footerInfo.Text = pending ? "Un jeu sélectionné reste à configurer" : invalidDisplay ? "Profil d’affichage à vérifier"
            : selected == 0 ? "Choisissez au moins un jeu pour activer" : baselineDraft is not null && DraftSignature() != baselineDraft ? "Modifications non enregistrées" : $"{selected} {(selected == 1 ? "jeu" : "jeux")} · activation automatique";
        fixSelection.Visible = pending || invalidDisplay;
        footerInfo.ForeColor = pending || invalidDisplay ? UiTheme.Warning : UiTheme.Secondary;
    }
}
