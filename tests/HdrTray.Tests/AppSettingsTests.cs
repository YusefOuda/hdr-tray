namespace HdrTray.Tests;

public class AppSettingsTests
{
    [Fact]
    public void NewMonitors_AreIncludedByDefault() =>
        Assert.True(new AppSettings().IsIncluded("any"));

    [Fact]
    public void SetIncluded_RoundTrips()
    {
        var settings = new AppSettings();
        settings.SetIncluded("a", false);
        settings.SetIncluded("a", false);
        Assert.False(settings.IsIncluded("a"));
        Assert.Single(settings.ExcludedMonitors);

        settings.SetIncluded("a", true);
        Assert.True(settings.IsIncluded("a"));
        Assert.Empty(settings.ExcludedMonitors);
    }
}
