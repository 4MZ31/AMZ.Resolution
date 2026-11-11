using System.Diagnostics;
using System.Collections.Concurrent;
using AMZ.Resolution.Core;

namespace AMZ.Resolution.Services;

internal sealed class SafetyMonitor : ISafetyMonitor
{
    private static readonly ConcurrentDictionary<int, long> watchdogs = new();
    private static readonly ConcurrentDictionary<Guid, EventWaitHandle> pulses = new();
    internal static void Pulse()
    {
        foreach (var signal in pulses.Values)
            try { signal.Set(); } catch (ObjectDisposedException) { }
    }
    internal static ProcessStartInfo Command(params string[] arguments)
    {
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("Exécutable introuvable.");
        var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
        if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            info.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "AMZ.Resolution.dll"));
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        return info;
    }
    public void Arm(Guid sessionId)
    {
        var pulse = new EventWaitHandle(false, EventResetMode.AutoReset, Program.MutexName + "-Pulse-" + sessionId);
        pulses[sessionId] = pulse;
        using var ready = new EventWaitHandle(false, EventResetMode.AutoReset, Program.MutexName + "-Ready-" + sessionId);
        using var process = Process.Start(Command("--watchdog", Environment.ProcessId.ToString(), sessionId.ToString(),
            Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks.ToString()))
            ?? throw new InvalidOperationException("Impossible de démarrer le processus de récupération.");
        var id = process.Id;
        var ticks = process.StartTime.ToUniversalTime().Ticks;
        watchdogs[id] = ticks;
        _ = Task.Run(() =>
        {
            try { using var child = TryParent(id, ticks); child?.WaitForExit(); }
            catch (Exception ex) { AppStorage.Log(ex); }
            finally { if (pulses.TryRemove(sessionId, out var signal)) signal.Dispose(); }
        });
        if (!ready.WaitOne(TimeSpan.FromSeconds(5)) || process.HasExited)
            throw new InvalidOperationException("Le processus de récupération ne répond pas. Activation annulée.");
    }

    internal static Task WaitForWatchdogsAsync() => Task.Run(() =>
    {
        foreach (var entry in watchdogs.ToArray())
        {
            var exited = false;
            try
            {
                using var process = Process.GetProcessById(entry.Key);
                if (process.StartTime.ToUniversalTime().Ticks == entry.Value && !process.WaitForExit(5000))
                    throw new IOException("Le processus de récupération de l’affichage est encore actif.");
                exited = true;
            }
            catch (ArgumentException) { exited = true; }
            catch (InvalidOperationException) { exited = true; }
            finally { if (exited) watchdogs.TryRemove(entry.Key, out _); }
        }
    });

    internal static int RunWatchdog(int parentId, Guid sessionId, long parentStartTicks)
    {
        try
        {
            using var ready = EventWaitHandle.OpenExisting(Program.MutexName + "-Ready-" + sessionId);
            ready.Set();
            using var parent = TryParent(parentId, parentStartTicks);
            using var pulse = EventWaitHandle.OpenExisting(Program.MutexName + "-Pulse-" + sessionId);
            var store = new RecoveryStore();
            var clock = Stopwatch.StartNew();
            var deadline = new WatchdogDeadline();
            var previousSample = TimeSpan.Zero;
            while (parent is not null && !parent.WaitForExit(1000))
            {
                var session = store.Load();
                if (session?.Id != sessionId) return 0;
                var elapsed = clock.Elapsed;
                // A suspended machine/watchdog is not proof of a hung application. Give it a fresh grace period.
                if (elapsed - previousSample > TimeSpan.FromSeconds(10))
                {
                    clock.Restart(); deadline = new WatchdogDeadline(); elapsed = TimeSpan.Zero;
                }
                previousSample = elapsed;
                if (!deadline.ShouldRecover(elapsed, pulse.WaitOne(0), session.Confirmed)) continue;
                // Never race driver recovery against the parent's still-running native call.
                parent.Kill();
                if (!parent.WaitForExit(10_000)) throw new IOException("Processus bloqué non arrêté ; journal conservé.");
                break;
            }
            // Take the same per-user lock as the main application before recovery.
            using var appLock = new Mutex(false, Program.MutexName);
            try { if (!appLock.WaitOne(TimeSpan.FromSeconds(10))) return 1; }
            catch (AbandonedMutexException) { }
            try
            {
                using var storageLock = AppStorage.AcquireExclusive("instance.lock");
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
