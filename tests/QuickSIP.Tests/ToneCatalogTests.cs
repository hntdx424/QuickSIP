using QuickSIP.Core.Tones;
using Xunit;

namespace QuickSIP.Tests;

public sealed class ToneCatalogTests
{
    [Fact]
    public void DialTone_IsContinuousDualTone()
    {
        var spec = ToneCatalog.Get(ToneKind.Dial);
        Assert.Equal(350, spec.Frequency1);
        Assert.Equal(440, spec.Frequency2);
        Assert.True(spec.IsContinuous);
        Assert.True(spec.IsDualTone);
    }

    [Fact]
    public void Ringback_UsesNorthAmericanCadence()
    {
        var spec = ToneCatalog.Ringback;
        Assert.Equal(2000, spec.OnMs);
        Assert.Equal(4000, spec.OffMs);
        Assert.Equal(440, spec.Frequency1);
        Assert.Equal(480, spec.Frequency2);
    }

    [Fact]
    public void BusyVsCongestion_Cadence()
    {
        Assert.Equal(500, ToneCatalog.Busy.OnMs);
        Assert.Equal(500, ToneCatalog.Busy.OffMs);
        Assert.Equal(250, ToneCatalog.Congestion.OnMs);
        Assert.Equal(250, ToneCatalog.Congestion.OffMs);
    }

    [Fact]
    public void Generator_ProducesAudibleSamples()
    {
        var pcm = TonePcmGenerator.Generate(ToneCatalog.Dial, durationMs: 200, sampleRate: 8000);
        Assert.Equal(1600, pcm.Length);
        Assert.Contains(pcm, s => Math.Abs(s) > 100);
    }

    [Fact]
    public void Generator_HonorsSilenceGaps()
    {
        var spec = ToneCatalog.Busy;
        var pcm = TonePcmGenerator.Generate(spec, durationMs: 1000, sampleRate: 8000);
        int on = spec.OnMs * 8;
        Assert.Contains(pcm.Take(on), s => Math.Abs(s) > 50);
        Assert.All(pcm.Skip(on).Take(spec.OffMs * 8), s => Assert.Equal(0, s));
    }
}
