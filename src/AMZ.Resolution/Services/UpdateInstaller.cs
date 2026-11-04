using System.Diagnostics;
using System.Text.Json;
using AMZ.Resolution.Core;

namespace AMZ.Resolution.Services;

internal sealed record UpdateJob(Guid Id, int ParentId, long ParentStartTicks, string Target,
    string OldHash, string PayloadHash, string Version, bool ResumeMonitoring);
internal sealed record UpdateOutcome(string Version, bool Success, string? Error, int? ProcessId = null, Guid? JobId = null);

internal static class UpdateInstaller
{
    private static string InstallMutex => Program.MutexName + "-InstallUpdate";
    private static string ReadyName(Guid id) => Program.MutexName + "-UpdateReady-" + id;
    private static string HealthyName(Guid id) => Program.MutexName + "-UpdateHealthy-" + id;
    internal static bool CanInstall
    {
        get
        {
#if PORTABLE
            return true;
#else
            return false;
#endif
        }
    }
    internal static bool IsInstalling()
    {
        using var mutex = new Mutex(false, InstallMutex);
        var acquired = false;
        try
        {
            try { acquired = mutex.WaitOne(0); }
            catch (AbandonedMutexException) { acquired = true; }
            return !acquired;
        }
        finally { if (acquired) mutex.ReleaseMutex(); }
    }

