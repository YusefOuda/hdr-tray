namespace HdrTray;

static class Program
{
    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, "hdr-tray-{6A1F0C2E-8B47-4D3A-9E15-C2D7B8F40A93}", out var firstInstance);
        if (!firstInstance) return;

        Log.Info($"hdr-tray {UpdateChecker.CurrentVersion} starting");
        // Log UI-thread exceptions and keep running instead of showing WinForms' Continue/Quit dialog
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Log.Error("Unhandled exception on the UI thread", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Log.Error($"Unhandled exception (terminating: {e.IsTerminating})", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) => Log.Error("Unobserved task exception", e.Exception);

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.Run(new TrayApp());
        Log.Info("Exiting");
    }
}
