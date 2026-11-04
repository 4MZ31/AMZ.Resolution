namespace AMZ.Resolution.Core;

public interface IGamePresence { bool IsRunning(); }

/// <summary>Applies at most once per observed game run; disabling always restores the saved desktop.</summary>
public sealed class GameModeController(ProfileController controller, IGamePresence game)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private bool attempted;
    private volatile bool enabled;
    public bool Enabled => enabled;

    public void Enable() { enabled = true; attempted = false; }

    public async Task DisableAsync()
    {
        enabled = false;
        await gate.WaitAsync();
        try { await controller.RestoreAsync(); attempted = false; }
        finally { gate.Release(); }
    }

    public async Task SynchronizeAsync(Profile profile, Func<RecoverySession, Task<bool>> confirm)
    {
        if (!await gate.WaitAsync(0)) return;
        try
        {
            if (!game.IsRunning())
            {
                attempted = false;
                await controller.RestoreAsync();
                return;
            }
            if (!Enabled) { await controller.RestoreAsync(); return; }
            if (attempted || controller.Session is not null) return;
            // No repeated attempts after a refusal/error while the same game run remains open.
            attempted = true;
            try
            {
                await controller.ActivateAsync(profile, CanApply);
                if (!CanApply() || !await confirm(controller.Session!) || !CanApply())
                {
                    await controller.RestoreAsync();
                    return;
                }
                await controller.ConfirmAsync(CanApply);
                if (!CanApply()) await controller.RestoreAsync();
            }
            catch (OperationCanceledException) when (!CanApply())
            {
                await controller.RestoreAsync();
            }
            finally { if (!game.IsRunning()) attempted = false; }
        }
        finally { gate.Release(); }
    }

    private bool CanApply() => Enabled && game.IsRunning();
}
