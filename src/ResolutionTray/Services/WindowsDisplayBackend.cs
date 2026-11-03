using System.Runtime.InteropServices;
using Microsoft.Win32;
using ResolutionTray.Core;
using ResolutionTray.Native;

namespace ResolutionTray.Services;

internal sealed class WindowsDisplayBackend : IDisplayBackend
{
    private sealed record Monitor(string Identity, string DeviceName, string Edid, string DriverVersion);
    private readonly object sync = new();

    public IReadOnlyList<DisplayTarget> GetDisplays()
    {
        lock (sync)
        {
            using var amd = new AdlxClient();
            var amdDisplays = amd.GetDisplays();
            return EnumerateMonitors().Select(m =>
            {
                var matches = amdDisplays.Where(a => AdlxClient.NormalizeEdid(a.Edid) == m.Edid).ToArray();
                var match = matches.Length == 1 ? matches[0] : default;
                var target = new DisplayTarget(m.Identity, m.DeviceName,
                    match.Name ?? "Écran non identifié", match.Width, match.Height, ReadMode(m.DeviceName), m.DriverVersion);
                return target with { RefreshRates = DisplayProfiles.NativeRefreshRates(ReadModes(m.DeviceName), target) };
            }).ToArray();
        }
    }

    public ScalingState GetScaling(string identity) => WithDisplay(identity, (amd, display) => amd.GetScaling(display));
    public bool IsCentered(string identity)
    {
        var s = GetScaling(identity);
        return s.GpuScaling && s.Mode == 2 && s.IntegerScaling is not true;
    }
    public void SetCentered(string identity) => WithDisplay(identity, (amd, display) =>
    {
        var state = amd.GetScaling(display);
        amd.SetScaling(display, new ScalingState(true, 2, state.IntegerScaling is null ? null : false));
        return true;
    });

    public void EnsureCustomMode(string identity, DisplayMode mode)
    {
        lock (sync)
        {
            var monitor = FindMonitor(identity);
            if (FindMode(monitor.DeviceName, mode) is not null) return;
            // Seed AMD timings from a real native mode at the requested frequency.
            // The controller has already journaled the original desktop and armed its watchdog.
            var target = GetDisplays().Single(d => d.Identity == identity);
            var reference = mode with { Width = target.NativeWidth, Height = target.NativeHeight };
            if (FindMode(monitor.DeviceName, reference) is null)
                throw new InvalidOperationException("Aucun mode natif compatible à cette fréquence. Aucun timing ne sera inventé.");
            if (!SameMode(ReadMode(monitor.DeviceName), reference))
            {
                TestMode(identity, reference);
                ApplyMode(identity, reference);
                if (!SameMode(ReadMode(monitor.DeviceName), reference))
                    throw new InvalidOperationException("La fréquence de référence n’a pas été appliquée.");
            }
            WithDisplay(identity, (amd, display) => { amd.CreateMode(display, mode); return true; });
            // Driver mode enumeration can lag behind ADLX's successful creation result.
            for (var i = 0; i < 30; i++)
            {
                if (FindMode(monitor.DeviceName, mode) is not null) return;
                Thread.Sleep(100);
            }
            throw new InvalidOperationException($"Le mode exact {mode.Width} × {mode.Height} à {mode.RefreshRate} Hz n’est pas exposé par Windows. Aucun mode approchant ne sera appliqué.");
        }
    }

    public void TestMode(string identity, DisplayMode mode) => ChangeMode(identity, mode, 2);
    public void ApplyMode(string identity, DisplayMode mode) => ChangeMode(identity, mode, 0);

    private void ChangeMode(string identity, DisplayMode mode, uint flags)
    {
        lock (sync)
        {
            var monitor = FindMonitor(identity);
            var native = FindMode(monitor.DeviceName, mode)
                ?? throw new InvalidOperationException($"Mode indisponible : {mode.Width} × {mode.Height} à {mode.RefreshRate} Hz.");
            // No registry update and no DM_POSITION: change only the selected display's mode.
            native.FixedOutput = (uint)mode.FixedOutput;
            native.Fields = 0x40000 | 0x80000 | 0x100000 | 0x200000 | 0x400000 | 0x80 | 0x20000000;
            var result = Win32.ChangeDisplaySettingsEx(monitor.DeviceName, ref native, 0, flags, 0);
            if (result != 0) throw new InvalidOperationException($"Windows a refusé le changement d'affichage (code {result}).");
        }
    }

    public bool Matches(string identity, DisplayMode mode)
    {
        lock (sync)
        {
            var monitor = EnumerateMonitors().SingleOrDefault(m => m.Identity == identity);
            if (monitor is null) return false;
            return SameMode(ReadMode(monitor.DeviceName), mode);
        }
    }

