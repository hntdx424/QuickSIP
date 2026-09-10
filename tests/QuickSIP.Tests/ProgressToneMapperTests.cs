using QuickSIP.Core.Sip;
using QuickSIP.Core.Tones;
using Xunit;

namespace QuickSIP.Tests;

public sealed class ProgressToneMapperTests
{
    [Theory]
    [InlineData(180)]
    [InlineData(183)]
    public void Ringback_WhenNoEarlyMedia(int code)
    {
        Assert.Equal(ToneKind.Ringback, ProgressToneMapper.FromProvisional(code, hasEarlyMedia: false));
    }

    [Fact]
    public void EarlyMedia_SuppressesLocalRingback()
    {
        Assert.Null(ProgressToneMapper.FromProvisional(183, hasEarlyMedia: true));
    }

    [Fact]
    public void AnswerAndHangup()
    {
        Assert.Equal(ToneKind.Answer, ProgressToneMapper.FromAnswer());
        Assert.Equal(ToneKind.Hangup, ProgressToneMapper.FromHangup());
    }

    [Theory]
    [InlineData(486, ToneKind.Busy)]
    [InlineData(600, ToneKind.Busy)]
    [InlineData(480, ToneKind.Congestion)]
    [InlineData(408, ToneKind.Congestion)]
    [InlineData(503, ToneKind.Congestion)]
    [InlineData(603, ToneKind.Congestion)]
    [InlineData(404, ToneKind.Error)]
    [InlineData(500, ToneKind.Error)]
    public void FailureTones(int code, ToneKind expected)
    {
        Assert.Equal(expected, ProgressToneMapper.FromFailure(code, unreachable: false));
    }

    [Fact]
    public void Unreachable_UsesErrorTone()
    {
        Assert.Equal(ToneKind.Error, ProgressToneMapper.FromFailure(null, unreachable: true));
    }

    [Fact]
    public void LocalCancel_IsSilent()
    {
        Assert.Equal(ToneKind.Silence, ProgressToneMapper.FromFailure(487, unreachable: false));
    }
}
