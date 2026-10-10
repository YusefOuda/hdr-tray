# CLAUDE.md

## Build & run

```powershell
dotnet build
dotnet test
dotnet run --project hdr-tray.csproj
dotnet publish hdr-tray.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

A running instance locks `bin\Debug\...\HdrTray.exe`; exit it from the tray before rebuilding.

## Architecture

`.NET 8` WinForms `ApplicationContext` owning a `NotifyIcon` (same shape as usb-headset-tray). Root namespace `HdrTray`, output `HdrTray.exe`.

| File | Role |
|---|---|
| `Program.cs` | Single-instance mutex, then `Application.Run(new TrayApp())` |
| `TrayApp.cs` | Tray icon, left-click toggle, right-click menu (rebuilt on open; Monitors submenu stays open on click), 2 s poll + `DisplaySettingsChanged` refresh, update check (30 s after start, then daily) |
| `UpdateChecker.cs` | GitHub latest-release check, same as usb-headset-tray; skipped for 0.0.0 dev builds |
| `HdrPolicy.cs` | Pure rules: which monitors are targets, icon state (HDR if any target is on), what a click sets |
| `DisplayConfig.cs` | `QueryDisplayConfig` / `DisplayConfigGetDeviceInfo` / `DisplayConfigSetDeviceInfo` interop |
| `IconRenderer.cs` | "HDR" (orange) / "SDR" (blue-grey; grey when no target) badge at `SystemInformation.SmallIconSize` |
| `AppSettings.cs` | `%APPDATA%\hdr-tray\settings.json`: `ExcludedMonitors` (device paths), `StartWithWindows`, `CheckForUpdates` |
| `Log.cs` | Append-only log at `%APPDATA%\hdr-tray\hdr-tray.log` (rolls to `.old.log` at 1 MB). `Program` routes unhandled exceptions here instead of the WinForms Continue/Quit dialog |

### HDR API notes

- Windows 11 24H2+: `GET_ADVANCED_COLOR_INFO_2` (15) / `SET_HDR_STATE` (16). HDR state = `highDynamicRangeUserEnabled` (bit 5), supported = bit 4
- Older builds: `GET_ADVANCED_COLOR_INFO` (9) / `SET_ADVANCED_COLOR_STATE` (10); `wideColorEnforced` (bit 2) means an ACM SDR display, treated as not HDR
- The new request is tried first; if it fails the legacy pair is used
- Monitors are keyed by `monitorDevicePath` from `GET_TARGET_NAME` (stable across reboots). Settings store an exclude list so new monitors are toggled by default
- Tests cover `HdrPolicy`, `AppSettings` and `UpdateChecker.IsNewer` only; keep logic out of `TrayApp` and the interop

### Distribution

`.github/workflows/release.yml` runs on `v*` tags: tests, self-contained single-file win-x64 publish (version from the tag), zip, Inno Setup installer (`installer.iss`), GitHub Release. Dev builds are `0.0.0`.
