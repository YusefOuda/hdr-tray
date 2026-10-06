namespace HdrTray;

enum HdrState { None, Sdr, Hdr }

// Pure rules for what the icon shows and what a click does; kept out of TrayApp so it can be tested.
static class HdrPolicy
{
    // Monitors the toggle acts on: HDR-capable and not excluded in settings
    public static List<DisplayInfo> Targets(IEnumerable<DisplayInfo> displays, Func<string, bool> isIncluded) =>
        displays.Where(d => d.HdrSupported && isIncluded(d.DevicePath)).ToList();

    // HDR if any target has it on, so a mixed state reads as HDR and a click turns everything off
    public static HdrState State(IReadOnlyCollection<DisplayInfo> targets) =>
        targets.Count == 0 ? HdrState.None
        : targets.Any(d => d.HdrEnabled) ? HdrState.Hdr
        : HdrState.Sdr;

    // What a click sets every target to
    public static bool ToggleTo(IReadOnlyCollection<DisplayInfo> targets) => State(targets) != HdrState.Hdr;
}
