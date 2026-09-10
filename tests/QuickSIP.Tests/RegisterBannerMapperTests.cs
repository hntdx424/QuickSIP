using QuickSIP.Core.Sip;
using Xunit;

namespace QuickSIP.Tests;

public sealed class RegisterBannerMapperTests
{
    [Fact]
    public void NullResponse_IsUnreachable()
    {
        var failure = RegisterBannerMapper.Map(null, "Connection timed out");
        Assert.Equal(RegisterFailureKind.Unreachable, failure.Kind);
        Assert.Contains("reach the SIP server", failure.Banner, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DnsFailure_IsUnreachable()
    {
        var failure = RegisterBannerMapper.Map(null, "DNS host not found");
        Assert.Equal(RegisterFailureKind.Unreachable, failure.Kind);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(407)]
    public void AuthFailures(int code)
    {
        var failure = RegisterBannerMapper.Map(code, "Forbidden");
        Assert.Equal(RegisterFailureKind.Authentication, failure.Kind);
        Assert.Contains(code.ToString(), failure.Banner);
        Assert.Contains("authentication", failure.Banner, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NotFound_404()
    {
        var failure = RegisterBannerMapper.Map(404);
        Assert.Equal(RegisterFailureKind.NotFound, failure.Kind);
        Assert.Contains("404", failure.Banner);
    }

    [Theory]
    [InlineData(408)]
    [InlineData(504)]
    public void Timeouts(int code)
    {
        var failure = RegisterBannerMapper.Map(code);
        Assert.Equal(RegisterFailureKind.Timeout, failure.Kind);
        Assert.Contains(code.ToString(), failure.Banner);
    }

    [Theory]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    public void ServerErrors(int code)
    {
        var failure = RegisterBannerMapper.Map(code);
        Assert.Equal(RegisterFailureKind.ServerError, failure.Kind);
        Assert.Contains("server error", failure.Banner, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ServerErrorWithUnreachableWording_PrefersUnreachable()
    {
        var failure = RegisterBannerMapper.Map(503, "network unreachable");
        Assert.Equal(RegisterFailureKind.Unreachable, failure.Kind);
    }
}
