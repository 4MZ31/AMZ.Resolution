using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using ResolutionTray.Core;

namespace ResolutionTray.Native;

/// <summary>
/// Minimal x64 binding to the published ADLX C ABI. Slots and layouts follow AMD SDK
/// commit 32b5a740d42295c5dfe9026b9f52683da0f3af91, SDK/Include.
/// Every acquired interface is released before ADLXTerminate and DLL unload.
/// </summary>
internal sealed class AdlxClient : IDisposable
{
    private nint library;
    private nint system;
    private Obj? services;
    private TerminateFn? terminate;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int VersionFn(out ulong version);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int InitFn(ulong version, out nint system);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int TerminateFn();
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] internal delegate int GetObject(nint self, out nint value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetSetting(nint self, nint display, out nint value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetAt(nint self, uint index, out nint value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate uint GetSize(nint self);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int ReleaseFn(nint self);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetBool(nint self, out byte value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int SetBool(nint self, byte value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetInt(nint self, out int value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int SetInt(nint self, int value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetPair(nint self, out int width, out int height);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetResolution(nint self, out CustomResolution value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int SetResolution(nint self, CustomResolution value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int UseResolution(nint self, nint resolution);

    [StructLayout(LayoutKind.Sequential)]
    private struct Timing
    {
        public int Flags, HTotal, VTotal, HDisplay, VDisplay, HFrontPorch, VFrontPorch;
        public int HSyncWidth, VSyncWidth, HPolarity, VPolarity;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct CustomResolution
    {
        public int Width, Height, RefreshRate, Presentation, TimingStandard, PixelClock;
        public Timing Detailed;
    }

    internal sealed class Obj(nint pointer) : IDisposable
    {
        public nint Pointer { get; private set; } = pointer != 0 ? pointer : throw new InvalidOperationException("Interface AMD vide.");
        public T Fn<T>(int slot) where T : Delegate => Function<T>(Pointer, slot);
        public void Dispose()
        {
            if (Pointer == 0) return;
            Fn<ReleaseFn>(1)(Pointer);
            Pointer = 0;
        }
    }

    private static T Function<T>(nint pointer, int slot) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(pointer), slot * nint.Size));

    public AdlxClient()
    {
        try
        {
            library = LoadDriverLibrary();
            var query = Marshal.GetDelegateForFunctionPointer<VersionFn>(NativeLibrary.GetExport(library, "ADLXQueryFullVersion"));
            Check(query(out var version), "Version ADLX");
            var initialize = Marshal.GetDelegateForFunctionPointer<InitFn>(NativeLibrary.GetExport(library, "ADLXInitialize"));
            terminate = Marshal.GetDelegateForFunctionPointer<TerminateFn>(NativeLibrary.GetExport(library, "ADLXTerminate"));
            Check(initialize(version, out system), "Initialisation ADLX");
            Check(Function<GetObject>(system, 3)(system, out var pointer), "Service d'affichage AMD");
            services = new Obj(pointer);
        }
        catch { Dispose(); throw; }
    }

    // Read only registered, active AMD driver paths; never scan old DriverStore packages.
    private static nint LoadDriverLibrary()
    {
        var candidates = new List<string> { Path.Combine(Environment.SystemDirectory, "amdadlx64.dll") };
        using var adapters = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}");
        if (adapters is not null)
            foreach (var name in adapters.GetSubKeyNames().Where(n => int.TryParse(n, out _)))
            {
                using var adapter = adapters.OpenSubKey(name);
                if (adapter?.GetValue("DriverDesc") is not string description || !description.Contains("AMD", StringComparison.OrdinalIgnoreCase)) continue;
                var value = adapter.GetValue("UserModeDriverName");
                var paths = value is string[] list ? list : value is string single ? [single] : Array.Empty<string>();
                foreach (var item in paths)
                {
                    var dir = Path.GetDirectoryName(item);
                    if (dir is null || !Path.GetFullPath(dir).StartsWith(
                        Path.Combine(Environment.SystemDirectory, "DriverStore") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) continue;
                    candidates.Add(Path.Combine(dir, "amdadlx64.dll"));
                }
            }
        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase).Where(File.Exists))
        {
            var handle = Win32.LoadLibraryEx(candidate, 0, 0x100 | 0x800); // DLL_LOAD_DIR | SYSTEM32
            if (handle != 0) return handle;
        }
        throw new InvalidOperationException("AMD ADLX est introuvable. Installez le pilote AMD Software: Adrenalin Edition complet.");
    }

    public IReadOnlyList<(string Edid, string Name, int Width, int Height)> GetDisplays()
    {
        using var list = GetList();
        var result = new List<(string, string, int, int)>();
        for (uint i = 0; i < list.Fn<GetSize>(3)(list.Pointer); i++)
        {
            using var display = At(list, i);
            Check(display.Fn<GetPair>(8)(display.Pointer, out var w, out var h), "Résolution native AMD");
            result.Add((ReadString(display, 7), ReadString(display, 6), w, h));
        }
        return result;
    }

    public Obj FindDisplay(string edid)
    {
        using var list = GetList();
        Obj? found = null;
        try
        {
            for (uint i = 0; i < list.Fn<GetSize>(3)(list.Pointer); i++)
            {
                var display = At(list, i);
                if (NormalizeEdid(ReadString(display, 7)) != NormalizeEdid(edid)) { display.Dispose(); continue; }
                if (found is not null) { display.Dispose(); throw new InvalidOperationException("Plusieurs écrans ont le même EDID. Identification AMD ambiguë."); }
                found = display;
            }
            return found ?? throw new InvalidOperationException("L'écran Windows n'a pas été retrouvé dans AMD ADLX.");
        }
        catch { found?.Dispose(); throw; }
    }

    internal static string NormalizeEdid(string edid)
    {
        var hex = new string(edid.Where(Uri.IsHexDigit).ToArray()).ToUpperInvariant();
        return hex.Length >= 256 ? hex[..256] : hex;
    }

    private Obj GetList()
    {
        Check(services!.Fn<GetObject>(4)(services.Pointer, out var pointer), "Liste d'écrans AMD");
        return new Obj(pointer);
    }
    private static Obj At(Obj list, uint index)
    {
        Check(list.Fn<GetAt>(11)(list.Pointer, index, out var pointer), "Écran AMD");
        return new Obj(pointer);
    }
    private static string ReadString(Obj obj, int slot)
    {
        Check(obj.Fn<GetObject>(slot)(obj.Pointer, out var pointer), "Information écran AMD");
        return Marshal.PtrToStringAnsi(pointer) ?? "";
    }
    private Obj Setting(Obj display, int slot)
    {
        Check(services!.Fn<GetSetting>(slot)(services.Pointer, display.Pointer, out var pointer), "Réglage écran AMD");
        return new Obj(pointer);
    }
    private static bool ReadBool(Obj obj, int slot)
    {
        Check(obj.Fn<GetBool>(slot)(obj.Pointer, out var value), "Lecture AMD");
        return value != 0;
    }
    private static void WriteBool(Obj obj, bool value) => Check(obj.Fn<SetBool>(5)(obj.Pointer, value ? (byte)1 : (byte)0), "Mise à l'échelle AMD");

    public ScalingState GetScaling(Obj display)
    {
        using var gpu = Setting(display, 11);
        using var scale = Setting(display, 12);
        if (!ReadBool(gpu, 3) || !ReadBool(scale, 3)) throw new InvalidOperationException("Le centrage GPU n'est pas pris en charge sur cet écran.");
        var enabled = ReadBool(gpu, 4);
        Check(scale.Fn<GetInt>(4)(scale.Pointer, out var mode), "Mode de mise à l'échelle");
        bool? integer = null;
        using var ints = Setting(display, 13);
        if (ReadBool(ints, 3)) integer = ReadBool(ints, 4);
        return new ScalingState(enabled, mode, integer);
    }

    public void SetScaling(Obj display, ScalingState state)
    {
        using var gpu = Setting(display, 11);
        using var scale = Setting(display, 12);
        using var ints = Setting(display, 13);
        // Scaling controls can require GPU scaling to be enabled even when restoring disabled state.
        WriteBool(gpu, true);
        if (ReadBool(ints, 3)) WriteBool(ints, false);
        Check(scale.Fn<SetInt>(5)(scale.Pointer, state.Mode), "Centrage AMD");
        if (state.IntegerScaling is true) WriteBool(ints, true);
        WriteBool(gpu, state.GpuScaling);
    }

    public void CreateMode(Obj display, DisplayMode requested)
    {
        using var custom = Setting(display, 18);
        if (!ReadBool(custom, 3)) throw new InvalidOperationException("Les résolutions personnalisées AMD ne sont pas prises en charge sur cet écran.");
        Check(custom.Fn<GetObject>(5)(custom.Pointer, out var pointer), "Timings actuels AMD");
        using var resolution = new Obj(pointer);
        Check(resolution.Fn<GetResolution>(3)(pointer, out var value), "Lecture des timings AMD");
        if (value.Detailed.HTotal <= requested.Width || value.Detailed.VTotal <= requested.Height)
            throw new InvalidOperationException("Les timings actuels ne permettent pas de créer ce mode. Créez-le dans Adrenalin.");
        // Preserve native totals/porches instead of CVT rounding 2324 to a multiple of 8.
        // The unchanged totals and clock retain the current signal refresh rate.
        if (value.RefreshRate != requested.RefreshRate)
            throw new InvalidOperationException("Réglez d'abord la fréquence du bureau sur la fréquence souhaitée, puis réessayez.");
        value.Width = requested.Width;
        value.Height = requested.Height;
        value.Presentation = 0;
        value.TimingStandard = 4; // MANUAL
        value.Detailed.HDisplay = requested.Width;
        value.Detailed.VDisplay = requested.Height;
        Check(resolution.Fn<SetResolution>(4)(pointer, value), "Préparation du mode personnalisé");
        Check(custom.Fn<UseResolution>(6)(custom.Pointer, pointer), "Création de la résolution AMD");
    }

    private static void Check(int result, string operation)
    {
        if (result is < 0 or > 2) throw new InvalidOperationException($"{operation} : erreur ADLX {result}.");
    }
    public void Dispose()
    {
        services?.Dispose(); services = null;
        if (system != 0) { terminate?.Invoke(); system = 0; }
        if (library != 0) { NativeLibrary.Free(library); library = 0; }
    }
}
