using System.Text.Json;
using AMZ.Resolution.Core;

internal static class GameCatalogTests
{
    private static readonly string Folder = Path.Combine(Path.GetTempPath(), "AMZ.Resolution-tests", "Games");
    private static string Exe(string game, string file = "Game.exe") => Path.Combine(Folder, game, file);
    private static GameEntry Entry(string id, bool enabled = true, string? executable = null) =>
        new(id, id, "Manuel", Path.Combine(Folder, id), [executable ?? Exe(id)], [], enabled);
    private static (string, Func<Task>) Test(string name, Action action) => (name, () => { action(); return Task.CompletedTask; });
    private static void Check(bool value) { if (!value) throw new Exception("Catalogue inattendu."); }
    private static string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    private static string Steam(string folder = "Sample") => "\"AppState\" { \"appid\" \"42\" \"name\" \"Sample Game\" \"installdir\" " + Quote(folder) + " }";
    private static string Epic(string launch = "Game.exe", bool incomplete = false) => JsonSerializer.Serialize(new
    {
        AppName = "sample", DisplayName = "Sample Game", InstallLocation = Folder, LaunchExecutable = launch, bIsIncompleteInstall = incomplete
    });

    internal static IEnumerable<(string, Func<Task>)> Cases()
    {
        yield return Test("Steam: bibliothèques imbriquées sur plusieurs disques", () =>
        {
            var first = Path.Combine(Folder, "Steam1"); var second = Path.Combine(Folder, "Steam2");
            var text = "\"libraryfolders\" { \"0\" { \"path\" " + Quote(first) + " \"apps\" { \"730\" \"900\" } } \"1\" { \"path\" " + Quote(second) + " } }";
            Check(GameManifests.SteamLibraries(text).SequenceEqual(new[] { first, second }));
        });
        yield return Test("Steam: ancien format de bibliothèques", () =>
        {
            var text = "libraryfolders { // commentaire\n 0 " + Quote(Folder) + " }";
            Check(GameManifests.SteamLibraries(text).Single() == Folder);
        });
        yield return Test("Steam: échappement des chemins et des guillemets", () =>
        {
            var value = ValveKeyValues.Parse("root { name " + Quote("A \"quoted\" game") + " path " + Quote(Folder) + " }").Child("root")!;
            Check(value.Text("name") == "A \"quoted\" game" && value.Text("path") == Folder);
        });
        yield return Test("Steam: manifeste incomplet refusé", () =>
        {
            try { ValveKeyValues.Parse("root { name \"unfinished"); throw new Exception("Erreur attendue"); }
            catch (FormatException) { }
        });
        yield return Test("Steam: installation trouvée sous steamapps/common", () =>
        {
            var game = GameManifests.Steam(Steam(), Folder)!;
            Check(game.Id == "steam:42" && game.Name == "Sample Game" && game.Directory == Path.Combine(Folder, "steamapps", "common", "Sample"));
        });
        yield return Test("Steam: sortie du dossier de jeux refusée", () =>
        {
            Check(GameManifests.Steam(Steam(Path.Combine("..", "..", "outside")), Folder) is null);
            Check(GameManifests.Steam(Steam(Folder), Folder) is null);
        });
        yield return Test("Steam: composants redistribuables exclus", () =>
        {
            Check(GameManifests.Steam("AppState { appid 228980 name Redistributables installdir Redist }", Folder) is null);
        });
        yield return Test("Epic: nom, dossier et exécutable de lancement", () =>
        {
            var game = GameManifests.Epic(Epic("Game/Binaries/Win64/Sample.exe"))!;
            Check(game.Id == "epic:sample" && game.Source == "Epic Games" && game.LaunchExecutable == Path.Combine(Folder, "Game", "Binaries", "Win64", "Sample.exe"));
        });
        yield return Test("Epic: installation incomplète exclue", () => Check(GameManifests.Epic(Epic(incomplete: true)) is null));
        yield return Test("Epic: lancement hors du dossier refusé", () => Check(GameManifests.Epic(Epic("../../outside.exe"))!.LaunchExecutable is null));
        yield return Test("Epic: manifeste non objet ignoré", () => Check(GameManifests.Epic("[]") is null));
        yield return Test("Catalogue: un rescan conserve les jeux cochés et décochés", () =>
        {
            var saved = new[] { Entry("a"), Entry("b", false) };
            var scanned = new[] { Entry("a", false), Entry("b"), Entry("c", false) };
            var result = GameCatalog.Merge(saved, scanned);
            Check(result.Single(g => g.Id == "a").Enabled && !result.Single(g => g.Id == "b").Enabled && !result.Single(g => g.Id == "c").Enabled);
        });
        yield return Test("Catalogue: ajout manuel conservé entre deux scans", () =>
        {
            var manual = Entry("manual");
            Check(GameCatalog.Merge([manual], [Entry("steam")]).Contains(manual));
        });
        yield return Test("Catalogue: choix manuel d’exécutable conservé", () =>
        {
            var saved = Entry("same", executable: Exe("same", "Chosen.exe"));
            var result = GameCatalog.Merge([saved], [Entry("same", false)]).Single();
            Check(result.Executables.Single() == saved.Executables.Single());
        });
        yield return Test("Catalogue: installation déplacée mise à jour", () =>
        {
            var saved = Entry("same");
            var moved = saved with { InstallDirectory = Path.Combine(Folder, "moved"), Executables = [Exe("moved")] };
            var result = GameCatalog.Merge([saved], [moved]).Single();
            Check(result.Enabled && result.Executables.SequenceEqual(moved.Executables));
        });
        yield return Test("Catalogue: absence temporaire et dédoublonnage", () =>
        {
            var old = Entry("old"); var current = Entry("current");
            var result = GameCatalog.Merge([old], [current, current]);
            Check(result.Length == 2 && result.Contains(old));
        });
        yield return Test("Migration: CS2 et CS:GO conservés sans scan", () =>
        {
            var matcher = new GameProcessMatcher(GameCatalog.Defaults());
            Check(matcher.HasSelection && matcher.Matches("cs2", null) && matcher.Matches("csgo", null));
        });
        yield return Test("Sélection vide: aucun jeu surveillé", () => Check(!new GameProcessMatcher([]).HasSelection));
        yield return Test("Processus: jeu décoché ignoré", () => Check(!new GameProcessMatcher([Entry("off", false)]).Matches("Game", Exe("off"))));
        yield return Test("Processus: même nom dans un autre dossier ignoré", () =>
        {
            var matcher = new GameProcessMatcher([Entry("selected")]);
            Check(!matcher.Matches("Game", Exe("other")) && matcher.Matches("Game", Exe("selected")));
        });
        yield return Test("Processus: chemin indisponible ne valide pas un jeu scanné", () => Check(!new GameProcessMatcher([Entry("selected")]).Matches("Game", null)));
        yield return Test("Processus: comparaison insensible à la casse", () => Check(new GameProcessMatcher([Entry("case")]).Matches("GAME", Exe("case").ToUpperInvariant())));
        yield return Test("Exécutables: auxiliaires de lancement exclus", () =>
        {
            Check(!GameExecutables.IsCandidate("Launcher.exe") && !GameExecutables.IsCandidate("CrashReportClient.exe")
                && !GameExecutables.IsCandidate("EasyAntiCheat.exe") && GameExecutables.IsCandidate("ActualGame.exe"));
        });
        yield return Test("Exécutables: version Win64 Shipping préférée", () =>
        {
            var preferred = Exe("ue", "Sample-Win64-Shipping.exe");
            var game = GameExecutables.FromInstallation(new("steam:1", "Sample", "Steam", Folder), [Exe("ue", "Sample.exe"), preferred]);
            Check(game.Executables.SequenceEqual(new[] { preferred }));
        });
        yield return Test("Exécutables: ambiguïté nécessite un choix utilisateur", () =>
        {
            var game = GameExecutables.FromInstallation(new("steam:1", "Sample", "Steam", Folder), [Exe("one"), Exe("two")]);
            Check(!game.Ready && game.Candidates.Length == 2);
        });
        yield return Test("Exécutables: lancement Epic explicite réutilisé", () =>
        {
            var launch = Exe("launch");
            var game = GameExecutables.FromInstallation(new("epic:1", "Sample", "Epic Games", Folder, launch), [launch, Exe("other")]);
            Check(game.Executables.SequenceEqual(new[] { launch }));
        });
        yield return Test("Exécutables: installation sans fichier ne devient pas prête", () =>
        {
            Check(!GameExecutables.FromInstallation(new("steam:1", "Sample", "Steam", Folder), []).Ready);
        });
        yield return Test("Préférences: sélection multiple conservée en JSON", () =>
        {
            var entries = new[] { Entry("one"), Entry("two", false) };
            var json = JsonSerializer.Serialize(entries);
            var read = JsonSerializer.Deserialize<GameEntry[]>(json)!;
            Check(read.Length == 2 && read[0].Enabled && !read[1].Enabled && !json.Contains("\"Ready\"", StringComparison.Ordinal));
        });
        yield return ("Plusieurs jeux: restauration seulement après fermeture du dernier", async () =>
        {
            var f = new Fixture(); var a = Entry("a"); var b = Entry("b");
            var presence = new Presence([a, b]); var mode = new GameModeController(f.Controller, presence);
            mode.Enable(); presence.Running.Add(a.Executables[0]); await Tick();
            presence.Running.Add(b.Executables[0]); await Tick();
            presence.Running.Remove(a.Executables[0]); await Tick();
            Check(f.Store.Value is { Confirmed: true } && f.Events.Count(e => e == "apply") == 1);
            presence.Running.Clear(); await Tick();
            Check(f.Store.Value is null && f.Backend.Current == Fixture.Original && mode.Enabled);
            Task Tick() => mode.SynchronizeAsync(new("monitor"), _ => Task.FromResult(true));
        });
        yield return ("Jeu non sélectionné: aucun changement de résolution", async () =>
        {
            var f = new Fixture(); var presence = new Presence([Entry("selected")]);
            presence.Running.Add(Exe("other"));
            var mode = new GameModeController(f.Controller, presence); mode.Enable();
            await mode.SynchronizeAsync(new("monitor"), _ => Task.FromResult(true));
            Check(f.Store.Value is null && !f.Events.Contains("apply"));
        });
    }
    private sealed class Presence(IEnumerable<GameEntry> games) : IGamePresence
    {
        private readonly GameProcessMatcher matcher = new(games);
        internal readonly List<string> Running = [];
        public bool IsRunning() => Running.Any(p => matcher.Matches(Path.GetFileNameWithoutExtension(p), p));
    }
}
