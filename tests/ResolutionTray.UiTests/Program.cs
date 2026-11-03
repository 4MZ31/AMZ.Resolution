using System.Text.Json;
using ResolutionTray.Core;
using ResolutionTray.Services;
using ResolutionTray.UI;

internal static class Program
{
    private static int passed;
    private static readonly DisplayTarget Display = new("test-monitor", "DISPLAY_TEST", "Écran de test 1440p", 2560, 1440, new(2560,1440,144), "test-driver", [144,120,60]);
    private static AppSettings Settings => new("test-monitor", Games: [
        new("cs2", "Counter-Strike 2", "Steam", @"C:\Games\CS2", [@"C:\Games\CS2\cs2.exe"], [], true),
        new("unknown", "Jeu à configurer", "Steam", @"C:\Games\Unknown", [], [], false),
        new("epic", "Fortnite", "Epic Games", @"C:\Games\Fortnite", [@"C:\Games\Fortnite\game.exe"], [], false)
    ]);
    [STAThread]
    private static int Main()
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2); Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        var before = UserSettings(); var recoveryBefore = RecoveryBytes();
        try
        {
            NavigationAndSelection(); DisplayAndCustom(); InvalidAndCancel(); Themes(); Dialogs(); CustomControls();
            Assert(UserSettings() == before && RecoveryBytes() == recoveryBefore, "aucun changement des paramètres ou du journal utilisateur");
            Console.WriteLine($"{passed} contrôles UI réussis, sans changement d’affichage ni écriture des préférences."); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static string? UserSettings() => File.Exists(AppStorage.FilePath("settings.json")) ? File.ReadAllText(AppStorage.FilePath("settings.json")) : null;
    private static string? RecoveryBytes() => File.Exists(AppStorage.FilePath("recovery.json")) ? File.ReadAllText(AppStorage.FilePath("recovery.json")) : null;
    private static T Find<T>(Control parent, string name) where T : Control => parent.Controls.Find(name, true).OfType<T>().Single();
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("FAIL UI: " + message);
        passed++; Console.WriteLine("PASS UI: " + message);
    }
    private static void Show(Form form) { form.Opacity = 0; form.Show(); Application.DoEvents(); }
    private static void Click(Control form, string name) { Find<Button>(form,name).PerformClick(); Application.DoEvents(); }
    private static int Option(ComboBox combo, string starts) => combo.Items.Cast<object>().ToList().FindIndex(i => i.ToString()!.StartsWith(starts, StringComparison.Ordinal));
    private static void NavigationAndSelection()
    {
        using var form = new SettingsForm(Settings, [Display], saveChanges:false); Show(form);
        var list = Find<ListView>(form,"gameLibrary");
        Assert(list.Visible && list.Items.Count == 3 && list.CheckedItems.Count == 1, "bibliothèque et sélection initiale");
        var searchFrame = Find<TextBox>(form,"searchGames").Parent!;
        Assert(searchFrame.Height <= 44 * form.DeviceDpi / 96f, $"champ de recherche compact ({searchFrame.Height}px)");
        Assert(Find<TextBox>(form,"searchGames").AccessibleName=="Rechercher un jeu", "nom accessible compréhensible");
        Click(form,"selectedGames"); Assert(list.Items.Count == 1 && list.Items[0].Text == "Counter-Strike 2", "filtre des jeux sélectionnés");
        list.Items[0].Checked = false; Application.DoEvents(); Assert(list.Items.Count == 0, "décocher retire le jeu du filtre sans perdre le catalogue");
        Click(form,"allGames"); Assert(list.Items.Count == 3 && list.CheckedItems.Count == 0, "retour au catalogue complet");
        list.Items.Cast<ListViewItem>().Single(i=>i.Text=="Counter-Strike 2").Checked = true;
        Find<TextBox>(form,"searchGames").Text = "Fortnite"; Application.DoEvents(); Assert(list.Items.Count == 1 && list.Items[0].Text == "Fortnite", "recherche par nom");
        Find<TextBox>(form,"searchGames").Clear(); Find<ComboBox>(form,"gameSource").SelectedIndex = 1; Application.DoEvents();
        Assert(list.Items.Count == 2 && list.CheckedItems.Count == 1, "filtre de source conserve les cases cochées");
        list.Items.Cast<ListViewItem>().Single(i=>i.Text=="Jeu à configurer").Checked = true; Application.DoEvents();
        Assert(!Find<Button>(form,"saveSettings").Enabled && !Find<Button>(form,"activateSettings").Enabled, "jeu sans exécutable bloque les deux actions");
        Click(form,"nav1"); Assert(Find<ComboBox>(form,"gameResolution").Visible && !list.Visible, "navigation vers Affichage");
        Click(form,"nav2"); Assert(Find<CheckBox>(form,"automaticUpdates").Visible, "navigation vers Application");
        Click(form,"nav0"); Assert(list.CheckedItems.Count == 2, "navigation conserve la sélection");
        form.ClientSize = new Size(1040,680); Application.DoEvents(); CheckButtons(form);
        Click(form,"nav1"); CheckButtons(form); Click(form,"nav2"); CheckButtons(form);
        form.Close();
    }
    private static void DisplayAndCustom()
    {
        var calls = 0; Profile? tested = null;
        using var form = new SettingsForm(Settings, [Display], false, (p,_)=>{ calls++; tested=p; return Task.FromResult(true); }); Show(form); Click(form,"nav1");
        var resolutions = Find<ComboBox>(form,"gameResolution"); var rate = Find<ComboBox>(form,"refreshRate");
        resolutions.SelectedIndex = Option(resolutions,"1920 × 1080"); rate.SelectedIndex = Option(rate,"120 Hz"); Application.DoEvents();
        Assert(Find<Label>(form,"profileSummary").Text.Contains("1920 × 1080 · 120 Hz"), "résumé suit la résolution et la fréquence");
        Assert(!Find<NumericUpDown>(form,"customWidth").Visible, "dimensions masquées pour un préréglage");
        resolutions.SelectedIndex = Option(resolutions,"Personnalisée"); Application.DoEvents();
        var width = Find<NumericUpDown>(form,"customWidth"); var height = Find<NumericUpDown>(form,"customHeight");
        Assert(width.Visible && height.Visible && !Find<Button>(form,"saveSettings").Enabled, "mode personnel révèle les dimensions et exige un test");
        width.Value = 3000; Application.DoEvents(); Assert(!Find<Button>(form,"checkCompatibility").Enabled, "dimension supérieure à la dalle non testable");
        width.Value=2251; height.Value=1267; Application.DoEvents(); Click(form,"checkCompatibility");
        Assert(calls==1 && tested is {Width:2251,Height:1267,RefreshRate:120}, "test reçoit les dimensions exactes et les Hz affichés");
        Assert(resolutions.SelectedItem!.ToString()!.Contains("perso") && Find<Button>(form,"saveSettings").Enabled, "test réussi ajoute et sélectionne le mode personnel");
        Assert(Find<Label>(form,"compatibilityStatus").Text.Contains("validé"), "résultat positif explicite");
        rate.SelectedIndex = Option(rate,"60 Hz"); Application.DoEvents(); Assert(!Find<Button>(form,"saveSettings").Enabled, "changer les Hz invalide le test personnel");
        Click(form,"checkCompatibility"); Click(form,"nav2"); Find<CheckBox>(form,"automaticUpdates").Checked=false;
        Click(form,"saveSettings");
        Assert(form.DialogResult==DialogResult.OK && form.Result.Width==2251 && form.Result.Height==1267 && form.Result.RefreshRate==60 && !form.Result.AutoUpdate, "enregistrement du brouillon complet depuis une autre page");
        Assert(form.Result.CustomResolutions!.Contains(new(2251,1267)) && form.Result.GameList.Count(g=>g.Enabled)==1, "résolutions personnelles et jeux conservés");
    }
    private static void InvalidAndCancel()
    {
        using (var form = new SettingsForm(Settings, [Display], false, (_,_)=>Task.FromResult(false)))
        {
            Show(form); Click(form,"nav1"); var resolutions=Find<ComboBox>(form,"gameResolution"); resolutions.SelectedIndex=Option(resolutions,"Personnalisée");
            Find<NumericUpDown>(form,"customWidth").Value=2250; Application.DoEvents(); Click(form,"checkCompatibility");
            Assert(!Find<Button>(form,"saveSettings").Enabled && Find<Label>(form,"compatibilityStatus").Text.Contains("non validé"), "test refusé bloque l’enregistrement et donne un retour");
            Click(form,"cancelSettings"); Assert(form.DialogResult==DialogResult.Cancel && JsonSerializer.Serialize(form.Result)==JsonSerializer.Serialize(Settings), "annulation conserve les paramètres d’origine");
        }
        using (var form = new SettingsForm(Settings with {RefreshRate=240}, [Display], false))
        {
            Show(form); Click(form,"nav1"); Assert(Find<ComboBox>(form,"refreshRate").Text.Contains("indisponible") && !Find<Button>(form,"saveSettings").Enabled, "fréquence disparue conservée et signalée"); form.Close();
        }
        using (var form = new SettingsForm(Settings with {Games=[]}, [], false))
        {
            Show(form); Assert(!Find<Button>(form,"activateSettings").Enabled && Find<Button>(form,"scanGames").Enabled, "catalogue vide reste utilisable sans écran");
            Click(form,"nav1"); Assert(Find<Label>(form,"compatibilityStatus").Text.Contains("Aucun écran"), "absence d’écran expliquée"); form.Close();
        }
    }
    private static void Themes()
    {
        using (var form = new SettingsForm(Settings, [Display], false))
        {
            Show(form); Click(form,"nav2"); var toggle=Find<CheckBox>(form,"darkMode");
            Assert(toggle.Checked && UiTheme.DarkMode && form.BackColor.R<50, "mode sombre par défaut");
            toggle.Checked=false; Application.DoEvents();
            Assert(!UiTheme.DarkMode && form.BackColor.R>200 && Find<ComboBox>(form,"gameResolution").BackColor.R>200, "mode clair appliqué aux pages masquées aussi");
            Click(form,"nav0"); Assert(Find<ListView>(form,"gameLibrary").CheckedItems.Count==1, "changement de thème conserve les jeux");
            Click(form,"saveSettings"); Assert(!form.Result.DarkMode, "choix du thème conservé dans le brouillon enregistré");
        }
        using (var form = new SettingsForm(Settings, [Display], false))
        {
            Show(form); Click(form,"nav2"); Find<CheckBox>(form,"darkMode").Checked=false; Click(form,"cancelSettings");
            Assert(UiTheme.DarkMode && form.Result.DarkMode, "annulation restaure le thème enregistré");
        }
        var legacy=JsonSerializer.Deserialize<AppSettings>("{\"DisplayIdentity\":\"test-monitor\"}")!;
        Assert(legacy.DarkMode, "anciens paramètres migrent vers le thème sombre");
        var light=Settings with {DarkMode=false};
        Assert(!JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(light))!.DarkMode, "mode clair conservé après sérialisation");
    }
    private static void Dialogs()
    {
        using (var form = new GameExecutableForm(Settings.GameList[0]))
        {
            Show(form); var choices=Find<CheckedListBox>(form,"executableChoices");
            Assert(choices.CheckedItems.Count==1 && Find<Button>(form,"applyExecutables").Enabled, "dialogue conserve les exécutables choisis");
            choices.SetItemChecked(0,false); Assert(!Find<Button>(form,"applyExecutables").Enabled, "dialogue refuse une sélection vide"); form.Close();
        }
        using (var form = new ConfirmationForm(null, ()=>true, new(2251,1267,120),true))
        {
            Show(form); Assert(Find<Label>(form,"confirmationCountdown").Text.Contains("15 s"), "délai de sécurité de 15 secondes visible");
            Click(form,"restoreProfile"); Assert(form.DialogResult==DialogResult.Cancel, "restauration explicite conserve le résultat d’annulation");
        }
    }
    private static void CustomControls()
    {
        using(var form=new SettingsForm(Settings,[Display],false))
        {
            Show(form); var search=Find<TextBox>(form,"searchGames"); search.Text="Fortnite"; Application.DoEvents();
            Assert(Find<Button>(form,"clearSearch").Visible && Find<ListView>(form,"gameLibrary").Items.Count==1,"recherche personnalisée affiche l’effacement");
            Click(form,"clearSearch"); Assert(search.TextLength==0 && Find<ListView>(form,"gameLibrary").Items.Count==3,"effacer restaure le catalogue sans perdre les jeux");
            search.Text="Counter";
            object[] arguments=[Message.Create(search.Handle,0,0,0),Keys.Escape];
            var handled=(bool)typeof(SearchTextBox).GetMethod("ProcessCmdKey",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(search,arguments)!;
            Assert(handled && search.TextLength==0 && form.Visible,"Échap efface la recherche avant de fermer la fenêtre");
            var source=Find<ThemedComboBox>(form,"gameSource");var original=source.SelectedIndex;
            NativeScrolling.SendMessage(source.Handle,0x0201,1,12|(12<<16));NativeScrolling.SendMessage(source.Handle,0x0202,0,12|(12<<16));Application.DoEvents();
            Assert(source.IsPopupOpen && !source.DroppedDown && source.ChoiceList!.AccessibilityObject.GetChildCount()==4,"menu personnalisé remplace le popup natif et expose ses options");
            source.ChoiceList!.HandleKey(Keys.End);source.ChoiceList.HandleKey(Keys.Escape);Application.DoEvents();
            Assert(!source.IsPopupOpen && source.SelectedIndex==original,"Échap ferme le menu sans changer la sélection");
            source.OpenChoices();Application.DoEvents();source.ChoiceList!.HandleKey(Keys.Down);source.ChoiceList.HandleKey(Keys.Enter);Application.DoEvents();
            Assert(!source.IsPopupOpen && source.SelectedIndex==1,"flèche et Entrée valident l’option personnalisée");
            Click(form,"nav1");var resolutions=Find<ThemedComboBox>(form,"gameResolution");resolutions.OpenChoices();Application.DoEvents();
            resolutions.ChoiceList!.HandleKey(Keys.End);Application.DoEvents();
            Assert(resolutions.ChoiceList.FirstVisible>0 && resolutions.ChoiceList.ActiveIndex==resolutions.Items.Count-1,"menu long défile jusqu’à la dernière option au clavier");
            var choicePanel=(ChoicePanel)resolutions.ChoiceList.Parent!;
            Assert(choicePanel.Bar.Visible && choicePanel.Bar.Maximum>0,"menu long utilise la barre personnalisée");
            UiTheme.SetDarkMode(false);Application.DoEvents();
            Assert(choicePanel.BackColor==UiTheme.Surface,"popup ouvert suit le thème clair");
            resolutions.CloseChoices();form.Close();
        }
        var many=Settings with {Games=Enumerable.Range(0,80).Select(i=>new GameEntry("test"+i,"Jeu "+i.ToString("D2"),"Manuel",@"C:\Games",[@"C:\Games\game"+i+".exe"],[],i==0)).ToArray()};
        using(var form=new SettingsForm(many,[Display],false))
        {
            Show(form);var list=Find<GameListView>(form,"gameLibrary");var bar=Find<SlimScrollBar>(form,"gameScroll");Application.DoEvents();
            Assert(list.HeaderStyle==ColumnHeaderStyle.None && form.Controls.Find("gameHeader",true).Length==1,"en-tête personnalisé couvre aussi la zone terminale du tableau");
            Assert(bar.Visible && bar.Maximum>0 && list.Width-NativeScrolling.ScrollbarWidth(list.DeviceDpi)>=list.Parent!.ClientSize.Width,"grande bibliothèque utilise exclusivement le défilement personnalisé");
            bar.Value=bar.Maximum;Application.DoEvents();
            Assert(list.TopItem is not null && list.TopItem.Index>0 && list.Items[^1].Bounds.Bottom<=list.ClientSize.Height,"poignée permet d’atteindre le dernier jeu");
            list.EnsureVisible(0);Application.DoEvents();Assert(bar.Value==0,"navigation native et poignée restent synchronisées");
            form.ClientSize=new Size(1040,680);Application.DoEvents();bar.Value=bar.Maximum;Application.DoEvents();
            Assert(list.Width-NativeScrolling.ScrollbarWidth(list.DeviceDpi)>=list.Parent!.ClientSize.Width && list.Items[^1].Bounds.Bottom<=list.ClientSize.Height,"redimensionnement conserve la dernière ligne accessible sans chrome blanc");
            Click(form,"nav2");var page=form.Controls.Find("pageScroll",true).OfType<SlimScrollBar>().First(b=>b.Visible);page.Value=page.Maximum;Application.DoEvents();
            Assert(page.Value==page.Maximum && !((ScrollableControl)page.Parent!).AutoScroll,"page Application défile sans barre système");form.Close();
        }
        using(var bar=new SlimScrollBar())
        {
            bar.SetRange(100,10);bar.Value=int.MaxValue;Assert(bar.Value==90,"poignée borne les valeurs hors plage");
            bar.SetRange(5,10);Assert(bar.Value==0 && bar.Maximum==0,"poignée réinitialisée quand le contenu tient dans la vue");
            bar.SetRange(100,10);bar.Value=30;bar.Wheel(-30);Assert(bar.Value==30,"petit delta de pavé tactile conservé avant un cran complet");
            bar.Wheel(-30);bar.Wheel(-30);bar.Wheel(-30);Assert(bar.Value>30,"deltas de pavé tactile cumulés sans perte de défilement");
        }
    }
    private static void CheckButtons(Control control)
    {
        foreach (Control child in control.Controls)
        {
            if (!child.Visible) continue;
            if (child is Button && child.Parent is not ScrollableControl {AutoScroll:true})
                Assert(child.Width>0 && child.Height>=32 * control.DeviceDpi / 96f && child.Right<=child.Parent!.ClientSize.Width+1 && child.Bottom<=child.Parent.ClientSize.Height+1, "bouton accessible à la taille minimale: " + child.Name);
            CheckButtons(child);
        }
    }
}
