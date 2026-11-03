using System.Diagnostics;
using ResolutionTray.Core;

namespace ResolutionTray.Services;

internal sealed class SafetyMonitor : ISafetyMonitor
{
    internal static ProcessStartInfo Command(params string[] arguments)
    {
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("Exécutable introuvable.");
        var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
        if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            info.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "ResolutionTray.dll"));
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        return info;
    }
    public void Arm(Guid sessionId)
    {
        using var ready = new EventWaitHandle(false, EventResetMode.AutoReset, Program.MutexName + "-Ready-" + sessionId);
        using var process = Process.Start(Command("--watchdog", Environment.ProcessId.ToString(), sessionId.ToString(),
            Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks.ToString()))
            ?? throw new InvalidOperationException("Impossible de démarrer le processus de récupération.");
        if (!ready.WaitOne(TimeSpan.FromSeconds(5)) || process.HasExited)
            throw new InvalidOperationException("Le processus de récupération ne répond pas. Activation annulée.");
    }

    internal static int RunWatchdog(int parentId, Guid sessionId, long parentStartTicks)
    {
        try
        {
            using var ready = EventWaitHandle.OpenExisting(Program.MutexName + "-Ready-" + sessionId);
            ready.Set();
            using var parent = TryParent(parentId, parentStartTicks);
            var store = new RecoveryStore();
            while (parent is not null && !parent.WaitForExit(1000))
                if (store.Load()?.Id != sessionId) return 0;
            // Take the same per-user lock as the main application before recovery.
            using var appLock = new Mutex(false, Program.MutexName);
            try { if (!appLock.WaitOne(TimeSpan.FromSeconds(10))) return 1; }
            catch (AbandonedMutexException) { }
            try
            {
                if (store.Load() is not { } session || session.Id != sessionId) return 0;
                var backend = new WindowsDisplayBackend();
                backend.Restore(session);
                if (!backend.Matches(session.DisplayIdentity, session.OriginalMode)
                    || backend.GetScaling(session.DisplayIdentity) != session.OriginalScaling)
                    throw new InvalidOperationException("Récupération automatique incomplète.");
                store.Delete();
                return 0;
            }
            finally { appLock.ReleaseMutex(); }
        }
        catch (Exception ex) { AppStorage.Log(ex); return 1; }
    }

    private static Process? TryParent(int id, long ticks)
    {
        try
        {
            var process = Process.GetProcessById(id);
            if (process.StartTime.ToUniversalTime().Ticks == ticks) return process;
            process.Dispose();
        }
        catch (ArgumentException) { }
        return null;
    }
}
