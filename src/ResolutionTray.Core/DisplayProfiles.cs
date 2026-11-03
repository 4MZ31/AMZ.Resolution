namespace ResolutionTray.Core;

/// <summary>Shared validation for presets, custom dimensions and native progressive refresh rates.</summary>
public static class DisplayProfiles
{
    public static IReadOnlyList<ResolutionSize> Presets { get; } = Array.AsReadOnly<ResolutionSize>([
        new(2324, 1306), new(2304, 1296), new(2272, 1278), new(2240, 1260),
        new(1920, 1080), new(1920, 1440), new(1600, 1200), new(1280, 960), new(2560, 1440)
    ]);

    public static int[] NativeRefreshRates(IEnumerable<DisplayMode> modes, DisplayTarget display) => modes
        .Where(m => m.Width == display.NativeWidth && m.Height == display.NativeHeight
            && m.BitsPerPixel == display.CurrentMode.BitsPerPixel && m.Orientation == 0 && m.Flags == 0
            && m.RefreshRate is >= 24 and <= 1000)
        .Select(m => m.RefreshRate).Distinct().OrderDescending().ToArray();

    public static DisplayMode Resolve(Profile profile, DisplayTarget display)
    {
        if (profile.DisplayIdentity != display.Identity)
            throw new InvalidOperationException("L’écran sélectionné a changé.");
        if (display.NativeWidth != 2560 || display.NativeHeight != 1440 || display.CurrentMode.Orientation != 0)
            throw new InvalidOperationException("Sélectionnez un écran natif 2560 × 1440 en orientation paysage.");
        if (profile.Width < 640 || profile.Height < 480 || profile.Width > display.NativeWidth || profile.Height > display.NativeHeight)
            throw new InvalidOperationException("La résolution doit être comprise entre 640 × 480 et 2560 × 1440, sans dépasser la dalle.");
        var hz = profile.RefreshRate == 0 ? display.CurrentMode.RefreshRate : profile.RefreshRate;
        if (hz is < 24 or > 1000 || display.RefreshRates is { } rates && !rates.Contains(hz))
            throw new InvalidOperationException("Cette fréquence n’est pas proposée par l’écran en 2560 × 1440. Choisissez une fréquence détectée.");
        return display.CurrentMode with { Width = profile.Width, Height = profile.Height, RefreshRate = hz, Orientation = 0, Flags = 0 };
    }

    public static string Fingerprint(DisplayTarget display, DisplayMode mode) =>
        $"{display.Identity}|{mode.Width}x{mode.Height}|{mode.RefreshRate}|{mode.BitsPerPixel}|{display.DriverVersion}";
}
