using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace AMZ.Resolution.Services;

/// <summary>Trust the same certificate as the running, Windows-verified application. Never trust metadata alone.</summary>
internal static class UpdateTrust
{
    private static readonly Lazy<string?> Publisher = new(() =>
    {
        try { return VerifiedCertificateHash(Environment.ProcessPath!); }
        catch (Exception ex) when (ex is CryptographicException or IOException or UnauthorizedAccessException) { return null; }
    });
    internal static bool CanAuthenticate => Publisher.Value is not null;
    internal static void Verify(string file)
    {
        var expected = Publisher.Value ?? throw new InvalidDataException("L’éditeur de cette installation n’est pas signé. Téléchargez la version depuis GitHub.");
        if (!string.Equals(expected, VerifiedCertificateHash(file), StringComparison.Ordinal))
            throw new InvalidDataException("La signature de la mise à jour n’appartient pas à l’éditeur autorisé.");
    }
    private static string VerifiedCertificateHash(string path)
    {
        var file = new TrustFile { Size = (uint)Marshal.SizeOf<TrustFile>(), Path = path };
        var pointer = Marshal.AllocHGlobal(Marshal.SizeOf<TrustFile>());
        Marshal.StructureToPtr(file, pointer, false);
        var data = new TrustData { Size = (uint)Marshal.SizeOf<TrustData>(), Ui = 2, Union = 1,
            File = pointer, StateAction = 1, Flags = 0x80 }; // chain revocation excluding root
        var action = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
        try
        {
            if (WinVerifyTrust(new nint(-1), ref action, ref data) != 0)
                throw new CryptographicException("Signature Authenticode absente, invalide ou non vérifiable.");
            // X509CertificateLoader cannot extract the signer from a PE container.
            // Windows has verified the Authenticode signature above; use this API only for its signer certificate.
#pragma warning disable SYSLIB0057
            using var certificate = X509Certificate.CreateFromSignedFile(path);
#pragma warning restore SYSLIB0057
            return certificate.GetCertHashString(HashAlgorithmName.SHA256);
        }
        finally
        {
            data.StateAction = 2;
            _ = WinVerifyTrust(new nint(-1), ref action, ref data);
            Marshal.DestroyStructure<TrustFile>(pointer);
            Marshal.FreeHGlobal(pointer);
        }
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct TrustFile
    {
        public uint Size;
        [MarshalAs(UnmanagedType.LPWStr)] public string Path;
        public nint File, KnownSubject;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct TrustData
    {
        public uint Size;
        public nint Policy, Sip;
        public uint Ui, Revocation, Union;
        public nint File;
        public uint StateAction;
        public nint State, Url;
        public uint Flags, Context;
        public nint SignatureSettings;
    }
    [DllImport("wintrust.dll", ExactSpelling = true)]
    private static extern int WinVerifyTrust(nint window, ref Guid action, ref TrustData data);
}
