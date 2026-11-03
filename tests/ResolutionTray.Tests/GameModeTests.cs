using ResolutionTray.Core;

internal static class GameModeTests
{
    internal static IEnumerable<(string, Func<Task>)> Cases()
    {
        yield return ("Surveillance désactivée: jeu ouvert sans bascule", async () =>
        {
            var f = new GameFixture(); f.Game.Running = true;
            await f.Tick();
            Assert(!f.Fixture.Events.Contains("create") && f.Fixture.Store.Value is null);
        });
        yield return ("Surveillance activée: aucun changement sans jeu", async () =>
        {
            var f = new GameFixture(); f.Mode.Enable();
            await f.Tick(); await f.Tick();
            Assert(f.Mode.Enabled && f.Fixture.Store.Value is null && !f.Fixture.Events.Contains("create"));
            Assert(f.Fixture.Backend.Current == Fixture.Original);
        });
        yield return ("Lancement du jeu: activation unique et confirmation", async () =>
        {
            var f = new GameFixture(); f.Mode.Enable();
            await f.Tick(); f.Game.Running = true;
            await f.Tick(); await f.Tick(); await f.Tick();
            Assert(f.Fixture.Store.Value is { Confirmed: true });
            Assert(f.Fixture.Events.Count(e => e == "apply") == 1 && f.Confirmations == 1);
        });
        yield return ("Jeu déjà ouvert quand la surveillance est armée", async () =>
        {
            var f = new GameFixture(); f.Game.Running = true; f.Mode.Enable();
            await f.Tick();
            Assert(await f.Fixture.Controller.IsActiveAsync());
        });
        yield return ("Fermeture du jeu: bureau restauré et surveillance conservée", async () =>
        {
            var f = new GameFixture(); f.Mode.Enable(); f.Game.Running = true;
            await f.Tick(); f.Game.Running = false; await f.Tick();
            Assert(f.Fixture.Store.Value is null && f.Fixture.Backend.Current == Fixture.Original);
            Assert(f.Fixture.Backend.Scaling == Fixture.Scaling && f.Mode.Enabled);
            f.Game.Running = true; await f.Tick();
            Assert(await f.Fixture.Controller.IsActiveAsync() && f.Confirmations == 2);
        });
        yield return ("Désactivation manuelle en jeu: restauration sans réactivation", async () =>
        {
            var f = new GameFixture(); f.Mode.Enable(); f.Game.Running = true;
            await f.Tick(); await f.Mode.DisableAsync(); await f.Tick();
            Assert(!f.Mode.Enabled && f.Fixture.Store.Value is null);
            Assert(f.Fixture.Events.Count(e => e == "apply") == 1);
            Assert(f.Fixture.Backend.Current == Fixture.Original);
        });
        yield return ("Désactivation en attente: prochain lancement ignoré", async () =>
        {
            var f = new GameFixture(); f.Mode.Enable(); await f.Tick();
            await f.Mode.DisableAsync(); f.Game.Running = true; await f.Tick();
            Assert(!f.Mode.Enabled && !f.Fixture.Events.Contains("apply"));
        });
        yield return ("Fermeture pendant création AMD: résolution jamais appliquée", async () =>
        {
            var f = new GameFixture(); f.Mode.Enable(); f.Game.Running = true;
            f.Fixture.Backend.BeforeCreate = () => f.Game.Running = false;
            await f.Tick();
            Assert(!f.Fixture.Events.Contains("apply") && f.Fixture.Store.Value is null);
            Assert(f.Fixture.Backend.Current == Fixture.Original && f.Confirmations == 0);
        });
        yield return ("Fermeture avant application Windows: restauration des réglages AMD", async () =>
        {
            var f = new GameFixture(); f.Mode.Enable(); f.Game.Running = true;
            f.Fixture.Backend.BeforeTest = () => f.Game.Running = false;
            await f.Tick();
            Assert(!f.Fixture.Events.Contains("apply") && f.Fixture.Backend.Scaling == Fixture.Scaling);
            Assert(f.Fixture.Store.Value is null);
        });
        yield return ("Fermeture pendant application Windows: restauration immédiate", async () =>
        {
            var f = new GameFixture(); f.Mode.Enable(); f.Game.Running = true;
            f.Fixture.Backend.BeforeApply = () => f.Game.Running = false;
            await f.Tick();
            Assert(f.Fixture.Backend.Current == Fixture.Original && f.Fixture.Store.Value is null);
        });
        yield return ("Fermeture pendant confirmation: profil non confirmé", async () =>
        {
            var f = new GameFixture(); f.Mode.Enable(); f.Game.Running = true;
            await f.Mode.SynchronizeAsync(new("monitor"), _ => { f.Game.Running = false; return Task.FromResult(true); });
            Assert(f.Fixture.Backend.Current == Fixture.Original && f.Fixture.Store.Value is null);
        });
        yield return ("Confirmation refusée: aucune boucle de réactivation pendant la partie", async () =>
        {
            var f = new GameFixture(); f.Mode.Enable(); f.Game.Running = true;
            await f.Mode.SynchronizeAsync(new("monitor"), _ => Task.FromResult(false));
            await f.Tick(); await f.Tick();
            Assert(f.Fixture.Events.Count(e => e == "apply") == 1 && f.Fixture.Store.Value is null);
            f.Game.Running = false; await f.Tick(); f.Game.Running = true; await f.Tick();
            Assert(await f.Fixture.Controller.IsActiveAsync());
        });
        yield return ("Changement externe en jeu: aucune réapplication en boucle", async () =>
        {
            var f = new GameFixture(); f.Mode.Enable(); f.Game.Running = true; await f.Tick();
            f.Fixture.Backend.Current = Fixture.Original; await f.Tick();
            Assert(!await f.Fixture.Controller.IsActiveAsync() && f.Fixture.Events.Count(e => e == "apply") == 1);
        });
        yield return ("Désactivation pendant une activation: annulation et restauration", async () =>
        {
            var f = new GameFixture(); f.Mode.Enable(); f.Game.Running = true;
            using var entered = new ManualResetEventSlim(); using var resume = new ManualResetEventSlim();
            f.Fixture.Backend.BeforeCreate = () =>
            {
                entered.Set();
                if (!resume.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException();
            };
            var activation = f.Tick();
            Assert(entered.Wait(TimeSpan.FromSeconds(5)));
            var disabled = f.Mode.DisableAsync();
            resume.Set();
            await activation; await disabled;
            Assert(!f.Mode.Enabled && f.Fixture.Store.Value is null && !f.Fixture.Events.Contains("apply"));
        });
        yield return ("Restauration impossible à la fermeture: sauvegarde conservée", async () =>
        {
            var f = new GameFixture(); f.Mode.Enable(); f.Game.Running = true; await f.Tick();
            f.Game.Running = false; f.Fixture.Backend.FailRestore = true;
            try { await f.Tick(); throw new Exception("Erreur attendue"); }
            catch (InvalidOperationException) { }
            Assert(f.Fixture.Store.Value is not null);
        });
        yield return ("Garde du moteur: aucun journal ni mutation si le jeu est absent", async () =>
        {
            var f = new Fixture();
            try { await f.Controller.ActivateAsync(new("monitor"), () => false); throw new Exception("Annulation attendue"); }
            catch (OperationCanceledException) { }
            Assert(f.Store.Value is null && !f.Events.Contains("create"));
        });
    }

    private static void Assert(bool result)
    {
        if (!result) throw new Exception("Comportement de surveillance inattendu.");
    }

    private sealed class Presence : IGamePresence
    {
        public volatile bool Running;
        public bool IsRunning() => Running;
    }
    private sealed class GameFixture
    {
        internal readonly Fixture Fixture = new();
        internal readonly Presence Game = new();
        internal readonly GameModeController Mode;
        internal int Confirmations;
        internal GameFixture() { Mode = new(Fixture.Controller, Game); }
        internal Task Tick() => Mode.SynchronizeAsync(new("monitor"), _ =>
        {
            Confirmations++; return Task.FromResult(true);
        });
    }
}
