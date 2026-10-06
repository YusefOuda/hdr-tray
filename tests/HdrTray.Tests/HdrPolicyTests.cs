namespace HdrTray.Tests;

public class HdrPolicyTests
{
    static DisplayInfo Display(string path, bool supported = true, bool enabled = false) =>
        new(path, path, supported, enabled);

    [Fact]
    public void Targets_SkipsUnsupportedAndExcluded()
    {
        var displays = new[] { Display("a"), Display("b", supported: false), Display("c") };
        var targets = HdrPolicy.Targets(displays, path => path != "c");
        Assert.Equal(["a"], targets.Select(d => d.DevicePath));
    }

    [Fact]
    public void State_NoTargets_IsNone() =>
        Assert.Equal(HdrState.None, HdrPolicy.State([]));

    [Fact]
    public void State_AllOff_IsSdr() =>
        Assert.Equal(HdrState.Sdr, HdrPolicy.State([Display("a"), Display("b")]));

    [Fact]
    public void State_Mixed_IsHdr() =>
        Assert.Equal(HdrState.Hdr, HdrPolicy.State([Display("a", enabled: true), Display("b")]));

    [Fact]
    public void ToggleTo_FromSdr_EnablesHdr() =>
        Assert.True(HdrPolicy.ToggleTo([Display("a"), Display("b")]));

    [Fact]
    public void ToggleTo_FromMixed_DisablesHdr() =>
        Assert.False(HdrPolicy.ToggleTo([Display("a", enabled: true), Display("b")]));
}
