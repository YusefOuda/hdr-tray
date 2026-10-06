using System.Text.Json;

namespace HdrTray;

class AppSettings
{
    // Monitor device paths the toggle should leave alone. An exclude list (rather than include)
    // means newly connected HDR monitors are toggled by default.
    public List<string> ExcludedMonitors { get; set; } = [];
    public bool StartWithWindows { get; set; } = false;
    public bool CheckForUpdates { get; set; } = true;

    static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "hdr-tray",
        "settings.json");

    static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new();
        }
        catch { }
        return new();
    }

    public bool IsIncluded(string monitorPath) => !ExcludedMonitors.Contains(monitorPath);

    public void SetIncluded(string monitorPath, bool included)
    {
        ExcludedMonitors.Remove(monitorPath);
        if (!included) ExcludedMonitors.Add(monitorPath);
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, WriteOptions));
        }
        catch { }
    }
}
