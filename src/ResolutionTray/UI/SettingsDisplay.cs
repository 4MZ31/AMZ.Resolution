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
    private readonly ComboBox resolutions = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, AccessibleName = "Résolution de jeu" };
    private readonly ComboBox refreshRates = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, AccessibleName = "Taux de rafraîchissement" };
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

    private Control BuildDisplay()
    {
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White, Margin = Padding.Empty };
        var card = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, BackColor = Color.White, Padding = new Padding(20),
            ColumnCount = 1, RowCount = 14, Margin = Padding.Empty };
        foreach (var height in new[] { 32, 32, 22, 34, 32, 22, 34, 38, 46, 100, 42, 32, 54, 24 })
            card.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        card.Controls.Add(UiTheme.Label("Votre zone de jeu", 14, true));
        displays.AccessibleName = "Écran cible";
        card.Controls.Add(displays);
        card.Controls.Add(UiTheme.Label("Résolution", 9));
        card.Controls.Add(resolutions);
        var dimensions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty };
        dimensions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        dimensions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 24));
        dimensions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        dimensions.Controls.Add(customWidth); dimensions.Controls.Add(UiTheme.Label("×")); dimensions.Controls.Add(customHeight);
        card.Controls.Add(dimensions);
        card.Controls.Add(UiTheme.Label("Taux de rafraîchissement", 9));
        card.Controls.Add(refreshRates);
        checkDisplay.Dock = DockStyle.Fill;
        card.Controls.Add(checkDisplay);
        card.Controls.Add(compatibility);
        card.Controls.Add(preview);
        card.Controls.Add(displayInfo);
        card.Controls.Add(UiTheme.Label("Appliqué aux jeux cochés.\nBureau et fréquence restaurés à leur fermeture.", 9));
        var options = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = Padding.Empty };
        options.Controls.Add(startup); options.Controls.Add(automaticUpdates);
        card.Controls.Add(options);
        card.Controls.Add(UiTheme.Label("Vert : en jeu · Gris : attente ou arrêt", 9));
        scroll.Controls.Add(card);
        return scroll;
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
        resolutions.SelectedIndexChanged += (_, _) => UpdateButtons();
        refreshRates.SelectedIndexChanged += (_, _) => UpdateButtons();
        customWidth.ValueChanged += (_, _) => UpdateButtons();
        customHeight.ValueChanged += (_, _) => UpdateButtons();
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
        customWidth.Enabled = customHeight.Enabled = input && !testingDisplay;
        if (!input && resolutions.SelectedItem is ResolutionChoice { Size: { } size })
        {
            updatingDisplay = true;
            customWidth.Value = size.Width; customHeight.Value = size.Height;
            updatingDisplay = false;
        }
        var key = SelectedKey();
        checkDisplay.Enabled = testProfile is not null && key is not null && !testingDisplay && scanCancellation is null;
        compatibility.ForeColor = key is not null && key == compatibilityKey ? UiTheme.Green : UiTheme.Muted;
        if (SelectedProfile() is not { } profile)
        {
            compatibility.Text = "Sélectionnez un écran AMD 2560 × 1440.";
            displayInfo.Text = "Utilisez le mode Étendre de Windows.";
            return;
        }
        try
        {
            var mode = DisplayProfiles.Resolve(profile, targets[displays.SelectedIndex]);
            preview.SetResolution(mode.Width, mode.Height);
            var diagonal = 27 * Math.Sqrt((double)mode.Width * mode.Width + (double)mode.Height * mode.Height) / Math.Sqrt(2560d * 2560 + 1440d * 1440);
            displayInfo.Text = $"{mode.Width} × {mode.Height} · {mode.RefreshRate} Hz · centré\nSur une dalle 27″ : environ {diagonal:0.0}″";
            compatibility.Text = testingDisplay ? "Test en cours… confirmation visuelle de 15 s."
                : key == compatibilityKey ? "Test validé. Le bureau a été restauré."
                : testProfile is null ? "Aperçu : aucun test matériel ne sera lancé."
                : resolutions.SelectedItem is ResolutionChoice { Custom: true } ? "Test requis avant enregistrement.\nL’écran changera brièvement, puis sera restauré."
                : "Test temporaire disponible (15 s).\nLa compatibilité sera aussi vérifiée en jeu.";
        }
        catch (InvalidOperationException ex) { compatibility.Text = ex.Message; displayInfo.Text = "Profil indisponible : modifiez les valeurs."; }
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
            }
            else compatibilityKey = null;
        }
        catch (Exception ex) { compatibilityKey = null; Program.ShowError(ex); }
        finally { Enabled = true; testingDisplay = false; UpdateButtons(); }
    }
}
