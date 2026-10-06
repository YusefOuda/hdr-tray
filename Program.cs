namespace HdrTray;

static class Program
{
    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, "hdr-tray-{6A1F0C2E-8B47-4D3A-9E15-C2D7B8F40A93}", out var firstInstance);
        if (!firstInstance) return;

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.Run(new TrayApp());
    }
}
