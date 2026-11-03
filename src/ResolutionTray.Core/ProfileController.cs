namespace ResolutionTray.Core;

/// <summary>Serializes display changes and retains recovery data until restoration is verified.</summary>
public sealed class ProfileController(IDisplayBackend backend, IRecoveryStore store, ISafetyMonitor safety)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    public bool IsBusy { get; private set; }
    public RecoverySession? Session => store.Load();

    public async Task ActivateAsync(Profile profile, Func<bool>? canApply = null)
    {
        await RunExclusiveAsync(() =>
        {
            RequirePermission(canApply);
            if (store.Load() is not null)
                throw new InvalidOperationException("Une session doit être restaurée avant une nouvelle activation.");
            var display = backend.GetDisplays().SingleOrDefault(d => d.Identity == profile.DisplayIdentity)
                ?? throw new InvalidOperationException("L'écran sélectionné est déconnecté.");
            var requested = DisplayProfiles.Resolve(profile, display);
            var session = new RecoverySession(Guid.NewGuid(), display.Identity, display.CurrentMode,
                backend.GetScaling(display.Identity), requested);
            // Persist and start the independent recovery process BEFORE touching the driver.
            store.Save(session);
            try
            {
                safety.Arm(session.Id);
                RequirePermission(canApply);
                backend.EnsureCustomMode(display.Identity, requested);
                RequirePermission(canApply);
                backend.SetCentered(display.Identity);
                backend.TestMode(display.Identity, requested);
                RequirePermission(canApply);
                backend.ApplyMode(display.Identity, requested);
                RequirePermission(canApply);
                VerifyRequested(session);
            }
            catch (Exception activationError)
            {
                try { RestoreAndClear(session); }
                catch (Exception recoveryError)
                {
                    throw new AggregateException("Activation échouée. La restauration reste à effectuer.",
                        activationError, recoveryError);
                }
                throw;
            }
        });
    }

    /// <summary>Explicit settings test: always restore, even after visual acceptance or an exception.</summary>
    public async Task<bool> TestAsync(Profile profile, Func<RecoverySession, Task<bool>> confirm, Func<bool>? canApply = null)
    {
        await ActivateAsync(profile, canApply);
        try { return await confirm(Session!) && (canApply?.Invoke() ?? true); }
        finally { await RestoreAsync(); }
    }

    public Task ConfirmAsync() => ConfirmAsync(null);

    public async Task ConfirmAsync(Func<bool>? canApply)
    {
        await RunExclusiveAsync(() =>
        {
            var session = store.Load() ?? throw new InvalidOperationException("Aucune activation à confirmer.");
            RequirePermission(canApply);
            VerifyRequested(session);
            RequirePermission(canApply);
            store.Save(session with { Confirmed = true });
        });
    }

    public async Task RestoreAsync()
    {
        await RunExclusiveAsync(() =>
        {
            if (store.Load() is { } session) RestoreAndClear(session);
        });
    }

    public async Task<bool> IsActiveAsync()
    {
        if (!await gate.WaitAsync(0)) return false;
        try
        {
            return await Task.Run(() => store.Load() is { Confirmed: true } session
                && backend.Matches(session.DisplayIdentity, session.RequestedMode)
                && backend.IsCentered(session.DisplayIdentity));
        }
        finally { gate.Release(); }
    }

    private void VerifyRequested(RecoverySession session)
    {
        if (!backend.Matches(session.DisplayIdentity, session.RequestedMode)
            || !backend.IsCentered(session.DisplayIdentity))
            throw new InvalidOperationException("Le mode ou le centrage demandé n'a pas été appliqué.");
    }

    private void RestoreAndClear(RecoverySession session)
    {
        backend.Restore(session);
        if (!backend.Matches(session.DisplayIdentity, session.OriginalMode)
            || backend.GetScaling(session.DisplayIdentity) != session.OriginalScaling)
            throw new InvalidOperationException("La restauration n'est pas complète. Les données de récupération sont conservées.");
        store.Delete();
    }

    private static void RequirePermission(Func<bool>? canApply)
    {
        if (canApply is not null && !canApply())
            throw new OperationCanceledException("Le jeu est fermé ou la surveillance a été désactivée.");
    }

    private async Task RunExclusiveAsync(Action action)
    {
        if (!await gate.WaitAsync(0))
            throw new InvalidOperationException("Un changement d'affichage est déjà en cours.");
        IsBusy = true;
        try { await Task.Run(action); }
        finally { IsBusy = false; gate.Release(); }
    }
}