    internal static Task BeginAsync(PreparedUpdate update, bool resumeMonitoring, string target) => Task.Run(() =>
    {
        target = Path.GetFullPath(target);
        if (!target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || !File.Exists(target)
            || (File.GetAttributes(target) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("L’exécutable installé n’est pas un fichier portable modifiable.");
        // Test destination permissions before asking the running application to exit.
        var probe = target + "." + Guid.NewGuid().ToString("N") + ".write-test";
        try { using var stream = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None); }
        finally { if (File.Exists(probe)) File.Delete(probe); }
        var id = Guid.ParseExact(Path.GetFileName(update.Directory), "N");
        if (!Path.GetFullPath(update.Directory).Equals(Path.Combine(Path.GetFullPath(GitHubUpdateClient.UpdatesRoot), id.ToString("N")), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Dossier de mise à jour inattendu.");
        var helper = Path.Combine(update.Directory, "AMZ.Resolution.Updater.exe");
        File.Copy(target, helper, true);
        using var parent = Process.GetCurrentProcess();
        var job = new UpdateJob(id, parent.Id, parent.StartTime.ToUniversalTime().Ticks, target,
            UpdateArchive.HashFile(target), update.Hash, update.Version.ToString(3), resumeMonitoring);
        var jobPath = Path.Combine(update.Directory, "install.json");
        File.WriteAllText(jobPath, JsonSerializer.Serialize(job, AppStorage.Json));
        File.Delete(Path.Combine(update.Directory, "cancelled"));
        using var ready = new EventWaitHandle(false, EventResetMode.AutoReset, ReadyName(id));
        var info = new ProcessStartInfo(helper) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
        info.ArgumentList.Add("--apply-update"); info.ArgumentList.Add(jobPath);
        using var child = Process.Start(info) ?? throw new IOException("Le processus de mise à jour n’a pas démarré.");
        if (!ready.WaitOne(TimeSpan.FromSeconds(10)) || child.HasExited)
        {
            File.WriteAllText(Path.Combine(update.Directory, "cancelled"), "cancelled");
            throw new IOException("Le processus de mise à jour ne répond pas. L’application reste ouverte.");
        }
    });

    internal static int Run(string jobPath)
    {
        UpdateJob? job = null;
        var parentExited = false;
        using var install = new Mutex(false, InstallMutex);
        var acquired = false;
        try
        {
            try { acquired = install.WaitOne(0); }
            catch (AbandonedMutexException) { acquired = true; }
            if (!acquired) return 1;
            job = ReadJob(jobPath);
            var directory = Path.GetDirectoryName(Path.GetFullPath(jobPath))!;
            var payload = Path.Combine(directory, "payload.exe");
            if (!UpdateRelease.TryVersion(job.Version, out var version) || !UpdateArchive.MatchesHash(payload, job.PayloadHash)
                || !UpdateArchive.MatchesHash(Environment.ProcessPath!, job.OldHash))
                throw new InvalidDataException("Les fichiers de l’installation ont changé.");
            GitHubUpdateClient.VerifyExecutable(payload, version);
            using (var ready = EventWaitHandle.OpenExisting(ReadyName(job.Id))) ready.Set();
            WaitForParent(job, directory);
            parentExited = true;
            if (File.Exists(Path.Combine(directory, "cancelled"))) throw new IOException("Mise à jour annulée.");
            using (var main = new Mutex(false, Program.MutexName))
            {
                var available = false;
                try { available = main.WaitOne(0); }
                catch (AbandonedMutexException) { available = true; }
                if (!available) throw new IOException("Une autre instance de l’application est ouverte. Mise à jour annulée.");
                main.ReleaseMutex();
            }
            using var healthy = new EventWaitHandle(false, EventResetMode.AutoReset, HealthyName(job.Id));
            int? startedId = null;
            UpdateTransaction.Install(job.Target, payload, job.OldHash, job.PayloadHash, () =>
            {
                using var child = Start(job.Target, job.Id, job.ResumeMonitoring);
                startedId = child.Id;
                var deadline = Stopwatch.StartNew();
                while (deadline.Elapsed < TimeSpan.FromSeconds(30))
                {
                    if (healthy.WaitOne(100)) return true;
                    if (child.HasExited) return false;
                }
                child.Kill();
                if (!child.WaitForExit(5000)) throw new IOException("Le nouveau processus ne peut pas être arrêté pour la restauration.");
                return false;
            });
            AppStorage.WriteAtomic("last-update.json", new UpdateOutcome(job.Version, true, null, startedId, job.Id));
            return 0;
        }
        catch (Exception ex)
        {
            AppStorage.Log(ex);
            if (acquired) { install.ReleaseMutex(); acquired = false; }
            if (job is not null && !File.Exists(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(jobPath))!, "cancelled")))
            {
                int? processId = null;
                // Start the original version only if rollback actually restored its exact bytes.
                try
                {
                    if (parentExited && UpdateArchive.MatchesHash(job.Target, job.OldHash))
                    {
                        using var original = Start(job.Target, null, job.ResumeMonitoring);
                        processId = original.Id;
                    }
                }
                catch (Exception restartError) { AppStorage.Log(restartError); }
                AppStorage.WriteAtomic("last-update.json", new UpdateOutcome(job.Version, false, ex.Message, processId, job.Id));
            }
            return 1;
        }
        finally { if (acquired) install.ReleaseMutex(); }
    }
    private static UpdateJob ReadJob(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var job = JsonSerializer.Deserialize<UpdateJob>(File.ReadAllText(fullPath)) ?? throw new InvalidDataException("Installation vide.");
        var expected = Path.Combine(Path.GetFullPath(GitHubUpdateClient.UpdatesRoot), job.Id.ToString("N"), "install.json");
        if (!fullPath.Equals(expected, StringComparison.OrdinalIgnoreCase) || job.Id == Guid.Empty || !UpdateArchive.IsHash(job.OldHash) || !UpdateArchive.IsHash(job.PayloadHash)
            || !Path.GetFullPath(Environment.ProcessPath!).Equals(Path.Combine(Path.GetDirectoryName(expected)!, "AMZ.Resolution.Updater.exe"), StringComparison.OrdinalIgnoreCase)
            || !Path.IsPathFullyQualified(job.Target) || !job.Target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Contexte d’installation inattendu.");
        return job;
    }
    private static void WaitForParent(UpdateJob job, string directory)
    {
        if (File.Exists(Path.Combine(directory, "cancelled"))) throw new IOException("Mise à jour annulée.");
        Process? parent = null;
        try
        {
            try
            {
                parent = Process.GetProcessById(job.ParentId);
                if (parent.StartTime.ToUniversalTime().Ticks != job.ParentStartTicks) return;
            }
            catch (ArgumentException) { return; }
            catch (InvalidOperationException) { return; }
            var elapsed = Stopwatch.StartNew();
            while (!parent.WaitForExit(100))
            {
                if (File.Exists(Path.Combine(directory, "cancelled"))) throw new IOException("Mise à jour annulée.");
                if (elapsed.Elapsed > TimeSpan.FromSeconds(60)) throw new IOException("L’application ne s’est pas fermée à temps.");
            }
            if (File.Exists(Path.Combine(directory, "cancelled"))) throw new IOException("Mise à jour annulée.");
        }
        finally { parent?.Dispose(); }
    }
    private static Process Start(string target, Guid? updated, bool resume)
    {
        var info = new ProcessStartInfo(target) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
        if (updated is { } id) { info.ArgumentList.Add("--updated"); info.ArgumentList.Add(id.ToString()); }
        if (resume) info.ArgumentList.Add("--resume-monitoring");
        return Process.Start(info) ?? throw new IOException("Impossible de redémarrer l’application.");
    }
    internal static void SignalHealthy(Guid id)
    {
        try { using var signal = EventWaitHandle.OpenExisting(HealthyName(id)); signal.Set(); }
        catch (WaitHandleCannotBeOpenedException) { }
    }
    internal static UpdateOutcome? TakeOutcome()
    {
        var path = AppStorage.FilePath("last-update.json");
        if (!File.Exists(path)) return null;
        try
        {
            var outcome = JsonSerializer.Deserialize<UpdateOutcome>(File.ReadAllText(path));
            File.Delete(path);
            return outcome;
        }
        catch (IOException) { return null; }
        catch (JsonException) { return null; }
    }
}
