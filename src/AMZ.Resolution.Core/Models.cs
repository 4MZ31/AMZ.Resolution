namespace AMZ.Resolution.Core;

public sealed record DisplayMode(int Width, int Height, int RefreshRate, int BitsPerPixel = 32,
    int Orientation = 0, int Flags = 0, int FixedOutput = 0);
public sealed record DisplayTarget(string Identity, string DeviceName, string Name,
    int NativeWidth, int NativeHeight, DisplayMode CurrentMode, string DriverVersion = "", int[]? RefreshRates = null);
public sealed record ScalingState(bool GpuScaling, int Mode, bool? IntegerScaling);
public sealed record ResolutionSize(int Width, int Height)
{
    public override string ToString() => $"{Width} × {Height}";
}
public sealed record Profile(string DisplayIdentity, int RefreshRate = 0, int Width = 2324, int Height = 1306);
public sealed record RecoverySession(Guid Id, string DisplayIdentity, DisplayMode OriginalMode,
    ScalingState OriginalScaling, DisplayMode RequestedMode, bool Confirmed = false);

public interface IDisplayBackend
{
    IReadOnlyList<DisplayTarget> GetDisplays();
    ScalingState GetScaling(string identity);
    void EnsureCustomMode(string identity, DisplayMode mode);
    void SetCentered(string identity);
    void TestMode(string identity, DisplayMode mode);
    void ApplyMode(string identity, DisplayMode mode);
    bool Matches(string identity, DisplayMode mode);
    bool IsCentered(string identity);
    void Restore(RecoverySession session);
}
public interface IRecoveryStore
{
    RecoverySession? Load();
    void Save(RecoverySession session);
    void Delete();
}
public interface ISafetyMonitor { void Arm(Guid sessionId); }
