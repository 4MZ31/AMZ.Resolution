using System.Text.Json;
using ResolutionTray.Core;
using ResolutionTray.Services;

internal static class DisplayProfileTests
{
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static DisplayTarget Display => new("monitor", "DISPLAY2", "1440p", 2560, 1440, new(2560, 1440, 165), "driver1", [60, 120, 144, 165]);
    private static async Task Reject(Func<Task> action)
    {
        try { await action(); } catch (InvalidOperationException) { return; }
        throw new Exception("Un rejet était attendu.");
    }
    public static IEnumerable<(string, Func<Task>)> Cases()
    {
        foreach (var size in DisplayProfiles.Presets)
        {
            yield return ($"Préréglage {size}: dimensions exactes et restauration", async () =>
            {
                var f = new Fixture();
                await f.Controller.ActivateAsync(new("monitor", 120, size.Width, size.Height));
                Assert(f.Backend.Current.Width == size.Width && f.Backend.Current.Height == size.Height && f.Backend.Current.RefreshRate == 120, "profil choisi appliqué");
                await f.Controller.RestoreAsync();
                Assert(f.Backend.Current == Fixture.Original && f.Backend.Scaling == Fixture.Scaling, "résolution, fréquence et mise à l’échelle restaurées");
            });
        }
        yield return ("Fréquences natives: filtrage, dédoublonnage et tri", () =>
        {
            DisplayMode[] modes = [new(2560,1440,165), new(2560,1440,60), new(2560,1440,165), new(1920,1080,240),
                new(2560,1440,144, Flags:2), new(2560,1440,120, Orientation:1), new(2560,1440,100, BitsPerPixel:16), new(2560,1440,1)];
            Assert(DisplayProfiles.NativeRefreshRates(modes, Display).SequenceEqual([165,60]), "seuls les modes natifs progressifs compatibles sont retenus");
            return Task.CompletedTask;
        });
        foreach (var size in new[] { new ResolutionSize(0,1306), new(2324,0), new(-1,720), new(639,480), new(640,479), new(2561,1440), new(2560,1441), new(int.MaxValue,int.MaxValue) })
        {
            yield return ($"Dimensions invalides {size}: rejet sans mutation", async () =>
            {
                var f = new Fixture();
                await Reject(() => f.Controller.ActivateAsync(new("monitor", 0, size.Width, size.Height)));
                Assert(f.Store.Value is null && !f.Events.Contains("arm") && !f.Events.Contains("create"), "aucun changement du pilote");
            });
        }
        foreach (var hz in new[] { -1, 1, 23, 1001, 240 })
        {
            yield return ($"Fréquence invalide ou absente {hz}: rejet sans mutation", async () =>
            {
                var f = new Fixture(); f.Backend.RefreshRates = [60,120,144,165];
                await Reject(() => f.Controller.ActivateAsync(new("monitor", hz)));
                Assert(f.Store.Value is null && !f.Events.Contains("arm"), "aucune sauvegarde ni bascule inutile");
            });
        }
        yield return ("Aucune fréquence native détectée: refus sans approximation", async () =>
        {
            var f = new Fixture(); f.Backend.RefreshRates = [];
            await Reject(() => f.Controller.ActivateAsync(new("monitor")));
            Assert(f.Store.Value is null, "aucune supposition de compatibilité");
        });
        yield return ("Mode personnalisé exact sans arrondi et fréquence explicite", async () =>
        {
            var f = new Fixture(); f.Backend.RefreshRates = [60,120,165];
            await f.Controller.ActivateAsync(new("monitor", 120, 2251, 1267));
            Assert(f.Backend.Current == new DisplayMode(2251,1267,120), "ni largeur ni fréquence arrondie");
            await f.Controller.RestoreAsync();
            Assert(f.Backend.Current == Fixture.Original, "165 Hz d’origine restaurés");
        });
        foreach (var accepted in new[] { true, false })
        {
            yield return ($"Test temporaire {(accepted ? "accepté" : "refusé")}: bureau toujours restauré", async () =>
            {
                var f = new Fixture();
                var result = await f.Controller.TestAsync(new("monitor", 120, 2272,1278), session =>
                {
                    Assert(f.Backend.Current == session.RequestedMode && f.Store.Value is not null, "test réel protégé par le journal");
                    return Task.FromResult(accepted);
                });
                Assert(result == accepted && f.Store.Value is null && f.Backend.Current == Fixture.Original, "résultat et restauration exacts");
            });
        }
        yield return ("Test temporaire: exception de confirmation restaure le bureau", async () =>
        {
            var f = new Fixture();
            await Reject(async () => await f.Controller.TestAsync(new("monitor", 120), _ => throw new InvalidOperationException("fermeture")));
            Assert(f.Backend.Current == Fixture.Original && f.Store.Value is null, "restauration après exception");
        });
        yield return ("Test temporaire: échec de restauration conserve le journal", async () =>
        {
            var f = new Fixture();
            await Reject(async () => await f.Controller.TestAsync(new("monitor"), _ => { f.Backend.FailRestore = true; return Task.FromResult(true); }));
            Assert(f.Store.Value is not null, "récupération toujours possible");
        });
        yield return ("Test temporaire: jeu lancé pendant la préparation annule le mode", async () =>
        {
            var f = new Fixture(); var available = true;
            f.Backend.BeforeCreate = () => available = false;
            try { await f.Controller.TestAsync(new("monitor"), _ => Task.FromResult(true), () => available); throw new Exception("annulation attendue"); }
            catch (OperationCanceledException) { }
            Assert(f.Backend.Current == Fixture.Original && f.Store.Value is null, "pas de test concurrent avec le jeu");
        });
        yield return ("Test temporaire: jeu lancé pendant la confirmation invalide le test", async () =>
        {
            var f = new Fixture(); var available = true;
            var result = await f.Controller.TestAsync(new("monitor"), _ => { available = false; return Task.FromResult(true); }, () => available);
            Assert(!result && f.Store.Value is null && f.Backend.Current == Fixture.Original, "test non validé et bureau restauré");
        });
        yield return ("Préparation à une autre fréquence: échec pilote restaure le bureau initial", async () =>
        {
            var f = new Fixture();
            f.Backend.BeforeCreate = () => f.Backend.Current = new(2560,1440,120);
            f.Backend.FailStage = "create";
            await Reject(() => f.Controller.ActivateAsync(new("monitor",120,2251,1267)));
            Assert(f.Backend.Current == Fixture.Original && f.Store.Value is null, "bureau antérieur à la préparation récupéré");
        });
        yield return ("Empreinte de validation: résolution, Hz et pilote indépendants", () =>
        {
            var mode = DisplayProfiles.Resolve(new("monitor"), Display);
            var key = DisplayProfiles.Fingerprint(Display, mode);
            Assert(key != DisplayProfiles.Fingerprint(Display, mode with { Width = 2272 }), "largeur");
            Assert(key != DisplayProfiles.Fingerprint(Display, mode with { Height = 1278 }), "hauteur");
            Assert(key != DisplayProfiles.Fingerprint(Display, mode with { RefreshRate = 120 }), "fréquence");
            Assert(key != DisplayProfiles.Fingerprint(Display with { DriverVersion = "driver2" }, mode), "pilote");
            return Task.CompletedTask;
        });
        yield return ("Anciens paramètres: profil 2324 × 1306 et fréquence du bureau conservés", () =>
        {
            var settings = JsonSerializer.Deserialize<AppSettings>("{\"DisplayIdentity\":\"monitor\",\"AutoUpdate\":false}")!;
            Assert(settings.Width == 2324 && settings.Height == 1306 && settings.RefreshRate == 0 && !settings.AutoUpdate, "migration sans changement des réglages existants");
            return Task.CompletedTask;
        });
        yield return ("Paramètres: dimensions, Hz et résolutions ajoutées persistent", () =>
        {
            var original = new AppSettings("monitor", Width:2251, Height:1267, RefreshRate:120, CustomResolutions:[new(2251,1267)], CompatibilityKey:"checked");
            var loaded = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(original))!;
            Assert(loaded.Width == 2251 && loaded.Height == 1267 && loaded.RefreshRate == 120 && loaded.CompatibilityKey == "checked" && loaded.CustomResolutions!.SequenceEqual(original.CustomResolutions!), "aller-retour JSON");
            return Task.CompletedTask;
        });
    }
}