    public void Restore(RecoverySession session)
    {
        var errors = new List<Exception>();
        try { ApplyMode(session.DisplayIdentity, session.OriginalMode); }
        catch (Exception ex) { errors.Add(ex); }
        try
        {
            WithDisplay(session.DisplayIdentity, (amd, display) =>
            {
                amd.SetScaling(display, session.OriginalScaling);
                return true;
            });
        }
        catch (Exception ex) { errors.Add(ex); }
        if (errors.Count != 0) throw new AggregateException("La restauration de l'affichage a échoué.", errors);
    }

    private T WithDisplay<T>(string identity, Func<AdlxClient, AdlxClient.Obj, T> action)
    {
        lock (sync)
        {
            var monitor = FindMonitor(identity);
            using var amd = new AdlxClient();
            using var display = amd.FindDisplay(monitor.Edid);
            return action(amd, display);
        }
    }

    private static Monitor FindMonitor(string identity) => EnumerateMonitors().SingleOrDefault(m => m.Identity == identity)
        ?? throw new InvalidOperationException("L'écran sélectionné est déconnecté ou son identification a changé.");

    private static IReadOnlyList<Monitor> EnumerateMonitors()
    {
        var result = new List<Monitor>();
        for (uint i = 0; ; i++)
        {
            var adapter = Win32.DisplayDevice.Create();
            if (!Win32.EnumDisplayDevices(null, i, ref adapter, 0)) break;
            if ((adapter.StateFlags & 1) == 0 || (adapter.StateFlags & 8) != 0) continue;
            using var adapterKey = Registry.LocalMachine.OpenSubKey(adapter.DeviceKey.Replace(@"\Registry\Machine\", "", StringComparison.OrdinalIgnoreCase));
            var driverVersion = adapterKey?.GetValue("DriverVersion") as string ?? "";
            var attached = new List<Monitor>();
            for (uint j = 0; ; j++)
            {
                var monitor = Win32.DisplayDevice.Create();
                if (!Win32.EnumDisplayDevices(adapter.DeviceName, j, ref monitor, 1)) break;
                if ((monitor.StateFlags & 1) == 0) continue;
                var parts = monitor.DeviceId.Split('#');
                if (parts.Length < 3) continue;
                var instance = @"DISPLAY\" + parts[1] + @"\" + parts[2];
                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\" + instance + @"\Device Parameters");
                if (key?.GetValue("EDID") is not byte[] edid || edid.Length < 128) continue;
                attached.Add(new Monitor(monitor.DeviceId.ToUpperInvariant(), adapter.DeviceName, Convert.ToHexString(edid.AsSpan(0, 128)), driverVersion));
            }
            // A cloned Windows source maps to multiple physical monitors: exclude it.
            if (attached.Count == 1) result.Add(attached[0]);
        }
        return result;
    }

    private static DisplayMode ReadMode(string device)
    {
        var native = Win32.DevMode.Create();
        if (!Win32.EnumDisplaySettingsEx(device, -1, ref native, 0)) throw new InvalidOperationException("Impossible de lire le mode actuel de l'écran.");
        return FromNative(native);
    }
    private static IEnumerable<DisplayMode> ReadModes(string device)
    {
        for (var i = 0; ; i++)
        {
            var native = Win32.DevMode.Create();
            if (!Win32.EnumDisplaySettingsEx(device, i, ref native, 0)) yield break;
            yield return FromNative(native);
        }
    }
    private static Win32.DevMode? FindMode(string device, DisplayMode requested)
    {
        for (var i = 0; ; i++)
        {
            var native = Win32.DevMode.Create();
            if (!Win32.EnumDisplaySettingsEx(device, i, ref native, 0)) return null;
            if (SameMode(FromNative(native), requested)) return native;
        }
    }
    private static bool SameMode(DisplayMode a, DisplayMode b) => a.Width == b.Width && a.Height == b.Height
        && a.RefreshRate == b.RefreshRate && a.BitsPerPixel == b.BitsPerPixel && a.Orientation == b.Orientation && a.Flags == b.Flags;
    private static DisplayMode FromNative(Win32.DevMode m) => new((int)m.PelsWidth, (int)m.PelsHeight,
        (int)m.DisplayFrequency, (int)m.BitsPerPel, (int)m.Orientation, (int)m.DisplayFlags, (int)m.FixedOutput);

    internal static void VerifyLayouts()
    {
        if (Marshal.SizeOf<Win32.DevMode>() != 220 || Marshal.SizeOf<Win32.DisplayDevice>() != 840)
            throw new InvalidOperationException("La disposition mémoire des structures Windows est incorrecte.");
    }
}
