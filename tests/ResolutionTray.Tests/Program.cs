using ResolutionTray.Core;

var cases = new List<(string, Func<Task>)>
{
    ("Activation: journal et surveillance avant toute mutation", async () =>
    {
        var f = new Fixture();
        await f.Controller.ActivateAsync(new("monitor"));
        Assert(f.Events.IndexOf("save") < f.Events.IndexOf("arm"), "journal avant surveillance");
        Assert(f.Events.IndexOf("arm") < f.Events.IndexOf("create"), "surveillance avant pilote");
        Assert(f.Backend.Current.Width == 2324 && f.Backend.Current.Height == 1306, "dimensions exactes");
        Assert(f.Backend.Current.RefreshRate == 165, "fréquence conservée");
        Assert(!await f.Controller.IsActiveAsync(), "activation non confirmée reste inactive");
        await f.Controller.ConfirmAsync();
        Assert(await f.Controller.IsActiveAsync(), "confirmation active le profil");
    }),
    ("Désactivation: restauration exacte puis suppression du journal", async () =>
    {
        var f = new Fixture();
        await f.Controller.ActivateAsync(new("monitor")); await f.Controller.ConfirmAsync();
        await f.Controller.RestoreAsync();
        Assert(f.Backend.Current == Fixture.Original, "mode original exact");
        Assert(f.Backend.Scaling == Fixture.Scaling, "réglages AMD originaux");
        Assert(f.Store.Value is null && !await f.Controller.IsActiveAsync(), "session effacée après vérification");
    }),
    ("Écran portrait refusé sans mutation", async () =>
    {
        var f = new Fixture(); f.Backend.Current = Fixture.Original with { Orientation = 1 };
        await ExpectError(() => f.Controller.ActivateAsync(new("monitor")));
        Assert(!f.Events.Contains("create") && f.Store.Value is null, "pas de rotation implicite");
    }),
    ("Écran Full HD refusé sans mutation", async () =>
    {
        var f = new Fixture(); f.Backend.NativeWidth = 1920;
        await ExpectError(() => f.Controller.ActivateAsync(new("monitor")));
        Assert(!f.Events.Contains("create") && f.Store.Value is null, "aucune mutation");
    }),
    ("Écran déconnecté refusé sans mutation", async () =>
    {
        var f = new Fixture(); f.Backend.Connected = false;
        await ExpectError(() => f.Controller.ActivateAsync(new("monitor")));
        Assert(f.Store.Value is null, "aucun journal inutile");
    }),
    ("Échec de sauvegarde: aucun appel modifiant le pilote", async () =>
    {
        var f = new Fixture(); f.Store.FailSave = true;
        await ExpectError(() => f.Controller.ActivateAsync(new("monitor")));
        Assert(!f.Events.Contains("arm") && !f.Events.Contains("create"), "échec sûr");
    }),
    ("Surveillance indisponible: activation annulée et état restauré", async () =>
    {
        var f = new Fixture(); f.Safety.Fail = true;
        await ExpectError(() => f.Controller.ActivateAsync(new("monitor")));
        Assert(!f.Events.Contains("create") && f.Store.Value is null, "pas de bascule sans surveillance");
    }),
    ("Centrage non appliqué: retour au mode initial", async () =>
    {
        var f = new Fixture(); f.Backend.IgnoreCentering = true;
        await ExpectError(() => f.Controller.ActivateAsync(new("monitor")));
        Assert(f.Backend.Current == Fixture.Original && f.Store.Value is null, "restauration après défaut de centrage");
    }),
    ("Mode réellement appliqué différent: activation refusée", async () =>
    {
        var f = new Fixture(); f.Backend.IgnoreApply = true;
        await ExpectError(() => f.Controller.ActivateAsync(new("monitor")));
        Assert(f.Store.Value is null, "activation non validée");
    }),
    ("Échec de restauration: conservation des données de récupération", async () =>
    {
        var f = new Fixture(); await f.Controller.ActivateAsync(new("monitor"));
        f.Backend.FailRestore = true;
        await ExpectError(f.Controller.RestoreAsync);
        Assert(f.Store.Value is not null, "journal conservé");
    }),
    ("Restauration incomplète: conservation des données de récupération", async () =>
    {
        var f = new Fixture(); await f.Controller.ActivateAsync(new("monitor"));
        f.Backend.IgnoreRestore = true;
        await ExpectError(f.Controller.RestoreAsync);
        Assert(f.Store.Value is not null, "journal conservé malgré retour sans erreur du pilote");
    }),
    ("Changement externe: l'état actif reflète l'affichage réel", async () =>
    {
        var f = new Fixture(); await f.Controller.ActivateAsync(new("monitor")); await f.Controller.ConfirmAsync();
        f.Backend.Current = Fixture.Original;
        Assert(!await f.Controller.IsActiveAsync(), "vert supprimé si le jeu change le mode");
        Assert(f.Store.Value is not null, "restauration toujours disponible");
    }),
    ("Changement AMD externe: l'état actif est invalidé", async () =>
    {
        var f = new Fixture(); await f.Controller.ActivateAsync(new("monitor")); await f.Controller.ConfirmAsync();
        f.Backend.Scaling = Fixture.Scaling;
        Assert(!await f.Controller.IsActiveAsync(), "centrage nécessaire à l'état vert");
    }),
    ("Deuxième activation: ne pas écraser la sauvegarde", async () =>
    {
        var f = new Fixture(); await f.Controller.ActivateAsync(new("monitor")); var saved = f.Store.Value;
        await ExpectError(() => f.Controller.ActivateAsync(new("monitor")));
        Assert(f.Store.Value == saved, "première sauvegarde intacte");
    }),
    ("Confirmation après changement externe: refusée", async () =>
    {
        var f = new Fixture(); await f.Controller.ActivateAsync(new("monitor"));
        f.Backend.Current = Fixture.Original;
        await ExpectError(f.Controller.ConfirmAsync);
        Assert(f.Store.Value is { Confirmed: false }, "pas de confirmation fictive");
    }),
    ("Récupération: une nouvelle instance restaure la session existante", async () =>
    {
        var f = new Fixture(); await f.Controller.ActivateAsync(new("monitor"));
        var restarted = new ProfileController(f.Backend, f.Store, f.Safety);
        await restarted.RestoreAsync();
        Assert(f.Store.Value is null && f.Backend.Current == Fixture.Original, "récupération après redémarrage");
    }),
    ("Concurrence: une bascule simultanée est refusée", async () =>
    {
        var f = new Fixture();
        using var entered = new ManualResetEventSlim(); using var resume = new ManualResetEventSlim();
        f.Backend.BeforeCreate = () => { entered.Set(); if (!resume.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException(); };
        var activation = f.Controller.ActivateAsync(new("monitor"));
        Assert(entered.Wait(TimeSpan.FromSeconds(5)), "activation commencée");
        try { await ExpectError(f.Controller.RestoreAsync); }
        finally { resume.Set(); }
        await activation;
        Assert(f.Store.Value is not null, "activation initiale intacte");
    })
};
cases.AddRange(GameModeTests.Cases());
cases.AddRange(GameCatalogTests.Cases());
foreach (var stage in new[] { "create", "center", "test", "apply" })
{
    var failedStage = stage;
    cases.Add(($"Échec pilote à l'étape {stage}: restauration", async () =>
    {
        var f = new Fixture(); f.Backend.FailStage = failedStage;
        await ExpectError(() => f.Controller.ActivateAsync(new("monitor")));
        Assert(f.Backend.Current == Fixture.Original && f.Backend.Scaling == Fixture.Scaling, "état initial restauré");
        Assert(f.Store.Value is null, "journal supprimé seulement après restauration");
    }));
}
var failures = 0;
foreach (var (name, test) in cases)
{
    try { await test(); Console.WriteLine("PASS " + name); }
    catch (Exception ex) { failures++; Console.Error.WriteLine($"FAIL {name}: {ex}"); }
}
Console.WriteLine($"{cases.Count - failures}/{cases.Count} tests réussis.");
return failures == 0 ? 0 : 1;

static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
static async Task ExpectError(Func<Task> action)
{
    try { await action(); } catch (InvalidOperationException) { return; } catch (AggregateException) { return; }
    throw new Exception("Une erreur était attendue.");
}

sealed class Fixture
{
    internal static readonly DisplayMode Original = new(2560, 1440, 165);
    internal static readonly ScalingState Scaling = new(false, 1, true);
    internal readonly List<string> Events = [];
    internal FakeBackend Backend { get; }
    internal FakeStore Store { get; }
    internal FakeSafety Safety { get; }
    internal ProfileController Controller { get; }
    internal Fixture()
    {
        Backend = new(Events); Store = new(Events); Safety = new(Events);
        Controller = new(Backend, Store, Safety);
    }
}
sealed class FakeStore(List<string> events) : IRecoveryStore
{
    public RecoverySession? Value;
    public bool FailSave;
    public RecoverySession? Load() => Value;
    public void Save(RecoverySession session)
    {
        if (FailSave) throw new InvalidOperationException("Disque inaccessible");
        events.Add("save"); Value = session;
    }
    public void Delete() { events.Add("delete"); Value = null; }
}
sealed class FakeSafety(List<string> events) : ISafetyMonitor
{
    public bool Fail;
    public void Arm(Guid id) { events.Add("arm"); if (Fail) throw new InvalidOperationException("Processus refusé"); }
}
sealed class FakeBackend(List<string> events) : IDisplayBackend
{
    public DisplayMode Current = Fixture.Original;
    public ScalingState Scaling = Fixture.Scaling;
    public bool Connected = true, IgnoreCentering, IgnoreApply, FailRestore, IgnoreRestore;
    public int NativeWidth = 2560;
    public string? FailStage;
    public Action? BeforeCreate, BeforeTest, BeforeApply;
    public IReadOnlyList<DisplayTarget> GetDisplays() => Connected
        ? [new("monitor", "DISPLAY2", "Generic 1440p", NativeWidth, 1440, Current)] : [];
    public ScalingState GetScaling(string identity) => Scaling;
    public void EnsureCustomMode(string identity, DisplayMode mode) { BeforeCreate?.Invoke(); Stage("create"); }
    public void SetCentered(string identity) { Stage("center"); if (!IgnoreCentering) Scaling = new(true, 2, false); }
    public void TestMode(string identity, DisplayMode mode) { BeforeTest?.Invoke(); Stage("test"); }
    public void ApplyMode(string identity, DisplayMode mode) { BeforeApply?.Invoke(); Stage("apply"); if (!IgnoreApply) Current = mode; }
    public bool Matches(string identity, DisplayMode mode) => Connected && Current == mode;
    public bool IsCentered(string identity) => Scaling.GpuScaling && Scaling.Mode == 2 && Scaling.IntegerScaling is not true;
    public void Restore(RecoverySession session)
    {
        events.Add("restore");
        if (FailRestore) throw new InvalidOperationException("Écran absent");
        if (!IgnoreRestore) { Current = session.OriginalMode; Scaling = session.OriginalScaling; }
    }
    private void Stage(string stage) { events.Add(stage); if (FailStage == stage) throw new InvalidOperationException("Pilote refusé"); }
}
