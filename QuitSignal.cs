namespace HdrTray;

// Lets the installer close the running instance cleanly (tray icon removed) before it replaces the
// exe: "<exe> --quit" sets this event and the running instance exits. Restart Manager can't close
// a tray app with no visible window.
static class QuitSignal
{
    const string Name = @"Local\hdr-tray-{6A1F0C2E-8B47-4D3A-9E15-C2D7B8F40A93}-quit";
    static EventWaitHandle? _event;
    static RegisteredWaitHandle? _wait;

    public static void Send()
    {
        if (EventWaitHandle.TryOpenExisting(Name, out var signal))
            using (signal) signal.Set();
    }

    // Runs onQuit on the calling (UI) thread once another process calls Send
    public static void Listen(Action onQuit)
    {
        var ui = SynchronizationContext.Current ?? throw new InvalidOperationException("Call on the UI thread");
        _event = new EventWaitHandle(false, EventResetMode.AutoReset, Name);
        _wait = ThreadPool.RegisterWaitForSingleObject(_event, (_, _) => ui.Post(_ =>
        {
            Log.Info("Quit requested (installer)");
            onQuit();
        }, null), null, Timeout.Infinite, executeOnlyOnce: true);
    }
}
