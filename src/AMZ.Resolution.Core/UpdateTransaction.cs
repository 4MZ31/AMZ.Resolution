namespace AMZ.Resolution.Core;

/// <summary>Swaps on the destination volume and retains the old executable until startup is acknowledged.</summary>
public static class UpdateTransaction
{
    public static void Install(string target, string payload, string oldHash, string newHash, Func<bool> startAndVerify)
    {
        if (!UpdateArchive.MatchesHash(target, oldHash)) throw new IOException("L’exécutable installé a changé. Mise à jour annulée.");
        if (!UpdateArchive.MatchesHash(payload, newHash)) throw new InvalidDataException("L’exécutable téléchargé a changé.");
        var token = Guid.NewGuid().ToString("N");
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
            File.Replace(candidate, target, backup);
            swapped = true;
            if (!startAndVerify()) throw new IOException("La nouvelle version n’a pas confirmé son démarrage.");
            success = true;
        }
        catch (Exception installationError)
        {
            if (swapped)
            {
                try { File.Replace(backup, target, null); }
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
            if (success) TryDelete(backup);
        }
    }
    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
