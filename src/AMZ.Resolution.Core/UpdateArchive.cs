using System.IO.Compression;
using System.Security.Cryptography;

namespace AMZ.Resolution.Core;

public static class UpdateArchive
{
    public const long MaxExecutableBytes = 250 * 1024 * 1024;
    public static string ParseChecksum(string text, string archiveName)
    {
        var parts = text.Trim('\uFEFF', ' ', '\r', '\n', '\t').Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || parts[1].TrimStart('*') != archiveName || !IsHash(parts[0]))
            throw new InvalidDataException("Le fichier SHA-256 de la mise à jour est invalide.");
        return parts[0].ToLowerInvariant();
    }
    public static bool IsHash(string value) => value.Length == 64 && value.All(Uri.IsHexDigit);
    public static string HashFile(string file)
    {
        using var stream = File.OpenRead(file);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
    public static bool MatchesHash(string file, string hash) => IsHash(hash)
        && string.Equals(HashFile(file), hash, StringComparison.OrdinalIgnoreCase);

    public static async Task<string> ExtractAsync(string archivePath, string expectedHash, string directory, CancellationToken cancellation)
    {
        if (new FileInfo(archivePath).Length > UpdateRelease.MaxArchiveBytes) throw new InvalidDataException("Archive trop volumineuse.");
        await using (var input = File.OpenRead(archivePath))
        {
            var actual = Convert.ToHexString(await SHA256.HashDataAsync(input, cancellation));
            if (!IsHash(expectedHash) || !actual.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Le SHA-256 de la mise à jour ne correspond pas. Installation annulée.");
        }
        using var archive = ZipFile.OpenRead(archivePath);
        var entries = archive.Entries.Where(e => e.FullName == "AMZ.Resolution.exe").ToArray();
        if (entries.Length != 1 || entries[0].Length < 128 || entries[0].Length > MaxExecutableBytes)
            throw new InvalidDataException("L’archive ne contient pas un exécutable valide.");
        Directory.CreateDirectory(directory);
        var output = Path.Combine(directory, "payload.exe");
        var completed = false;
        var created = false;
        try
        {
            await using (var source = entries[0].Open())
            await using (var destination = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                created = true;
                var buffer = new byte[81920]; long total = 0; int read;
                while ((read = await source.ReadAsync(buffer, cancellation)) > 0)
                {
                    total += read;
                    if (total > MaxExecutableBytes || total > entries[0].Length) throw new InvalidDataException("Exécutable trop volumineux.");
                    await destination.WriteAsync(buffer.AsMemory(0, read), cancellation);
                }
                if (total != entries[0].Length) throw new InvalidDataException("Téléchargement incomplet.");
            }
            if (!IsWindowsX64Executable(output)) throw new InvalidDataException("Le fichier reçu n’est pas un exécutable Windows x64.");
            completed = true;
            return output;
        }
        finally { if (!completed && created && File.Exists(output)) File.Delete(output); }
    }
    private static bool IsWindowsX64Executable(string file)
    {
        using var input = File.OpenRead(file);
        using var reader = new BinaryReader(input);
        if (input.Length < 128 || reader.ReadUInt16() != 0x5A4D) return false;
        input.Position = 0x3C;
        var offset = reader.ReadInt32();
        if (offset < 64 || offset > input.Length - 6) return false;
        input.Position = offset;
        return reader.ReadUInt32() == 0x00004550 && reader.ReadUInt16() == 0x8664;
    }
}
