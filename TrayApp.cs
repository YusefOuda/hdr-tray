using Microsoft.Win32;

namespace HdrTray;

class TrayApp : ApplicationContext
{
    readonly NotifyIcon _tray;
    readonly System.Windows.Forms.Timer _timer;
    readonly System.Windows.Forms.Timer _updateTimer;
    readonly ContextMenuStrip _menu = new();
    readonly AppSettings _settings;
    List<DisplayInfo> _displays = [];
    HdrState? _shownState;
    string? _shownTooltip;
    // Menu items that reflect the current state; updated in place while the menu is open
    ToolStripMenuItem? _statusItem;
    ToolStripMenuItem? _toggleItem;
    // Tag of a newer release, if one was found
    string? _availableUpdate;
    string? _notifiedUpdate;
    // What clicking the current balloon does; balloons are shared by all notifications
    Action? _balloonClick;

    // HDR can also be changed from Windows Settings or Win+Alt+B, which raise no event we can rely on
    const int PollIntervalMs = 2000;
    static readonly TimeSpan FirstUpdateCheckDelay = TimeSpan.FromSeconds(30);
    static readonly TimeSpan UpdateCheckInterval = TimeSpan.FromDays(1);

    public TrayApp()
    {
        _settings = AppSettings.Load();

        _tray = new NotifyIcon { Text = "hdr-tray", Visible = true, ContextMenuStrip = _menu };
        _tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) Toggle(); };
        _tray.BalloonTipClicked += (_, _) => _balloonClick?.Invoke();
        // Rebuilt each time so the monitor list is current
        // e.Cancel is preset when the menu is empty, so clear it after populating
        _menu.Opening += (_, e) => { Refresh(); BuildMenu(); e.Cancel = false; };

        Refresh();
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

        _timer = new System.Windows.Forms.Timer { Interval = PollIntervalMs };
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();

        // Short first delay so startup isn't slowed, then daily
        _updateTimer = new System.Windows.Forms.Timer { Interval = (int)FirstUpdateCheckDelay.TotalMilliseconds };
        _updateTimer.Tick += UpdateTimer_Tick;
        _updateTimer.Start();
    }

    void OnDisplaySettingsChanged(object? sender, EventArgs e) => Refresh();

    List<DisplayInfo> Targets() => HdrPolicy.Targets(_displays, _settings.IsIncluded);

    void Refresh()
    {
        _displays = DisplayConfig.GetDisplays();
        var targets = Targets();
        var state = HdrPolicy.State(targets);
        var tooltip = TooltipText(state, targets);

        if (state != _shownState)
        {
            _shownState = state;
            var oldIcon = _tray.Icon;
            _tray.Icon = IconRenderer.ForState(state);
            oldIcon?.Dispose();
        }
        if (tooltip != _shownTooltip)
        {
            _shownTooltip = tooltip;
            // NotifyIcon.Text is limited to 127 characters
            _tray.Text = tooltip.Length > 127 ? tooltip[..127] : tooltip;
        }
        UpdateStatusItems(state);
    }

    static string TooltipText(HdrState state, List<DisplayInfo> targets) => state switch
    {
        HdrState.None => "hdr-tray\nNo HDR monitor selected",
        _ => "hdr-tray\n" + string.Join("\n", targets.Select(d => $"{d.Name}: {(d.HdrEnabled ? "HDR" : "SDR")}")),
    };

    void Toggle()
    {
        _displays = DisplayConfig.GetDisplays();
        var targets = Targets();
        if (targets.Count == 0)
        {
            ShowBalloon("No HDR monitor selected", "Right-click the icon and pick monitors under Monitors.", ToolTipIcon.Info);
            return;
        }

        bool enable = HdrPolicy.ToggleTo(targets);
        var failed = targets.Where(d => d.HdrEnabled != enable && !DisplayConfig.SetHdr(d, enable)).ToList();
        if (failed.Count > 0)
            ShowBalloon($"Couldn't turn HDR {(enable ? "on" : "off")}", string.Join(", ", failed.Select(d => d.Name)), ToolTipIcon.Warning);
        Refresh();
    }

    async void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        _updateTimer.Interval = (int)UpdateCheckInterval.TotalMilliseconds;
        await CheckForUpdateAsync();
    }

    // The menu is rebuilt on every open, so a found update shows up there without further work
    async Task CheckForUpdateAsync()
    {
        if (!_settings.CheckForUpdates) return;
        var tag = await UpdateChecker.CheckAsync();
        if (tag == null) return;

        _availableUpdate = tag;
        if (_notifiedUpdate != tag)
        {
            _notifiedUpdate = tag;
            ShowBalloon("Update available", $"hdr-tray {tag} is available. Click to open the download page.",
                ToolTipIcon.Info, OpenReleasesPage);
        }
    }

    static void OpenReleasesPage()
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(UpdateChecker.ReleasesPage) { UseShellExecute = true }); }
        catch { }
    }

    void ShowBalloon(string title, string text, ToolTipIcon icon, Action? onClick = null)
    {
        _balloonClick = onClick;
        _tray.ShowBalloonTip(5000, title, text, icon);
    }

    void BuildMenu()
    {
        _menu.Items.Clear();

        _statusItem = new ToolStripMenuItem { Enabled = false };
        _menu.Items.Add(_statusItem);
        _toggleItem = new ToolStripMenuItem();
        _toggleItem.Click += (_, _) => Toggle();
        _menu.Items.Add(_toggleItem);
        UpdateStatusItems(HdrPolicy.State(Targets()));

        if (_availableUpdate != null)
        {
            var update = new ToolStripMenuItem($"Update available ({_availableUpdate})...");
            update.Click += (_, _) => OpenReleasesPage();
            _menu.Items.Add(update);
        }
        _menu.Items.Add(new ToolStripSeparator());

        var monitors = new ToolStripMenuItem("Monitors");
        foreach (var display in _displays)
        {
            if (!display.HdrSupported)
            {
                monitors.DropDownItems.Add(new ToolStripMenuItem($"{display.Name} (no HDR)") { Enabled = false });
                continue;
            }
            var item = new ToolStripMenuItem(display.Name) { CheckOnClick = true, Checked = _settings.IsIncluded(display.DevicePath) };
            item.CheckedChanged += (_, _) =>
            {
                _settings.SetIncluded(display.DevicePath, item.Checked);
                _settings.Save();
                Refresh();
            };
            monitors.DropDownItems.Add(item);
        }
        if (monitors.DropDownItems.Count == 0)
            monitors.DropDownItems.Add(new ToolStripMenuItem("No monitors found") { Enabled = false });
        // Stay open so several monitors can be ticked in one go
        monitors.DropDown.Closing += (_, e) =>
        {
            if (e.CloseReason == ToolStripDropDownCloseReason.ItemClicked) e.Cancel = true;
        };
        _menu.Items.Add(monitors);

        _menu.Items.Add(BuildSettingsSubmenu());

        _menu.Items.Add(new ToolStripSeparator());
        var exit = new ToolStripMenuItem("Exit");
        exit.Click += (_, _) => ExitApp();
        _menu.Items.Add(exit);
    }

    ToolStripMenuItem BuildSettingsSubmenu()
    {
        var parent = new ToolStripMenuItem("Settings");

        var checkUpdates = new ToolStripMenuItem("Check for updates") { CheckOnClick = true, Checked = _settings.CheckForUpdates };
        checkUpdates.CheckedChanged += async (_, _) =>
        {
            _settings.CheckForUpdates = checkUpdates.Checked;
            _settings.Save();
            if (checkUpdates.Checked) await CheckForUpdateAsync();
            else _availableUpdate = null;
        };
        parent.DropDownItems.Add(checkUpdates);

        var startWithWindows = new ToolStripMenuItem("Start with Windows") { CheckOnClick = true, Checked = _settings.StartWithWindows };
        startWithWindows.CheckedChanged += (_, _) =>
        {
            _settings.StartWithWindows = startWithWindows.Checked;
            _settings.Save();
            ApplyStartWithWindows(startWithWindows.Checked);
        };
        parent.DropDownItems.Add(startWithWindows);

        return parent;
    }

    // Keeps the status line and toggle item current while the menu is open (e.g. after ticking a monitor)
    void UpdateStatusItems(HdrState state)
    {
        if (_statusItem == null || _toggleItem == null) return;
        _statusItem.Text = state switch
        {
            HdrState.Hdr => "HDR is on",
            HdrState.Sdr => "HDR is off",
            _ => "No HDR monitor selected",
        };
        _toggleItem.Text = state == HdrState.Hdr ? "Switch to SDR" : "Switch to HDR";
        _toggleItem.Visible = state != HdrState.None;
    }

    static void ApplyStartWithWindows(bool enable)
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
        if (key == null) return;
        if (enable)
            key.SetValue("hdr-tray", $"\"{Application.ExecutablePath}\"");
        else
            key.DeleteValue("hdr-tray", throwOnMissingValue: false);
    }

    void ExitApp()
    {
        _timer.Stop();
        _updateTimer.Stop();
        _tray.Visible = false;
        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            _timer.Dispose();
            _updateTimer.Dispose();
            _tray.Icon?.Dispose();
            _tray.Dispose();
            _menu.Dispose();
        }
        base.Dispose(disposing);
    }
}
