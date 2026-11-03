using ResolutionTray.Core;
using ResolutionTray.Services;

namespace ResolutionTray.UI;

internal sealed partial class SettingsForm
{
    private sealed record ResolutionChoice(ResolutionSize? Size, bool Custom = false)
    {
        public override string ToString() => Size is null ? "Personnalisée…" :
            Size + (Custom ? " · perso" : Size.Width == 2560 ? " · native" : Size.Width == 2324 ? " · défaut" : "");
    }
    private sealed record RefreshChoice(int Rate, string Label) { public override string ToString() => Label; }
    private readonly ComboBox resolutions = new ThemedComboBox() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, AccessibleName = "Résolution de jeu" };
    private readonly ComboBox refreshRates = new ThemedComboBox() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, AccessibleName = "Taux de rafraîchissement" };
    private readonly NumericUpDown customWidth = new() { Minimum = 1, Maximum = 7680, Value = 2324, Dock = DockStyle.Fill, AccessibleName = "Largeur en pixels" };
    private readonly NumericUpDown customHeight = new() { Minimum = 1, Maximum = 4320, Value = 1306, Dock = DockStyle.Fill, AccessibleName = "Hauteur en pixels" };
    private readonly Button checkDisplay = UiTheme.Button("Vérifier la compatibilité", 280);
    private readonly Label compatibility = UiTheme.Label("", 9);
    private readonly DisplayPreview preview = new();
    private readonly List<ResolutionSize> customResolutions = [];
    private readonly Func<Profile, Func<bool>, Task<bool>>? testProfile;
    private string? compatibilityKey;
    private bool updatingDisplay;
    private bool testingDisplay;

    private readonly Label diagonalValue = UiTheme.Label("", 11, true);
    private readonly Label horizontalValue = UiTheme.Label("", 10);
    private readonly Label verticalValue = UiTheme.Label("", 10);
    private TableLayoutPanel? displayFields;
    private Control? customDimensions;
    private string? testFeedback;

    private Control BuildDisplay()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 88)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(PageHeader("Affichage", "Un profil commun à tous vos jeux sélectionnés"));
        var columns = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        columns.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        var leftScroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Margin = new Padding(0, 0, 12, 0) };
        var settings = new SurfacePanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        displayFields = UiTheme.Stack(20, 44, 20, 20, 44, 0, 20, 20, 44, 44, 12, 40, 84);
        displayFields.Controls.Add(UiTheme.Label("Écran", 9, true));
        displayFields.Controls.Add(UiTheme.Field(displays, "targetDisplay")); displayFields.Controls.Add(new Panel());
        displayFields.Controls.Add(UiTheme.Label("Résolution de jeu", 9, true));
        displayFields.Controls.Add(UiTheme.Field(resolutions, "gameResolution"));
        var dimensions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2, Margin = Padding.Empty, Visible = false };
        dimensions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); dimensions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 16)); dimensions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        dimensions.RowStyles.Add(new RowStyle(SizeType.Absolute, 24)); dimensions.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        dimensions.Controls.Add(UiTheme.Label("Largeur (px)", 9), 0, 0); dimensions.Controls.Add(UiTheme.Label("Hauteur (px)", 9), 2, 0);
        dimensions.Controls.Add(UiTheme.Field(customWidth, "customWidth"), 0, 1); dimensions.Controls.Add(UiTheme.Field(customHeight, "customHeight"), 2, 1);
        customDimensions = dimensions; displayFields.Controls.Add(dimensions); displayFields.Controls.Add(new Panel());
        displayFields.Controls.Add(UiTheme.Label("Taux de rafraîchissement", 9, true));
        displayFields.Controls.Add(UiTheme.Field(refreshRates, "refreshRate"));
        displayFields.Controls.Add(UiTheme.Label("Les fréquences proposées sont celles détectées sur votre écran", 9));
        displayFields.Controls.Add(new Panel());
        checkDisplay.Name = "checkCompatibility"; checkDisplay.Dock = DockStyle.Fill; checkDisplay.Margin = new Padding(0, 0, 0, 4);
        if (checkDisplay is ActionButton check) check.Glyph = UiGlyph.Monitor;
        displayFields.Controls.Add(checkDisplay);
        compatibility.Name = "compatibilityStatus"; compatibility.Padding = new Padding(0, 12, 0, 0); compatibility.TextAlign = ContentAlignment.TopLeft;
        displayFields.Controls.Add(compatibility); settings.Controls.Add(displayFields); leftScroll.Controls.Add(settings); columns.Controls.Add(leftScroll);
        var rightScroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Margin = new Padding(12, 0, 0, 0) };
        var view = new SurfacePanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        var content = UiTheme.Stack(28, 256, 8, 96, 16, 52);
        content.Controls.Add(UiTheme.Label("Votre zone de jeu", 12, true));
        preview.Name = "displayPreview"; content.Controls.Add(preview); content.Controls.Add(new Panel());
        var facts = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, Margin = Padding.Empty };
        facts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56)); facts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
        foreach (var _ in new[] { 0, 1, 2 }) facts.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        facts.Controls.Add(UiTheme.Label("Sur une dalle 27″", 9), 0, 0); facts.Controls.Add(diagonalValue, 1, 0);
        facts.Controls.Add(UiTheme.Label("Marges latérales", 9), 0, 1); facts.Controls.Add(horizontalValue, 1, 1);
        facts.Controls.Add(UiTheme.Label("Marges haut / bas", 9), 0, 2); facts.Controls.Add(verticalValue, 1, 2);
        horizontalValue.Font = verticalValue.Font = UiTheme.Figures(9);
        content.Controls.Add(facts); content.Controls.Add(new Panel());
        displayInfo.Text = "Le bureau retrouve sa résolution et sa fréquence à la fermeture du dernier jeu";
        displayInfo.Font = UiTheme.Font(9); content.Controls.Add(displayInfo); view.Controls.Add(content); rightScroll.Controls.Add(view); columns.Controls.Add(rightScroll);
        layout.Controls.Add(columns); return layout;
    }

    private void InitializeDisplay(AppSettings settings)
    {
        updatingDisplay = true;
        compatibilityKey = settings.CompatibilityKey;
        foreach (var size in (settings.CustomResolutions ?? []).Append(new(settings.Width, settings.Height)).Distinct())
            if (size.Width is >= 640 and <= 2560 && size.Height is >= 480 and <= 1440 && !DisplayProfiles.Presets.Contains(size))
                customResolutions.Add(size);
        foreach (var size in DisplayProfiles.Presets) resolutions.Items.Add(new ResolutionChoice(size));
        foreach (var size in customResolutions) resolutions.Items.Add(new ResolutionChoice(size, true));
        resolutions.Items.Add(new ResolutionChoice(null, true));
        resolutions.SelectedIndex = resolutions.Items.Cast<ResolutionChoice>().ToList().FindIndex(c => c.Size == new ResolutionSize(settings.Width, settings.Height));
        if (resolutions.SelectedIndex < 0) resolutions.SelectedIndex = 0;
        foreach (var display in targets) displays.Items.Add(display.Name);
        var selected = targets.ToList().FindIndex(d => d.Identity == settings.DisplayIdentity);
        displays.SelectedIndex = selected >= 0 ? selected : targets.Count == 1 ? 0 : -1;
        PopulateRefreshRates(settings.RefreshRate);
        updatingDisplay = false;
        displays.SelectedIndexChanged += (_, _) =>
        {
            if (updatingDisplay) return;
            var desired = (refreshRates.SelectedItem as RefreshChoice)?.Rate ?? 0;
            updatingDisplay = true; PopulateRefreshRates(desired); updatingDisplay = false;
            UpdateButtons();
        };
        resolutions.SelectedIndexChanged += (_, _) => { testFeedback = null; UpdateButtons(); };
        refreshRates.SelectedIndexChanged += (_, _) => { testFeedback = null; UpdateButtons(); };
        customWidth.ValueChanged += (_, _) => { if (!updatingDisplay) testFeedback = null; UpdateButtons(); };
        customHeight.ValueChanged += (_, _) => { if (!updatingDisplay) testFeedback = null; UpdateButtons(); };
        checkDisplay.Click += async (_, _) => await CheckDisplayAsync();
    }

    private void PopulateRefreshRates(int selectedRate)
    {
        refreshRates.Items.Clear();
        if (displays.SelectedIndex < 0) return;
        var display = targets[displays.SelectedIndex];
        refreshRates.Items.Add(new RefreshChoice(0, $"Fréquence du bureau ({display.CurrentMode.RefreshRate} Hz)"));
        foreach (var rate in display.RefreshRates ?? [display.CurrentMode.RefreshRate])
            refreshRates.Items.Add(new RefreshChoice(rate, $"{rate} Hz"));
        var index = refreshRates.Items.Cast<RefreshChoice>().ToList().FindIndex(c => c.Rate == selectedRate);
        // Keep an unavailable saved rate visible and invalid, instead of silently changing the profile.
        if (index < 0 && selectedRate != 0)
        {
            refreshRates.Items.Add(new RefreshChoice(selectedRate, $"{selectedRate} Hz · indisponible"));
            index = refreshRates.Items.Count - 1;
        }
        refreshRates.SelectedIndex = Math.Max(0, index);
    }

    private Profile? SelectedProfile()
    {
        if (displays.SelectedIndex < 0 || resolutions.SelectedItem is not ResolutionChoice choice || refreshRates.SelectedItem is not RefreshChoice rate) return null;
        var size = choice.Size ?? new((int)customWidth.Value, (int)customHeight.Value);
        return new(targets[displays.SelectedIndex].Identity, rate.Rate, size.Width, size.Height);
    }

    private string? SelectedKey()
    {
        var profile = SelectedProfile();
        if (profile is null) return null;
        try
        {
            var display = targets[displays.SelectedIndex];
            return DisplayProfiles.Fingerprint(display, DisplayProfiles.Resolve(profile, display));
        }
        catch (InvalidOperationException) { return null; }
    }

    private bool CanSaveDisplay() => !testingDisplay && SelectedKey() is { } key
        && (resolutions.SelectedItem is not ResolutionChoice { Custom: true } || key == compatibilityKey);

    private void UpdateDisplayInfo()
    {
        if (updatingDisplay) return;
        var input = resolutions.SelectedItem is ResolutionChoice { Size: null };
        if (customDimensions is not null && displayFields is not null)
        {
            customDimensions.Visible = input;
            displayFields.RowStyles[5].Height = input ? 80 * DeviceDpi / 96f : 0;
        }
        customWidth.Enabled = customHeight.Enabled = input && !testingDisplay;
        if (!input && resolutions.SelectedItem is ResolutionChoice { Size: { } size })
        {
            updatingDisplay = true; customWidth.Value = size.Width; customHeight.Value = size.Height; updatingDisplay = false;
        }
        var key = SelectedKey();
        checkDisplay.Enabled = testProfile is not null && key is not null && !testingDisplay && scanCancellation is null;
        var personal = resolutions.SelectedItem is ResolutionChoice { Custom: true };
        checkDisplay.Text = testingDisplay ? "Test en cours…" : input ? "Tester et ajouter" : "Tester ce profil";
        compatibility.ForeColor = key is not null && key == compatibilityKey ? UiTheme.Green : personal ? UiTheme.Warning : UiTheme.Secondary;
        if (SelectedProfile() is not { } profile)
        {
            compatibility.Text = "Aucun écran AMD 2560 × 1440 sélectionné. Vérifiez le branchement et le mode Étendre de Windows.";
            diagonalValue.Text = horizontalValue.Text = verticalValue.Text = "—";
            return;
        }
        try
        {
            var mode = DisplayProfiles.Resolve(profile, targets[displays.SelectedIndex]);
            preview.SetResolution(mode.Width, mode.Height, mode.RefreshRate);
            var diagonal = 27 * Math.Sqrt((double)mode.Width * mode.Width + (double)mode.Height * mode.Height) / Math.Sqrt(2560d * 2560 + 1440d * 1440);
            diagonalValue.Text = $"≈ {diagonal:0.0}″";
            horizontalValue.Text = $"{(2560 - mode.Width) / 2d:0.#} px / côté";
            verticalValue.Text = $"{(1440 - mode.Height) / 2d:0.#} px / côté";
            compatibility.Text = testingDisplay ? "Confirmation visuelle de 15 secondes, puis retour au bureau"
                : testFeedback is not null ? testFeedback
                : key == compatibilityKey ? "Profil testé et validé\nLe bureau a été restauré"
                : testProfile is null ? "Aperçu en lecture seule\nAucun changement d’affichage ne sera effectué"
                : personal ? "Test requis avant enregistrement\nEssai de 15 secondes, puis retour au bureau"
                : "Test facultatif de 15 secondes\nLe bureau sera restauré même après validation";
        }
        catch (InvalidOperationException ex)
        {
            compatibility.ForeColor = UiTheme.Error; compatibility.Text = ex.Message;
            diagonalValue.Text = horizontalValue.Text = verticalValue.Text = "—";
        }
    }

    private async Task CheckDisplayAsync()
    {
        if (testingDisplay || testProfile is null || SelectedProfile() is not { } profile || SelectedKey() is not { } key) return;
        testingDisplay = true;
        UpdateButtons();
        Enabled = false;
        try
        {
            var presence = new SelectedGamePresence();
            presence.Update(games);
            var mode = DisplayProfiles.Resolve(profile, targets[displays.SelectedIndex]);
            // Test the Hz shown in the UI even if the desktop changed since the window opened.
            if (await testProfile(profile with { RefreshRate = mode.RefreshRate }, presence.IsRunning))
            {
                compatibilityKey = key;
                var size = new ResolutionSize(profile.Width, profile.Height);
                if (!DisplayProfiles.Presets.Contains(size) && !customResolutions.Contains(size))
                {
                    customResolutions.Add(size);
                    var index = resolutions.Items.Count - 1;
                    resolutions.Items.Insert(index, new ResolutionChoice(size, true));
                    resolutions.SelectedIndex = index;
                }
                testFeedback = "Profil testé et validé\nLe bureau a été restauré";
            }
            else { compatibilityKey = null; testFeedback = "Test non validé\nLe bureau a été restauré ; vous pouvez réessayer"; }
        }
        catch (Exception ex) { compatibilityKey = null; testFeedback = "Test impossible — vérifiez le message et réessayez"; Program.ShowError(ex); }
        finally { Enabled = true; testingDisplay = false; UpdateButtons(); }
    }
}
