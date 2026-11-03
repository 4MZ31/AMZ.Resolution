# AMD Resolution Tray

[Documentation française](../README.md)

A portable Windows tray utility for an exact **2324 × 1306 centered desktop** on a native **2560 × 1440** display connected to an AMD Radeon GPU.

**Green = active. Gray = inactive. Left double-click toggles the profile; right-click opens the menu.** On a 27-inch panel this produces an approximately **24.5-inch** image with 118-pixel left/right and 67-pixel top/bottom margins when displayed pixel for pixel. This is not exactly 24 inches or exactly 16:9.

## Get started

1. Download the Windows x64 ZIP from [Releases](https://github.com/4MZ31/amd-resolution-tray/releases), extract it to a permanent folder and run ResolutionTray.exe. The portable release includes the .NET runtime.
2. Select your native 1440p display and choose **Enregistrer et activer**. The current refresh rate is preserved.
3. Confirm the first activation within 15 seconds; otherwise the previous display settings are restored automatically.
4. Use the tray icon (possibly hidden under Windows' **^** menu) to toggle. **Désactiver et restaurer** disables the profile; **Quitter** restores settings and exits.

The initial confirmation is remembered for that display, refresh rate and driver version. Startup with Windows is optional and starts inactive. The interface is currently French and the executable is unsigned.

## Requirements and scope

Windows 11 x64, AMD Radeon with an Adrenalin driver providing ADLX, and a landscape native 2560 × 1440 monitor directly connected to that GPU. The exact mode must be accepted by the driver and monitor. Extended desktop is supported; cloned displays and Eyefinity are excluded. Identical EDIDs are rejected when display identity would be ambiguous. Other connected displays may have different native resolutions.

The application reuses an existing exact mode or creates one through AMD ADLX using the current signal timings. It enables GPU scaling in Centered mode and disables integer scaling if available. Creating a custom mode is separate from applying it; the custom mode remains available after exit. If automatic creation fails, create an accepted mode manually in Adrenalin and retry. No approximate resolution or silent refresh-rate fallback is applied.

Activate before launching CS2 and verify its video settings: a game can select its own display mode. External resolution/scaling changes turn the tray icon gray while keeping restoration available. Physical image size, game behavior, HDR and FreeSync require testing on your setup.

## Recovery and privacy

Original display and scaling settings are stored before any driver mutation. A separate process attempts restoration if the main process crashes. Complete Windows shutdown, driver failure and disconnected monitors can prevent immediate recovery; the journal is retained for the next launch. Do not delete recovery.json until the display is restored.

Settings, recovery data and local error logs live in **%LOCALAPPDATA%\AmdResolutionTray**. There is no telemetry. A read-only diagnostic can be generated with:

~~~powershell
.\ResolutionTray.exe --diagnose "$env:TEMP\resolution-tray.json"
~~~

Diagnostics include monitor identifiers; remove them before posting a public issue. Use --settings to reopen settings and --quit to request a graceful exit. To uninstall, disable startup in settings, exit and remove the extracted folder. The custom AMD mode can be removed separately in Adrenalin.

## Build and validation

.NET 10 SDK on Windows x64; no redistributed AMD binaries or external NuGet dependencies.

~~~powershell
dotnet build ResolutionTray.slnx -c Release
dotnet run --project tests/ResolutionTray.Tests -c Release
.\build.ps1
~~~

Close the app before rebuilding into the same output folder. build.ps1 runs deterministic tests, publishes a self-contained executable and creates a ZIP plus SHA-256 checksum under artifacts/. GitHub Actions performs the same checks on Windows.

Experimental first version: mode creation/application and automatic restoration were verified on a Radeon RX 7800 XT with a MSI MAG272CQR at 144 Hz. Two other connected displays retained their original settings after restoration. This does not establish compatibility with every monitor or validate CS2 rendering.

[MIT license](../LICENSE). Independent project, not affiliated with AMD or Valve. See the [French documentation](../README.md) for implementation details and troubleshooting.
