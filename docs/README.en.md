# AMD Resolution Tray

[Documentation française](../README.md)

A portable Windows tray utility for an exact **2324 × 1306 centered desktop** on a native **2560 × 1440** AMD-connected display, applied **only while at least one selected game is running** and monitoring is enabled.

**Green = resolution applied in game. Gray = monitoring disabled or waiting for the game. Left double-click toggles monitoring; right-click opens the menu.** On a 27-inch panel this produces an approximately **24.5-inch** image with 118-pixel left/right and 67-pixel top/bottom margins when displayed pixel for pixel. This is not exactly 24 inches or exactly 16:9.

## Get started

1. Download the Windows x64 ZIP from [Releases](https://github.com/4MZ31/amd-resolution-tray/releases), extract it to a permanent folder and run ResolutionTray.exe. The portable release includes the .NET runtime.
2. Select your native 1440p display. Its current refresh rate is preserved.
3. Click **Scanner mes jeux** to scan Steam and Epic Games installations, then check the games to monitor. **Ajouter un jeu…** adds other accessible Windows executables. Existing CS2 / CS:GO users keep their default selection.
4. Choose **Enregistrer et surveiller**. Without a selected game running, the desktop stays unchanged and the tray reads **En attente d’un jeu sélectionné**.
5. Launch a selected game. Confirm the first activation within 15 seconds; otherwise the previous display settings are restored automatically.
6. Closing the last selected game restores the previous desktop and keeps monitoring armed for the next launch. **Désactiver la surveillance** or a tray double-click disables monitoring and restores immediately. **Quitter** restores and exits. The tray icon may be hidden under Windows’ **^** menu.

The initial confirmation is remembered for that display, refresh rate and driver version. Startup with Windows is optional and starts inactive. The interface is currently French and the executable is unsigned.

## Game library

The first visit after upgrading scans automatically. The **Scanner mes jeux** button scans Steam libraries across registered drives and Epic manifests, then looks for executables only within the identified game folders. Scans are cancellable and never launch game files. Search/source filters and rescans retain checked games and manual executable choices.

**Prêt** means the game has a recognized/chosen executable. For **Exécutable à choisir**, select the row and use **Exécutables…** to choose the executable used during gameplay. Ambiguous games require a manual choice. All checked games share the same 2324 × 1306 profile. Save is blocked while a checked game lacks an executable.

Other launchers (GOG, EA, Ubisoft, Xbox…) are not scanned automatically yet; accessible Windows executables can be added manually. Retained entries may belong to a temporarily disconnected drive. Scanned/manual games are matched by executable path; the legacy CS2 / CS:GO fallback matches process names until replaced by a scan.

**Enregistrer** saves choices. **Enregistrer et surveiller** saves and arms monitoring. Saving with all games unchecked stops monitoring. Cancel closes without saving.

## Requirements and scope

Windows 11 x64, AMD Radeon with an Adrenalin driver providing ADLX, and a landscape native 2560 × 1440 monitor directly connected to that GPU. The exact mode must be accepted by the driver and monitor. Extended desktop is supported; cloned displays and Eyefinity are excluded. Identical EDIDs are rejected when display identity would be ambiguous. Other connected displays may have different native resolutions.

The application reuses an existing exact mode or creates one through AMD ADLX using the current signal timings. It enables GPU scaling in Centered mode and disables integer scaling if available. Creating a custom mode is separate from applying it; the custom mode remains available after exit. If automatic creation fails, create an accepted mode manually in Adrenalin and retry. No approximate resolution or silent refresh-rate fallback is applied.

Enable monitoring before or after launching the game. The app checks the selected game executables in the current Windows session about once per second. A minimized/background game still counts as running; restoration happens only after all selected games exit. Rejected confirmation is not retried during the same run; restart the game or toggle monitoring to try again. Closing the game during activation/confirmation cancels the profile and restores the desktop. Verify in-game video settings: a game can select its own display mode. External resolution/scaling changes turn the tray icon gray while keeping restoration available. Physical image size, game behavior, HDR and FreeSync require testing on your setup.

## Recovery and privacy

Original display and scaling settings are stored before any driver mutation. A separate process attempts restoration if the main process crashes. Complete Windows shutdown, driver failure and disconnected monitors can prevent immediate recovery; the journal is retained for the next launch. Do not delete recovery.json until the display is restored.

Settings, recovery data and local error logs live in **%LOCALAPPDATA%\AmdResolutionTray**. There is no telemetry. A read-only diagnostic can be generated with:

~~~powershell
.\ResolutionTray.exe --diagnose "$env:TEMP\resolution-tray.json"
~~~

Diagnostics include monitor identifiers; remove them before posting a public issue. Use --settings to reopen settings and --quit to request a graceful exit. --scan-games <file.json> exports a read-only game scan containing installation names and paths. From v0.4.0, portable releases update automatically; upgrading from an older version requires one final manual replacement. Existing settings are preserved.

## Automatic updates

The portable app checks this GitHub repository at startup and every six hours, including numbered experimental releases. Older versions and drafts are ignored. Network errors leave the app running and are retried later. **Mettre à jour automatiquement** in settings enables/disables this behavior; **Mettre à jour maintenant…** in the tray menu checks and installs on demand.

Downloads are checked against the published SHA-256 and GitHub digest when present, then the Windows x64 executable format and file version are verified. Installation waits until selected games and configuration windows are closed. The desktop is restored before exit. A separate process replaces the executable in its current folder, relaunches it and keeps a backup until startup is acknowledged. Failed startup restores and relaunches the previous version. Armed monitoring resumes, and settings are preserved.

The install folder must be writable by the current user; no elevation is requested. Upgrade from v0.3.0 or earlier once by quitting the old app, extracting v0.4.0 and replacing ResolutionTray.exe. Future updates are automatic. Development builds do not replace themselves. Temporary downloads under %LOCALAPPDATA%\AmdResolutionTray\Updates are cleaned during later checks. Update requests contact GitHub and its download servers; game paths and settings stay local. Checksums verify download integrity, not publisher signatures. To uninstall, disable startup in settings, exit and remove the extracted folder. The custom AMD mode can be removed separately in Adrenalin.

## Build and validation

.NET 10 SDK on Windows x64; no redistributed AMD binaries or external NuGet dependencies.

~~~powershell
dotnet build ResolutionTray.slnx -c Release
dotnet run --project tests/ResolutionTray.Tests -c Release
.\build.ps1
~~~

Close the app before rebuilding into the same output folder. build.ps1 runs 92 deterministic tests (including manifests, executable matching, multiple simultaneous games and cancellation scenarios), publishes a self-contained executable and creates a ZIP plus SHA-256 checksum under artifacts/. GitHub Actions performs the same checks on Windows, plus native updater restart and rollback tests on isolated executable copies.

Experimental first version: mode creation/application and automatic restoration were verified on a Radeon RX 7800 XT with a MSI MAG272CQR at 144 Hz. Two other connected displays retained their original settings after restoration. This does not establish compatibility with every monitor or validate CS2 rendering. Multi-game selection is covered by automated tests. Real Steam/Epic scanning, search, checkbox selection and rescan retention were verified. Newly selected games still need validation on each installation.

[MIT license](../LICENSE). Independent project, not affiliated with AMD or Valve. See the [French documentation](../README.md) for implementation details and troubleshooting.
