using System.Text.Json;

namespace AMZ.Resolution.Core;

/// <summary>Swaps on the destination volume and retains the old executable until startup is acknowledged.</summary>
public static class UpdateTransaction
{
    private sealed record Journal(Guid Id, string OldHash, string NewHash, bool Committed = false);

    /// <summary>Called by the old helper, not by the executable being replaced.</summary>
    public static void Recover(string target)
    {
        var path = target + ".update.json";
        if (!File.Exists(path)) return;
        if (new FileInfo(path).Length > 16_384 || IsLink(path)) throw new InvalidDataException("Journal de mise à jour invalide.");
        var journal = JsonSerializer.Deserialize<Journal>(File.ReadAllText(path)) ?? throw new InvalidDataException("Journal vide.");
        if (journal.Id == Guid.Empty || !UpdateArchive.IsHash(journal.OldHash) || !UpdateArchive.IsHash(journal.NewHash))
            throw new InvalidDataException("Journal de mise à jour invalide.");
        var backup = target + "." + journal.Id.ToString("N") + ".backup";
        var candidate = target + "." + journal.Id.ToString("N") + ".new";
        if (IsLink(target) || IsLink(backup) || IsLink(candidate)) throw new IOException("Lien inattendu dans la transaction.");
        bool Matches(string file, string hash) => File.Exists(file) && UpdateArchive.MatchesHash(file, hash);
        if (journal.Committed && Matches(target, journal.NewHash) || Matches(target, journal.OldHash))
        {
            if (File.Exists(backup) && !Matches(backup, journal.OldHash)) throw new IOException("Sauvegarde modifiée ; fichiers conservés.");
        }
        else
        {
            if (!Matches(backup, journal.OldHash)) throw new IOException("Sauvegarde vérifiée indisponible ; transaction conservée.");
            if (File.Exists(target)) File.Replace(backup, target, null);
            else File.Move(backup, target);
        }
        TryDelete(candidate); TryDelete(backup);
        File.Delete(path);
    }
    private static bool IsLink(string path) => File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
    private static void SaveJournal(string target, Journal journal)
    {
        var path = target + ".update.json";
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(output, journal);
                output.Flush(true);
            }
            File.Move(temporary, path, true);
        }
        finally { TryDelete(temporary); }
    }
    public static void Install(string target, string payload, string oldHash, string newHash, Func<bool> startAndVerify)
    {
        Recover(target);
        if (!UpdateArchive.MatchesHash(target, oldHash)) throw new IOException("L’exécutable installé a changé. Mise à jour annulée.");
        if (!UpdateArchive.MatchesHash(payload, newHash)) throw new InvalidDataException("L’exécutable téléchargé a changé.");
        var journal = new Journal(Guid.NewGuid(), oldHash, newHash);
        var token = journal.Id.ToString("N");
        var candidate = target + "." + token + ".new";
        var backup = target + "." + token + ".backup";
        var swapped = false;
        var success = false;
        try
        {
            using (var input = File.OpenRead(payload))
            using (var output = new FileStream(candidate, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.WriteThrough))
            {
                input.CopyTo(output);
                output.Flush(true);
            }
            if (!UpdateArchive.MatchesHash(candidate, newHash) || !UpdateArchive.MatchesHash(target, oldHash))
                throw new IOException("Un fichier de la mise à jour a changé avant son installation.");
            SaveJournal(target, journal);
            File.Replace(candidate, target, backup);
            swapped = true;
            if (!startAndVerify()) throw new IOException("La nouvelle version n’a pas confirmé son démarrage.");
            SaveJournal(target, journal with { Committed = true });
            success = true;
        }
        catch (Exception installationError)
        {
            if (swapped)
            {
                try { File.Replace(backup, target, null); File.Delete(target + ".update.json"); }
                catch (Exception rollbackError)
                {
                    throw new AggregateException($"Restauration automatique impossible. Sauvegarde : {backup}", installationError, rollbackError);
                }
            }
            throw;
        }
        finally
        {
            TryDelete(candidate);
            if (success) { TryDelete(backup); TryDelete(target + ".update.json"); }
        }
    }
    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
