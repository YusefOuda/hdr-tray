# hdr-tray

A tiny Windows tray app that shows whether HDR is on and toggles it with a click.

- **Left-click**: toggle HDR on/off for the selected monitors (if any is on, all are turned off)
- **Right-click**: pick which monitors to toggle (`Monitors`); `Settings` has update checks and start with Windows

The icon shows **HDR** (orange) or **SDR** (blue-grey). Changes made elsewhere (Windows Settings, Win+Alt+B) are picked up within ~2 seconds.

Requires Windows 10/11 and an HDR-capable display. Settings live in `%APPDATA%\hdr-tray\settings.json`.

## Install

Download `hdr-tray-setup.exe` (or the portable `hdr-tray.zip`) from [Releases](https://github.com/YusefOuda/hdr-tray/releases).

## Releasing

Push a `v*` tag (e.g. `v1.0.0`). GitHub Actions runs the tests, publishes a self-contained single-file exe versioned from the tag, builds the Inno Setup installer, and attaches both to a GitHub Release.
